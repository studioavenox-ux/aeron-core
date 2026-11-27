namespace Aeron.Contracts;

// AERON Milestone 2 — durable approval contracts.
// An approval is a single-use, expiring permission for one exact operation by one
// exact actor. Consuming an approval is NOT dispatch authority: the action boundary
// must re-preflight immediately before the side effect executes.

/// <summary>Durable storage record for one approval. The raw payload is never stored.</summary>
public sealed record ApprovalRecord(
    Guid ApprovalId,
    SecurityAction Action,
    DataSensitivity Sensitivity,
    string Target,
    string ActorSubject,
    IdentityKind ActorKind,
    string PolicyVersion,
    byte[] BindingHash,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    ApprovalLifecycleState State,
    Guid? ConsumedBySessionId);

/// <summary>Outcome codes for approval operations. Deny codes are stable and non-secret.</summary>
public enum ApprovalOutcomeCode
{
    Success = 0,
    NotFound = 1,
    AlreadyDecided = 2,
    Expired = 3,
    BindingMismatch = 4,
    ActorMismatch = 5,
    PolicyVersionMismatch = 6,
    AlreadyConsumed = 7,
    AuditUnavailable = 8,
    StoreUnavailable = 9,
    InvalidRequest = 10,
    OwnerRequired = 11,
    CapabilityNotGranted = 12,
    PermissionDenied = 13,
    EmergencyStopActive = 14,
    SessionInvalid = 15,
    PolicyNotApprovalRequired = 16,

    /// <summary>A concurrent decision changed the approval state before this transition.</summary>
    ConcurrencyConflict = 17
}

public sealed record ApprovalOutcome(ApprovalOutcomeCode Code, Guid ApprovalId)
{
    public bool IsSuccess => Code == ApprovalOutcomeCode.Success;

    public static ApprovalOutcome Success(Guid id) => new(ApprovalOutcomeCode.Success, id);

    public static ApprovalOutcome Failure(ApprovalOutcomeCode code, Guid id = default) => new(code, id);
}

/// <summary>
/// Durable approval store. Implementations must enforce single-use state transitions
/// atomically (compare-and-set), survive process restart, and never store raw payloads.
/// </summary>
public interface IApprovalStore
{
    bool TryInsert(ApprovalRecord record);

    bool TryGet(Guid approvalId, out ApprovalRecord? record);

    /// <summary>Atomically moves an entry from one state to another. False when the current state differs.</summary>
    bool TryTransition(Guid approvalId, ApprovalLifecycleState expected, ApprovalLifecycleState next);

    /// <summary>
    /// Atomically consumes an Approved entry exactly once, recording the consuming session.
    /// </summary>
    bool TryConsume(Guid approvalId, Guid consumerSessionId);

    IReadOnlyList<ApprovalRecord> ListPending(DateTimeOffset utcNow, int maxRecords);
}

/// <summary>
/// Computes and verifies the exact-operation binding. The binding must cover every
/// material parameter including the raw payload bytes, and must be deterministic so a
/// replayed or tampered operation fails verification even when displays are identical.
/// </summary>
public interface IOperationBinding
{
    /// <summary>Computes a binding for the approval id and exact operation inputs.</summary>
    byte[] Compute(
        Guid approvalId,
        SecurityAction action,
        string actorSubject,
        IdentityKind actorKind,
        string target,
        string policyVersion,
        ReadOnlySpan<byte> exactParameters);

    /// <summary>Constant-time verification of a previously computed binding.</summary>
    bool Verify(
        ReadOnlySpan<byte> expectedBinding,
        Guid approvalId,
        SecurityAction action,
        string actorSubject,
        IdentityKind actorKind,
        string target,
        string policyVersion,
        ReadOnlySpan<byte> exactParameters);
}
