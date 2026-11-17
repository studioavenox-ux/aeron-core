namespace Aeron.Contracts;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IAppPaths
{
    string RootDirectory { get; }
    string ConfigDirectory { get; }
    string DataDirectory { get; }
    string BackupsDirectory { get; }
    string LogsDirectory { get; }
    string ConfigFilePath { get; }
    string DatabaseFilePath { get; }
    string ActiveLogFilePath { get; }

    void EnsureCreatedAndSecured();
}

public interface IRedactionFilter
{
    string Redact(string? input);
    void RegisterSensitiveValue(string sensitiveValue);
    bool ContainsPotentialSecret(string? input);
}

public interface IAeronLogger : IDisposable
{
    bool IsActive { get; }

    void Configure(AeronLogLevel minimumLevel, long maxFileBytes, int maxRetainedFiles);

    void Log(
        AeronLogLevel level,
        string category,
        string message,
        IReadOnlyDictionary<string, string?>? properties = null,
        Exception? exception = null);

    void Flush();
}

public static class AeronLoggerExtensions
{
    public static void LogDebug(
        this IAeronLogger logger,
        string category,
        string message,
        IReadOnlyDictionary<string, string?>? properties = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.Log(AeronLogLevel.Debug, category, message, properties);
    }

    public static void LogInfo(
        this IAeronLogger logger,
        string category,
        string message,
        IReadOnlyDictionary<string, string?>? properties = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.Log(AeronLogLevel.Information, category, message, properties);
    }

    public static void LogWarning(
        this IAeronLogger logger,
        string category,
        string message,
        IReadOnlyDictionary<string, string?>? properties = null,
        Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.Log(AeronLogLevel.Warning, category, message, properties, exception);
    }

    public static void LogError(
        this IAeronLogger logger,
        string category,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, string?>? properties = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.Log(AeronLogLevel.Error, category, message, properties, exception);
    }

    public static void LogCritical(
        this IAeronLogger logger,
        string category,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, string?>? properties = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.Log(AeronLogLevel.Critical, category, message, properties, exception);
    }
}

public interface ISecureConfigStore
{
    Result<AeronAppConfig> LoadOrCreateDefault();
    Result Save(AeronAppConfig config);
}

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public interface IDatabaseHealthProbe
{
    Task<DatabaseHealthResult> ProbeAsync(CancellationToken cancellationToken = default);
}

public interface ILifecycleJournalStore
{
    Task RecordTransitionAsync(
        string instanceId,
        AppLifecycleState state,
        string reason,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default);
}

public interface IOsBaselineEvaluator
{
    OsBaselineReport Evaluate(int minimumRequiredBuild, string minimumRequiredRelease);
}

public interface ICoreEventBus
{
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class, ICoreEvent;
    void Publish<TEvent>(TEvent coreEvent) where TEvent : class, ICoreEvent;
}

public interface IAppLifecycle : IAsyncDisposable
{
    string InstanceId { get; }
    AppLifecycleState CurrentState { get; }
    DateTimeOffset? StartedAtUtc { get; }
    CancellationToken ShutdownToken { get; }
    AeronAppConfig CurrentConfig { get; }
    bool IsConfigValid { get; }

    IDisposable RegisterShutdownHook(Func<CancellationToken, Task> shutdownHook);
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(string reason = "Normal shutdown");
    void TriggerEmergencyStop(string reason);
    Task<Result> UpdateCloseToTrayAsync(bool closeToTray, CancellationToken cancellationToken = default);
}

public interface IFoundationHealthService
{
    Task<FoundationHealthSnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken = default);
}
