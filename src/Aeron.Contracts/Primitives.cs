namespace Aeron.Contracts;

/// <summary>
/// Deterministic lifecycle states for the AERON native desktop application.
/// </summary>
public enum AppLifecycleState
{
    Uninitialized = 0,
    Starting = 1,
    Running = 2,
    Stopping = 3,
    Stopped = 4,
    Faulted = 5
}

/// <summary>
/// Severity levels emitted by the redacting structured logger.
/// </summary>
public enum AeronLogLevel
{
    Debug = 0,
    Information = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}
