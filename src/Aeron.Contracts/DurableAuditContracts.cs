namespace Aeron.Contracts;

// AERON Milestone 2 — durable audit ledger contracts.
// Records carry fixed vocabulary, opaque correlation ids and non-secret detail codes
// only. Free text, identity evidence, payloads and credentials are never recorded.

/// <summary>Fixed audit event kinds. New kinds must be appended and never renumbered.</summary>
public enum DurableAuditKind
{
    Unknown = 0,
    StartupVerified = 1,
    StartupRefused = 2,
    KernelUnavailable = 3,
    RecoveryCompleted = 4,
    OwnerEnrolled = 10,
    OwnerAuthenticated = 11,
    OwnerAuthenticationFailed = 12,
    OwnerPreempted = 13,
    OwnerNotEnrolled = 14,
    OwnerPreemptedDenied = 15,
    AgentRegistered = 20,
    AgentAuthenticated = 21,
    AgentAuthenticationFailed = 22,
    AgentRevoked = 23,
    AgentRegisteredDenied = 27,
    AgentRevocationDenied = 28,
    SessionOpened = 24,
    SessionRevoked = 25,
    SessionExpired = 26,
    SessionRevokedDenied = 29,
    PermissionGranted = 30,
    PermissionRevoked = 31,
    PermissionDenied = 32,
    ApprovalRequested = 40,
    ApprovalGranted = 41,
    ApprovalDenied = 42,
    ApprovalCancelled = 43,
    ApprovalRevoked = 44,
    ApprovalConsumed = 45,
    ApprovalExpired = 46,
    ApprovalRejected = 47,
    ApprovalReplayDetected = 48,
    BindingMismatch = 49,
    PreflightDenied = 50,
    DispatchRefused = 51,
    DispatchSucceeded = 52,
    DispatchAuthorized = 53,
    EmergencyStop = 60,
    EmergencyStopCleared = 61,
    EmergencyStopDenied = 62,
    AuditIntegrityFailure = 70,
    AuditCheckpointMismatch = 71,
    AuditAppendFailure = 72,
    AuditRecovered = 73,
    VaultSecretStored = 80,
    VaultSecretRotated = 81,
    VaultSecretDeleted = 82,
    VaultOperationFailed = 83,
    PolicyVersionMismatch = 90
}

/// <summary>
/// Correlates an audit record with the entity it concerns. Only opaque ids and
/// fixed enums are permitted; no free text or secret material.
/// </summary>
public sealed record AuditSubject(Guid CorrelationId, Guid SubjectId, Guid SecondaryId)
{
    public static AuditSubject For(Guid correlationId) => new(correlationId, Guid.Empty, Guid.Empty);

    public static AuditSubject For(Guid correlationId, Guid sessionId, Guid secondary = default) =>
        new(correlationId, sessionId, secondary);
}

/// <summary>A single immutable audit record as exposed to readers.</summary>
public sealed record AuditRecordView(
    long Sequence,
    DateTimeOffset RecordedAtUtc,
    DurableAuditKind Kind,
    Guid CorrelationId,
    Guid SubjectId,
    Guid SecondaryId,
    string DetailCode,
    byte[] PreviousHash,
    byte[] RecordHash);

/// <summary>Result of a full chain verification against the protected checkpoint.</summary>
public sealed record AuditVerificationResult(
    bool IsHealthy,
    long LastSequence,
    string FailureCode,
    string Diagnostic)
{
    public static AuditVerificationResult Healthy(long lastSequence) =>
        new(true, lastSequence, string.Empty, "Chain verified against the protected checkpoint.");

    public static AuditVerificationResult Failed(string failureCode, string diagnostic) =>
        new(false, 0, failureCode, diagnostic);
}

/// <summary>
/// Durable, append-only, hash-chained audit sink. Every authorizing operation must
/// durably append before it takes effect; a failed append is a denial, not a warning.
/// </summary>
public interface IDurableAuditLedger
{
    /// <summary>
    /// Appends one record. Returns false when the ledger is unavailable, unhealthy, or
    /// the append itself failed. Callers must treat false as a denial.
    /// </summary>
    bool TryAppend(DurableAuditKind kind, AuditSubject subject, string detailCode);

    /// <summary>Full integrity verification against the independently protected checkpoint.</summary>
    AuditVerificationResult VerifyIntegrity();

    /// <summary>Reads the most recent records, newest last. Never contains secret material.</summary>
    IReadOnlyList<AuditRecordView> ReadRecent(int maxRecords);

    /// <summary>True until the first integrity or append failure latches the ledger unavailable.</summary>
    bool IsHealthy { get; }

    long LastVerifiedSequence { get; }

    /// <summary>
    /// Explicit, Owner-initiated recovery. Re-verifies and re-anchors the checkpoint only
    /// after the existing chain verifies; never repairs or truncates silently.
    /// </summary>
    AuditVerificationResult AttemptRecovery();
}

/// <summary>
/// Independently protected anchor holding the last verified chain head. Implementations
/// must be tamper-resistant relative to the ledger file; a missing or unreadable anchor
/// must report false rather than silently initializing.
/// </summary>
public interface IAuditCheckpointStore
{
    bool TryRead(out long sequence, out byte[] headHash);

    bool TryWrite(long sequence, ReadOnlySpan<byte> headHash);
}
