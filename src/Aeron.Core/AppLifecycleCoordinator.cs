using Aeron.Contracts;

namespace Aeron.Core;

/// <summary>
/// Raised when AERON fails a mandatory fail-closed foundation startup check.
/// </summary>
public sealed class LifecycleStartupException : InvalidOperationException
{
    public LifecycleStartupException(string errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

/// <summary>
/// Coordinates AERON's deterministic startup, steady-state operation, emergency cancellation,
/// LIFO shutdown hooks, and fail-closed startup rollback.
/// </summary>
public sealed class AppLifecycleCoordinator : IAppLifecycle
{
    private static readonly TimeSpan ShutdownHookTimeout = TimeSpan.FromSeconds(5);

    private readonly Lock _stateLock = new();
    private readonly SemaphoreSlim _transitionGate = new(1, 1);
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly List<Func<CancellationToken, Task>> _shutdownHooks = [];

    private readonly IAppPaths _paths;
    private readonly ISecureConfigStore _configStore;
    private readonly IDatabaseInitializer _databaseInitializer;
    private readonly IDatabaseHealthProbe _databaseHealthProbe;
    private readonly ILifecycleJournalStore _journalStore;
    private readonly ICoreEventBus _eventBus;
    private readonly IAeronLogger _logger;
    private readonly IClock _clock;

    private AppLifecycleState _currentState = AppLifecycleState.Uninitialized;
    private AeronAppConfig _currentConfig = AeronAppConfig.CreateDefault();
    private DateTimeOffset? _startedAtUtc;
    private bool _isConfigValid;
    private bool _databaseReady;
    private bool _disposed;

    public AppLifecycleCoordinator(
        IAppPaths paths,
        ISecureConfigStore configStore,
        IDatabaseInitializer databaseInitializer,
        IDatabaseHealthProbe databaseHealthProbe,
        ILifecycleJournalStore journalStore,
        ICoreEventBus eventBus,
        IAeronLogger logger,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(configStore);
        ArgumentNullException.ThrowIfNull(databaseInitializer);
        ArgumentNullException.ThrowIfNull(databaseHealthProbe);
        ArgumentNullException.ThrowIfNull(journalStore);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(clock);

        _paths = paths;
        _configStore = configStore;
        _databaseInitializer = databaseInitializer;
        _databaseHealthProbe = databaseHealthProbe;
        _journalStore = journalStore;
        _eventBus = eventBus;
        _logger = logger;
        _clock = clock;

        InstanceId = $"aeron-{Guid.NewGuid():N}"[..18];
    }

    public string InstanceId { get; }

    public AppLifecycleState CurrentState
    {
        get
        {
            lock (_stateLock)
            {
                return _currentState;
            }
        }
    }

    public DateTimeOffset? StartedAtUtc
    {
        get
        {
            lock (_stateLock)
            {
                return _startedAtUtc;
            }
        }
    }

    public CancellationToken ShutdownToken => _shutdownCts.Token;

    public AeronAppConfig CurrentConfig
    {
        get
        {
            lock (_stateLock)
            {
                return _currentConfig;
            }
        }
    }

    public bool IsConfigValid
    {
        get
        {
            lock (_stateLock)
            {
                return _isConfigValid;
            }
        }
    }

    public static bool IsTransitionAllowed(AppLifecycleState from, AppLifecycleState to) =>
        (from, to) switch
        {
            (AppLifecycleState.Uninitialized, AppLifecycleState.Starting) => true,
            (AppLifecycleState.Uninitialized, AppLifecycleState.Stopped) => true,
            (AppLifecycleState.Starting, AppLifecycleState.Running) => true,
            (AppLifecycleState.Starting, AppLifecycleState.Faulted) => true,
            (AppLifecycleState.Starting, AppLifecycleState.Stopping) => true,
            (AppLifecycleState.Running, AppLifecycleState.Stopping) => true,
            (AppLifecycleState.Running, AppLifecycleState.Faulted) => true,
            (AppLifecycleState.Stopping, AppLifecycleState.Stopped) => true,
            (AppLifecycleState.Stopping, AppLifecycleState.Faulted) => true,
            (AppLifecycleState.Faulted, AppLifecycleState.Stopping) => true,
            (AppLifecycleState.Faulted, AppLifecycleState.Stopped) => true,
            _ => false
        };

    public IDisposable RegisterShutdownHook(Func<CancellationToken, Task> shutdownHook)
    {
        ArgumentNullException.ThrowIfNull(shutdownHook);

        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _shutdownHooks.Add(shutdownHook);
        }

        return new HookRegistration(this, shutdownHook);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed), this);

        await _transitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (CurrentState != AppLifecycleState.Uninitialized)
            {
                throw new InvalidOperationException(
                    $"Invalid lifecycle transition: cannot start AERON from state '{CurrentState}'.");
            }

            TransitionTo(AppLifecycleState.Starting, "Foundation startup initiated");

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _shutdownCts.Token);
            var token = linkedCts.Token;

            // Execute filesystem and configuration bootstrap off the caller thread so UI Dispatcher never blocks.
            var configResult = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                _paths.EnsureCreatedAndSecured();
                return _configStore.LoadOrCreateDefault();
            }, token).ConfigureAwait(false);

            if (configResult.IsFailure)
            {
                throw new LifecycleStartupException(configResult.ErrorCode, configResult.ErrorMessage);
            }

            var loadedConfig = configResult.Value;
            lock (_stateLock)
            {
                _currentConfig = loadedConfig;
                _isConfigValid = true;
            }

            _logger.Configure(
                loadedConfig.Logging.MinimumLevel,
                loadedConfig.Logging.MaxFileBytes,
                loadedConfig.Logging.MaxRetainedFiles);

            _logger.LogInfo(
                nameof(AppLifecycleCoordinator),
                "Validated AERON configuration and security invariants.",
                new Dictionary<string, string?>
                {
                    ["instanceId"] = InstanceId,
                    ["closeToTray"] = loadedConfig.Window.CloseToTray.ToString()
                });

            await _databaseInitializer.InitializeAsync(token).ConfigureAwait(false);

            var dbHealth = await _databaseHealthProbe.ProbeAsync(token).ConfigureAwait(false);
            if (!dbHealth.IsHealthy)
            {
                throw new LifecycleStartupException(
                    "DATABASE_UNHEALTHY",
                    $"SQLite foundation verification failed: {dbHealth.StatusMessage}");
            }

            _databaseReady = true;

            var startedAt = _clock.UtcNow;
            await _journalStore
                .RecordTransitionAsync(InstanceId, AppLifecycleState.Starting, "Startup verified", startedAt, token)
                .ConfigureAwait(false);

            lock (_stateLock)
            {
                _startedAtUtc = startedAt;
            }

            TransitionTo(AppLifecycleState.Running, "Foundation ready");

            await _journalStore
                .RecordTransitionAsync(InstanceId, AppLifecycleState.Running, "Foundation ready", startedAt, token)
                .ConfigureAwait(false);

            _logger.LogInfo(nameof(AppLifecycleCoordinator), "AERON foundation entered Running state.");
        }
        catch (Exception ex)
        {
            CancelRootShutdownTokenSafe();
            await ExecuteShutdownHooksSafeAsync().ConfigureAwait(false);

            if (CurrentState != AppLifecycleState.Faulted &&
                IsTransitionAllowed(CurrentState, AppLifecycleState.Faulted))
            {
                TransitionTo(AppLifecycleState.Faulted, "Foundation startup rolled back due to failure");
            }

            _logger.LogCritical(
                nameof(AppLifecycleCoordinator),
                "AERON foundation startup failed and rolled back cleanly.",
                ex);
            _logger.Flush();
            throw;
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    public async Task StopAsync(string reason = "Normal shutdown")
    {
        if (Volatile.Read(ref _disposed))
        {
            return;
        }

        var effectiveReason = string.IsNullOrWhiteSpace(reason) ? "Normal shutdown" : reason;

        // Cancel immediately before waiting on any gate so background tasks observe cancellation without delay.
        CancelRootShutdownTokenSafe();

        await _transitionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var state = CurrentState;
            if (state == AppLifecycleState.Stopped)
            {
                return;
            }

            if (state == AppLifecycleState.Uninitialized)
            {
                TransitionTo(AppLifecycleState.Stopped, effectiveReason);
                _logger.Flush();
                return;
            }

            TransitionTo(AppLifecycleState.Stopping, effectiveReason);
            _logger.LogInfo(nameof(AppLifecycleCoordinator), $"Stopping AERON foundation: {effectiveReason}");

            await ExecuteShutdownHooksSafeAsync().ConfigureAwait(false);

            if (_databaseReady)
            {
                try
                {
                    await _journalStore
                        .RecordTransitionAsync(
                            InstanceId,
                            AppLifecycleState.Stopped,
                            effectiveReason,
                            _clock.UtcNow,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        nameof(AppLifecycleCoordinator),
                        "Unable to record final Stopped state in lifecycle journal.",
                        exception: ex);
                }
            }

            TransitionTo(AppLifecycleState.Stopped, effectiveReason);
            _logger.LogInfo(nameof(AppLifecycleCoordinator), "AERON foundation stopped cleanly.");
            _logger.Flush();
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    public void TriggerEmergencyStop(string reason)
    {
        var effectiveReason = string.IsNullOrWhiteSpace(reason)
            ? "Owner emergency cancellation invoked"
            : reason;

        CancelRootShutdownTokenSafe();
        _logger.LogCritical(
            nameof(AppLifecycleCoordinator),
            $"Emergency stop triggered: {effectiveReason}");
        _logger.Flush();
    }

    public async Task<Result> UpdateCloseToTrayAsync(
        bool closeToTray,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed), this);

        await _transitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (CurrentState != AppLifecycleState.Running)
            {
                return Result.Failure(
                    "LIFECYCLE_NOT_RUNNING",
                    "Configuration can only be updated while AERON is in the Running state.");
            }

            var updated = CurrentConfig with
            {
                Window = CurrentConfig.Window with { CloseToTray = closeToTray }
            };

            var saveResult = await Task.Run(() => _configStore.Save(updated), cancellationToken)
                .ConfigureAwait(false);

            if (saveResult.IsFailure)
            {
                return saveResult;
            }

            lock (_stateLock)
            {
                _currentConfig = updated;
            }

            _logger.LogInfo(
                nameof(AppLifecycleCoordinator),
                $"Updated Window.CloseToTray preference to '{closeToTray}'.");

            _eventBus.Publish(new ConfigurationUpdatedEvent(closeToTray, _clock.UtcNow));
            return Result.Success();
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Volatile.Read(ref _disposed))
        {
            return;
        }

        await StopAsync("Disposing lifecycle coordinator").ConfigureAwait(false);

        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _shutdownHooks.Clear();
        }

        _shutdownCts.Dispose();
        _transitionGate.Dispose();
    }

    private void TransitionTo(AppLifecycleState nextState, string reason)
    {
        AppLifecycleState previous;
        lock (_stateLock)
        {
            previous = _currentState;
            if (!IsTransitionAllowed(previous, nextState))
            {
                throw new InvalidOperationException(
                    $"Disallowed lifecycle state transition from '{previous}' to '{nextState}'.");
            }

            _currentState = nextState;
        }

        _eventBus.Publish(new LifecycleStateChangedEvent(previous, nextState, reason, _clock.UtcNow));
    }

    private async Task ExecuteShutdownHooksSafeAsync()
    {
        Func<CancellationToken, Task>[] hooks;
        lock (_stateLock)
        {
            if (_shutdownHooks.Count == 0)
            {
                return;
            }

            hooks = _shutdownHooks.ToArray();
        }

        // Execute registered shutdown hooks in reverse registration (LIFO) order.
        for (var i = hooks.Length - 1; i >= 0; i--)
        {
            using var timeoutCts = new CancellationTokenSource(ShutdownHookTimeout);
            try
            {
                await hooks[i](timeoutCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    nameof(AppLifecycleCoordinator),
                    "A registered shutdown hook failed during teardown.",
                    ex);
            }
        }
    }

    private void CancelRootShutdownTokenSafe()
    {
        try
        {
            if (!_shutdownCts.IsCancellationRequested)
            {
                _shutdownCts.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
            // Already disposed during shutdown race.
        }
    }

    private void RemoveShutdownHook(Func<CancellationToken, Task> hook)
    {
        lock (_stateLock)
        {
            _shutdownHooks.Remove(hook);
        }
    }

    private sealed class HookRegistration : IDisposable
    {
        private AppLifecycleCoordinator? _owner;
        private readonly Func<CancellationToken, Task> _hook;

        public HookRegistration(AppLifecycleCoordinator owner, Func<CancellationToken, Task> hook)
        {
            _owner = owner;
            _hook = hook;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.RemoveShutdownHook(_hook);
        }
    }
}
