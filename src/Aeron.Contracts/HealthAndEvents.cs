namespace Aeron.Contracts;

/// <summary>
/// Result of evaluating the current host OS against AERON's minimum supported Windows baseline.
/// </summary>
public sealed record OsBaselineReport(
    bool IsWindows,
    bool Is64BitOperatingSystem,
    int DetectedBuild,
    int MinimumRequiredBuild,
    string MinimumRequiredRelease,
    bool MeetsMinimumBaseline,
    string Summary);

/// <summary>
/// Verified live health result for the local SQLite persistence store.
/// </summary>
public sealed record DatabaseHealthResult(
    bool IsHealthy,
    string JournalMode,
    bool ForeignKeysEnabled,
    bool IntegrityOk,
    int AppliedMigrationCount,
    int LatestSchemaVersion,
    string StatusMessage);

/// <summary>
/// Truthful runtime health snapshot of all Milestone 1 foundation subsystems.
/// </summary>
public sealed record FoundationHealthSnapshot(
    string ApplicationVersion,
    string RuntimeDescription,
    string InstanceId,
    AppLifecycleState LifecycleState,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset? StartedAtUtc,
    string AppDataRootPath,
    bool ConfigValid,
    bool CloseToTrayEnabled,
    bool SecurityInvariantsLocked,
    bool RedactingLoggerActive,
    DatabaseHealthResult Database,
    OsBaselineReport OsBaseline);

/// <summary>
/// Base contract for strongly typed internal application events.
/// </summary>
public interface ICoreEvent
{
    DateTimeOffset OccurredAtUtc { get; }
}

public sealed record LifecycleStateChangedEvent(
    AppLifecycleState PreviousState,
    AppLifecycleState CurrentState,
    string Reason,
    DateTimeOffset OccurredAtUtc) : ICoreEvent;

public sealed record WindowActivationRequestedEvent(
    string Source,
    DateTimeOffset OccurredAtUtc) : ICoreEvent;

public sealed record ConfigurationUpdatedEvent(
    bool CloseToTray,
    DateTimeOffset OccurredAtUtc) : ICoreEvent;
