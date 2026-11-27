namespace Aeron.Contracts;

// AERON Milestone 2 — authority, permission and action-boundary contracts.
// Default deny: every authorizing call fails closed unless an explicit grant, an active
// session, a valid policy version, and a healthy audit ledger all hold.

/// <summary>
/// Durable permission grant store. Grants are provisioned and revoked by the Owner only;
/// an agent can never grant itself a capability or elevate to Owner.
/// </summary>
public interface IPermissionStore
{
    bool TryGrant(string subjectId, IdentityKind kind, SecurityCapability capability);

    bool TryRevoke(string subjectId, IdentityKind kind, SecurityCapability capability);

    bool IsGranted(string subjectId, IdentityKind kind, SecurityCapability capability);

    IReadOnlyList<SecurityCapability> ListGrants(string subjectId, IdentityKind kind);

    int RevokeAll(string subjectId, IdentityKind kind);
}

/// <summary>
/// Cross-component preemption and emergency-stop state, shared by composition so the
/// approval engine, permission evaluator, and action boundary observe one authority.
/// </summary>
public interface IPreemptionState
{
    bool IsEmergencyStopActive { get; }

    /// <summary>Monotonic counter bumped on every Owner preemption; used to invalidate in-flight work.</summary>
    long PreemptionEpoch { get; }

    bool TrySetEmergencyStop(ReadOnlyMemory<byte> ownerEvidence, string reasonCode);

    bool TryClearEmergencyStop(ReadOnlyMemory<byte> ownerEvidence, string reasonCode);

    /// <summary>Owner-initiated preemption: revokes pending consequential state and bumps the epoch.</summary>
    bool TryPreempt(ReadOnlyMemory<byte> ownerEvidence, string reasonCode);
}

/// <summary>
/// The single authorized path to a real side effect. Callers may only obtain an
/// <see cref="IDispatchPermit"/> after the kernel has authorized the exact operation;
/// the permit resolves once and must be redeemed before the effect executes.
/// </summary>
public interface IActionDispatcher
{
    /// <summary>
    /// Executes a non-consequential, policy-allowed operation without approval. The
    /// dispatcher itself re-verifies session, capability, policy version, audit health,
    /// and preemption state before invoking the callback.
    /// </summary>
    Result<T> ExecuteDirect<T>(Guid actorSessionId, OperationIdentity operation, Func<T> effect);

    /// <summary>Redeems a single-use approval and returns a permit, or a denial reason.</summary>
    Result<IDispatchPermit> AuthorizeApproved(
        Guid actorSessionId,
        Guid approvalId,
        OperationIdentity operation,
        ReadOnlyMemory<byte> exactParameters);

    /// <summary>Requests approval for a consequential operation. Returns the approval id.</summary>
    Result<Guid> RequestApproval(
        Guid actorSessionId,
        OperationIdentity operation,
        ReadOnlyMemory<byte> exactParameters,
        TimeSpan lifetime);
}

/// <summary>
/// One-shot dispatch authority. Obtain through <see cref="IActionDispatcher"/> only.
/// </summary>
public interface IDispatchPermit
{
    Guid ApprovalId { get; }

    Guid ActorSessionId { get; }

    /// <summary>
    /// Executes the effect once. Returns the callback result on success; a second call,
    /// an expired permit, or a failed immediate revalidation refuses without executing.
    /// </summary>
    Result<T> TryExecute<T>(Func<T> effect);
}

/// <summary>Snapshot of kernel health for the Owner UI. Honest, reason-coded, no secrets.</summary>
public sealed record SecurityStatusSnapshot(
    bool OwnerEnrolled,
    bool OwnerSessionActive,
    string OwnerSubjectDisplay,
    string PolicyVersion,
    SecuritySubsystemStatus AuditStatus,
    long AuditLastSequence,
    string AuditDiagnostic,
    SecuritySubsystemStatus ApprovalStatus,
    int PendingApprovals,
    SecuritySubsystemStatus VaultStatus,
    string VaultAlgorithmSummary,
    bool EmergencyStopActive,
    int ActiveSessionCount,
    int RegisteredAgentCount,
    int RevokedAgentCount,
    DateTimeOffset CapturedAtUtc);

/// <summary>Non-secret view of the audit trail shown in the Owner UI.</summary>
public sealed record SecurityAuditEntryView(
    long Sequence,
    DateTimeOffset RecordedAtUtc,
    string Kind,
    string DetailCode,
    string CorrelationDisplay);

/// <summary>
/// Deterministic outcome of M2 authority policy evaluation for one exact operation.
/// <see cref="DenialReason"/> is meaningful only when <see cref="Disposition"/> is Deny.
/// </summary>
public sealed record PolicyEvaluation(
    PolicyDisposition Disposition,
    SecurityDenialReason DenialReason,
    SecurityCapability RequiredCapability,
    LocalAuthorityTier Tier,
    GovernanceDomain Domains,
    bool RequiresApproval,
    string PolicyVersion,
    string RuleCode)
{
    public bool IsAllowedDirectly =>
        Disposition == PolicyDisposition.Allow && RequiredCapability != SecurityCapability.Unknown;

    public bool IsApprovalRequired => Disposition == PolicyDisposition.RequireOwnerApproval;

    public bool IsDenied => Disposition == PolicyDisposition.Deny;
}
