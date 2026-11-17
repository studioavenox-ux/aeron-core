using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Aeron.Contracts;

namespace Aeron.Desktop.SingleInstance;

/// <summary>
/// Coordinates single-instance execution for AERON within the current Windows user session
/// using a user-scoped named Mutex (owned on a dedicated thread to honor Win32 thread affinity)
/// and a current-user-SID ACL-protected NamedPipe activation channel.
/// Never opens any TCP, UDP, or localhost HTTP socket.
/// </summary>
public sealed class SingleInstanceManager : IAsyncDisposable
{
    public const string ActivateCommand = "AERON:ACTIVATE";
    private const int MaxCommandBytes = 64;
    private static readonly TimeSpan ServerReadTimeout = TimeSpan.FromSeconds(2);
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly string _mutexName;
    private readonly string _pipeName;
    private readonly Thread? _mutexOwnerThread;
    private readonly CancellationTokenSource _listenerCts = new();
    private readonly Lock _listenerLock = new();

    private Task? _listenerTask;
    private int _disposed;

    private SingleInstanceManager(
        string mutexName,
        string pipeName,
        bool isPrimaryInstance,
        bool recoveredAbandonedMutex,
        Thread? mutexOwnerThread)
    {
        _mutexName = mutexName;
        _pipeName = pipeName;
        IsPrimaryInstance = isPrimaryInstance;
        RecoveredAbandonedMutex = recoveredAbandonedMutex;
        _mutexOwnerThread = mutexOwnerThread;
    }

    public string MutexName => _mutexName;

    public string PipeName => _pipeName;

    public bool IsPrimaryInstance { get; }

    public bool RecoveredAbandonedMutex { get; }

    public static SingleInstanceManager Acquire(string scopeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeName);

        var (mutexName, pipeName) = BuildScopedChannelNames(scopeName);
        using var acquiredEvent = new ManualResetEventSlim(false);
        var releaseSignal = new ManualResetEventSlim(false);

        var isPrimary = false;
        var recoveredAbandoned = false;
        Exception? acquisitionFailure = null;

        var ownerThread = new Thread(() =>
        {
            Mutex? mutex = null;
            var ownsMutex = false;

            try
            {
                mutex = new Mutex(initiallyOwned: false, name: mutexName);
                try
                {
                    ownsMutex = mutex.WaitOne(millisecondsTimeout: 0, exitContext: false);
                }
                catch (AbandonedMutexException)
                {
                    ownsMutex = true;
                    recoveredAbandoned = true;
                }

                isPrimary = ownsMutex;
                acquiredEvent.Set();

                if (ownsMutex)
                {
                    releaseSignal.Wait();
                }
            }
            catch (Exception ex)
            {
                acquisitionFailure = ex;
                acquiredEvent.Set();
            }
            finally
            {
                if (ownsMutex && mutex is not null)
                {
                    try
                    {
                        mutex.ReleaseMutex();
                    }
                    catch (ApplicationException)
                    {
                        // Defensive guard; thread is always the acquiring thread.
                    }
                }

                mutex?.Dispose();
                releaseSignal.Dispose();
            }
        })
        {
            IsBackground = true,
            Name = "Aeron.SingleInstanceMutexOwner"
        };

        ownerThread.Start();
        acquiredEvent.Wait();

        if (acquisitionFailure is not null)
        {
            releaseSignal.Set();
            ownerThread.Join(TimeSpan.FromSeconds(2));
            throw new InvalidOperationException("Failed to acquire single-instance synchronization handle.", acquisitionFailure);
        }

        if (!isPrimary)
        {
            ownerThread.Join(TimeSpan.FromSeconds(2));
            return new SingleInstanceManager(
                mutexName,
                pipeName,
                isPrimaryInstance: false,
                recoveredAbandonedMutex: false,
                mutexOwnerThread: null);
        }

        var instance = new SingleInstanceManager(
            mutexName,
            pipeName,
            isPrimaryInstance: true,
            recoveredAbandonedMutex: recoveredAbandoned,
            mutexOwnerThread: ownerThread);

        // Link instance disposal to the owner thread's release signal.
        instance._releaseCallback = () =>
        {
            try
            {
                releaseSignal.Set();
            }
            catch (ObjectDisposedException)
            {
                // Owner thread already exited cleanly.
            }
        };

        return instance;
    }

    private Action? _releaseCallback;

    public static (string MutexName, string PipeName) BuildScopedChannelNames(string scopeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeName);

        using var identity = WindowsIdentity.GetCurrent();
        var sidValue = identity.User?.Value
            ?? throw new InvalidOperationException("Unable to determine current Windows user SID for IPC scoping.");

        var hashBytes = SHA256.HashData(Utf8NoBom.GetBytes($"{sidValue}|{scopeName.Trim()}"));
        var scopeToken = Convert.ToHexStringLower(hashBytes)[..24];

        return (
            MutexName: $@"Local\Aeron.SingleInstance.{scopeToken}",
            PipeName: $"Aeron.Activation.{scopeToken}");
    }

    public static PipeSecurity CreateCurrentUserPipeSecurity()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var currentUserSid = identity.User
            ?? throw new InvalidOperationException("Unable to determine current Windows user SID for pipe ACL.");

        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new PipeAccessRule(
            currentUserSid,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return security;
    }

    public void StartActivationListener(Action onActivationRequested, IAeronLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(onActivationRequested);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        if (!IsPrimaryInstance)
        {
            throw new InvalidOperationException(
                "Only the primary AERON instance may start the activation named-pipe listener.");
        }

        lock (_listenerLock)
        {
            if (_listenerTask is not null)
            {
                throw new InvalidOperationException("Activation listener has already been started.");
            }

            _listenerTask = Task.Run(() => RunActivationLoopAsync(onActivationRequested, logger, _listenerCts.Token));
        }
    }

    public async Task<bool> TrySignalPrimaryInstanceAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Signal timeout must be positive.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await using var client = new NamedPipeClientStream(
                serverName: ".",
                pipeName: _pipeName,
                direction: PipeDirection.Out,
                options: PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            await client.ConnectAsync(timeoutCts.Token).ConfigureAwait(false);

            var payload = Utf8NoBom.GetBytes(ActivateCommand + "\n");
            await client.WriteAsync(payload, timeoutCts.Token).ConfigureAwait(false);
            await client.FlushAsync(timeoutCts.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await _listenerCts.CancelAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Already cancelled/disposed.
        }

        Task? listener;
        lock (_listenerLock)
        {
            listener = _listenerTask;
        }

        if (listener is not null)
        {
            try
            {
                await listener.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown cancellation.
            }
        }

        _releaseCallback?.Invoke();
        _releaseCallback = null;

        _mutexOwnerThread?.Join(TimeSpan.FromSeconds(3));

        _listenerCts.Dispose();
    }

    private async Task RunActivationLoopAsync(
        Action onActivationRequested,
        IAeronLogger? logger,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxCommandBytes];

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var pipeSecurity = CreateCurrentUserPipeSecurity();

                await using var server = NamedPipeServerStreamAcl.Create(
                    pipeName: _pipeName,
                    direction: PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
                    inBufferSize: MaxCommandBytes,
                    outBufferSize: 0,
                    pipeSecurity: pipeSecurity);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                readCts.CancelAfter(ServerReadTimeout);

                var totalRead = 0;
                while (totalRead < buffer.Length)
                {
                    var bytesRead = await server
                        .ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), readCts.Token)
                        .ConfigureAwait(false);

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    totalRead += bytesRead;
                    if (Array.IndexOf(buffer, (byte)'\n', 0, totalRead) >= 0)
                    {
                        break;
                    }
                }

                var command = Utf8NoBom.GetString(buffer, 0, totalRead).Trim();
                if (string.Equals(command, ActivateCommand, StringComparison.Ordinal))
                {
                    logger?.LogInfo(
                        nameof(SingleInstanceManager),
                        "Received valid window activation signal from secondary launch.");
                    onActivationRequested();
                }
                else
                {
                    logger?.LogWarning(
                        nameof(SingleInstanceManager),
                        "Ignored unrecognized single-instance IPC payload.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                // Timed out reading from slow client; recycle pipe server safely.
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger?.LogWarning(
                    nameof(SingleInstanceManager),
                    "Transient named-pipe activation listener fault; recycling listener.",
                    exception: ex);

                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
