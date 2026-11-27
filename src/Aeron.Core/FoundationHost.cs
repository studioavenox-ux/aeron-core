using System.Runtime.InteropServices;
using Aeron.Contracts;
using Aeron.Infrastructure;
using Aeron.Security;

namespace Aeron.Core;

/// <summary>
/// Captures truthful, empirically verified runtime health data for all Milestone 1 foundation subsystems.
/// </summary>
public sealed class FoundationHealthService : IFoundationHealthService
{
    private readonly IAppPaths _paths;
    private readonly IAppLifecycle _lifecycle;
    private readonly IDatabaseHealthProbe _databaseHealthProbe;
    private readonly IOsBaselineEvaluator _osBaselineEvaluator;
    private readonly IAeronLogger _logger;
    private readonly IClock _clock;

    public FoundationHealthService(
        IAppPaths paths,
        IAppLifecycle lifecycle,
        IDatabaseHealthProbe databaseHealthProbe,
        IOsBaselineEvaluator osBaselineEvaluator,
        IAeronLogger logger,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(databaseHealthProbe);
        ArgumentNullException.ThrowIfNull(osBaselineEvaluator);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(clock);

        _paths = paths;
        _lifecycle = lifecycle;
        _databaseHealthProbe = databaseHealthProbe;
        _osBaselineEvaluator = osBaselineEvaluator;
        _logger = logger;
        _clock = clock;
    }

    public async Task<FoundationHealthSnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var config = _lifecycle.CurrentConfig;
        var database = await _databaseHealthProbe.ProbeAsync(cancellationToken).ConfigureAwait(false);
        var osReport = _osBaselineEvaluator.Evaluate(
            config.Platform.MinimumWindowsBuild,
            config.Platform.MinimumWindowsRelease);

        var version = typeof(FoundationHealthService).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

        return new FoundationHealthSnapshot(
            ApplicationVersion: version,
            RuntimeDescription: RuntimeInformation.FrameworkDescription,
            InstanceId: _lifecycle.InstanceId,
            LifecycleState: _lifecycle.CurrentState,
            CapturedAtUtc: _clock.UtcNow,
            StartedAtUtc: _lifecycle.StartedAtUtc,
            AppDataRootPath: _paths.RootDirectory,
            ConfigValid: _lifecycle.IsConfigValid,
            CloseToTrayEnabled: config.Window.CloseToTray,
            SecurityInvariantsLocked: _lifecycle.IsConfigValid &&
                                      ConfigPolicyValidator.AreSecurityInvariantsLocked(config),
            RedactingLoggerActive: _logger.IsActive,
            Database: database,
            OsBaseline: osReport);
    }
}

/// <summary>
/// Modular composition root wiring AERON Milestone 1 foundation services in deterministic
/// startup and teardown order without exposing persistence internals to the WPF shell.
/// </summary>
public sealed class AeronFoundationHost : IAsyncDisposable
{
    private int _disposed;

    private AeronFoundationHost(
        IAppPaths paths,
        IRedactionFilter redactionFilter,
        IAeronLogger logger,
        ISecureConfigStore configStore,
        ICoreEventBus eventBus,
        IAppLifecycle lifecycle,
        IFoundationHealthService healthService,
        ISecurityPolicy securityPolicy,
        SecurityKernelStartupResult securityKernel)
    {
        Paths = paths;
        RedactionFilter = redactionFilter;
        Logger = logger;
        ConfigStore = configStore;
        EventBus = eventBus;
        Lifecycle = lifecycle;
        HealthService = healthService;
        SecurityPolicy = securityPolicy;
        SecurityKernel = securityKernel;
    }

    public IAppPaths Paths { get; }
    public IRedactionFilter RedactionFilter { get; }
    public IAeronLogger Logger { get; }
    public ISecureConfigStore ConfigStore { get; }
    public ICoreEventBus EventBus { get; }
    public IAppLifecycle Lifecycle { get; }
    public IFoundationHealthService HealthService { get; }
    /// <summary>
    /// Legacy M2 boundary policy from the Phase B foundation. It evaluates requests with
    /// no trusted identity provider and therefore denies. The production authority path
    /// is <see cref="SecurityKernel"/>; this property remains for the foundation health
    /// surface and its existing regression tests.
    /// </summary>
    public ISecurityPolicy SecurityPolicy { get; }

    /// <summary>
    /// The M2 production security kernel: identity authority, durable audit ledger,
    /// approval engine, action dispatcher, preemption state, and vault scaffolding.
    /// Construction is fail-closed; <see cref="SecurityKernelStartupResult.IsOperational"/>
    /// reports whether the kernel verified at startup.
    /// </summary>
    public SecurityKernelStartupResult SecurityKernel { get; }

    public static AeronFoundationHost Create(IAppPaths? customPaths = null, IClock? customClock = null)
    {
        var paths = customPaths ?? AppPaths.CreateDefault();
        var clock = customClock ?? SystemClock.Instance;
        var redactionFilter = new RedactionFilter();
        var logger = new RedactingLogger(paths, redactionFilter, clock);
        var validator = new ConfigPolicyValidator(redactionFilter);
        var configStore = new SecureConfigStore(paths, validator, redactionFilter, logger);
        var connectionFactory = new SqliteConnectionFactory(paths);
        var migrationRunner = new SqliteMigrationRunner(connectionFactory, clock, logger);
        var databaseService = new SqliteDatabaseService(connectionFactory, migrationRunner, redactionFilter);
        var osEvaluator = new OsBaselineEvaluator();
        var eventBus = new CoreEventBus(logger);
        var lifecycle = new AppLifecycleCoordinator(
            paths,
            configStore,
            databaseService,
            databaseService,
            databaseService,
            eventBus,
            logger,
            clock);
        var healthService = new FoundationHealthService(
            paths,
            lifecycle,
            databaseService,
            osEvaluator,
            logger,
            clock);

        // M2 security kernel: wired before the lifecycle starts so no operation can run
        // against an unverified audit chain. A broken chain yields an unhealthy kernel that
        // denies everything rather than a fallback path.
        var securityKernel = SecurityComposition.CreateKernel(paths, clock, redactionFilter);

        logger.LogInfo(
            nameof(AeronFoundationHost),
            securityKernel.IsOperational
                ? "M2 security kernel verified and operational."
                : "M2 security kernel is not operational; security-dependent operations are denied.",
            new Dictionary<string, string?>
            {
                ["kernelOperational"] = securityKernel.IsOperational ? "true" : "false",
                ["assetVerified"] = securityKernel.Kernel is not null ? "constructed" : "unavailable"
            });

        return new AeronFoundationHost(
            paths,
            redactionFilter,
            logger,
            configStore,
            eventBus,
            lifecycle,
            healthService,
            // Legacy boundary policy: no trusted identity provider is registered here, so
            // it denies. Production authority flows through SecurityKernel instead.
            new PolicyDecisionEngine(identityProvider: null, grants: null),
            securityKernel);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await Lifecycle.DisposeAsync().ConfigureAwait(false);
        SecurityKernel.Kernel?.Dispose();
        Logger.Flush();
        Logger.Dispose();
    }
}
