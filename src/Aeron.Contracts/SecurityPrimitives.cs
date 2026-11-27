namespace Aeron.Contracts;

// AERON Milestone 2 — security kernel primitives.
// Fixed, closed vocabularies only: no free text, credentials, or operation payloads.

/// <summary>
/// A single, explicitly enumerable authority capability. Capabilities are provisioned
/// by the Owner only; no agent may derive, inherit-by-default, or grant one.
/// </summary>
public enum SecurityCapability
{
    Unknown = 0,

    /// <summary>Read non-sensitive AERON internal application state (L0).</summary>
    ViewSecurityStatus = 1,

    /// <summary>Read the redacted security audit trail (L0/L1).</summary>
    ViewAuditTrail = 2,

    /// <summary>Request (never decide) consequential operations that require approval.</summary>
    RequestConsequentialApproval = 3,

    /// <summary>Perform a policy-permitted local read through a validated scope boundary (L1).</summary>
    ReadLocalData = 4,

    /// <summary>Stage local drafts only; never transmit (L1).</summary>
    StageLocalDraft = 5
}

/// <summary>Lifecycle state of a verified authentication session.</summary>
public enum SessionState
{
    Unknown = 0,
    Active = 1,
    Expired = 2,
    Revoked = 3,
    Closed = 4
}

/// <summary>Terminal or current state of a durable approval entry.</summary>
public enum ApprovalLifecycleState
{
    Unknown = 0,
    Pending = 1,
    Approved = 2,
    Denied = 3,
    Cancelled = 4,
    Revoked = 5,
    Consumed = 6,
    Expired = 7
}

/// <summary>Why an operation was denied, revoked, or refused by the kernel.</summary>
public enum SecurityDenialReason
{
    None = 0,
    SessionMissing = 1,
    SessionExpired = 2,
    SessionRevoked = 3,
    IdentityRejected = 4,
    IdentityKindNotPermitted = 5,
    CapabilityNotGranted = 6,
    AgentRevoked = 7,
    PolicyVersionMismatch = 8,
    AuditUnavailable = 9,
    AuditIntegrityFailure = 10,
    VaultUnavailable = 11,
    OwnerPreempted = 12,
    EmergencyStopActive = 13,
    OwnerNotEnrolled = 14,
    FactorPolicyNotApproved = 15,
    ApprovalRequired = 16,
    ApprovalMissing = 17,
    ApprovalExpired = 18,
    ApprovalRevoked = 19,
    ApprovalAlreadyConsumed = 20,
    ApprovalBindingMismatch = 21,
    ApprovalNotForActor = 22,
    ConcurrencyConflict = 23,
    InternalFailure = 24,
    InvalidRequest = 25,
    StoreUnavailable = 26,
    ForbiddenAction = 27,
    OwnerRequired = 28
}

/// <summary>Coarse health classification surfaced to the Owner with honest wording.</summary>
public enum SecuritySubsystemStatus
{
    Unknown = 0,
    Healthy = 1,
    Degraded = 2,
    Unavailable = 3,
    Tampered = 4
}

/// <summary>
/// The immutable, versioned identity of the active policy set. Any change to the
/// evaluation rules must produce a new version string; pending approvals bound to an
/// older version are invalidated on revalidation.
/// </summary>
public sealed record SecurityPolicyVersion(string Value)
{
    public const int MaxLength = 64;

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Value) &&
        Value.Length <= MaxLength &&
        Value.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');
}

/// <summary>
/// A verified identity together with the isolation domain it authenticated in.
/// <see cref="Kind"/> is Owner-only for the Owner account; every agent is a distinct
/// subject isolated by name and may never authenticate as another agent or as the Owner.
/// </summary>
public sealed record VerifiedActor(string SubjectId, IdentityKind Kind)
{
    public const int MaxSubjectLength = 256;

    public bool IsWellFormed =>
        !string.IsNullOrWhiteSpace(SubjectId) &&
        SubjectId.Length <= MaxSubjectLength &&
        Kind is IdentityKind.Owner or IdentityKind.Agent;

    public static VerifiedActor? From(VerifiedIdentity? identity) =>
        identity is null
            ? null
            : new VerifiedActor(identity.SubjectId, identity.Kind) is { } actor && actor.IsWellFormed
                ? actor
                : null;
}

/// <summary>An authenticated session. Session ids are opaque and never reused.</summary>
public sealed record AuthenticatedSession(
    Guid SessionId,
    VerifiedActor Actor,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    SessionState State,
    string PolicyVersion);

/// <summary>An approval request summary safe for display and audit (no raw parameters).</summary>
public sealed record ApprovalSummary(
    Guid ApprovalId,
    SecurityAction Action,
    DataSensitivity Sensitivity,
    string ActorSubject,
    IdentityKind ActorKind,
    string PolicyVersion,
    DateTimeOffset ExpiresAtUtc,
    ApprovalLifecycleState State,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Exact operation identity, minus payload bytes. The payload itself is bound by
/// <see cref="IOperationBinding"/> and is never persisted or displayed.
/// </summary>
public sealed record OperationIdentity(
    SecurityAction Action,
    DataSensitivity Sensitivity,
    string Target,
    string PolicyVersion)
{
    public const int MaxTargetLength = 512;

    public bool IsWellFormed =>
        Enum.IsDefined(Action) && Action != SecurityAction.Unknown &&
        Enum.IsDefined(Sensitivity) && Sensitivity != DataSensitivity.Unknown &&
        !string.IsNullOrWhiteSpace(Target) && Target.Length <= MaxTargetLength &&
        !string.IsNullOrWhiteSpace(PolicyVersion) && PolicyVersion.Length <= SecurityPolicyVersion.MaxLength;
}

/// <summary>Outcome of the pre-execution authority preflight at the action boundary.</summary>
public sealed record PreflightResult(bool IsAuthorized, SecurityDenialReason Reason)
{
    public static PreflightResult Allow => new(true, SecurityDenialReason.None);

    public static PreflightResult Deny(SecurityDenialReason reason) => new(false, reason);
}

/// <summary>Stable, non-secret codes used in audit details and operator-facing text.</summary>
public static class SecurityCodes
{
    public const string OwnerEnrolled = "owner.enrolled";
    public const string OwnerEnrollmentRejected = "owner.enroll_rejected";
    public const string OwnerEnrollmentLocked = "owner.enroll_locked";
    public const string OwnerSessionOpened = "owner.session_opened";
    public const string OwnerAuthenticated = "owner.authenticated";
    public const string OwnerAuthenticationFailed = "owner.auth_failed";
    public const string OwnerNotEnrolled = "owner.not_enrolled";
    public const string FactorPolicyNotApproved = "owner.factor_unapproved";
    public const string OwnerPreempted = "owner.preempted";
    public const string OwnerPreemptedDenied = "owner.preempt_deny";
    public const string AgentRegistered = "agent.registered";
    public const string AgentSessionOpened = "agent.session_open";
    public const string AgentAuthenticationFailed = "agent.auth_failed";
    public const string AgentRevoked = "agent.revoked";
    public const string AgentRegisteredDenied = "agent.registered_denied";
    public const string AgentRevocationDenied = "agent.revoke_deny";
    public const string SessionOpened = "session.opened";
    public const string SessionRevoked = "session.revoked";
    public const string SessionRevokedDenied = "session.revoke_deny";
    public const string SessionExpired = "session.expired";
    public const string PermissionGranted = "permission.granted";
    public const string PermissionRevoked = "permission.revoked";
    public const string PermissionDenied = "permission.denied";
    public const string ApprovalRequested = "approval.requested";
    public const string ApprovalGranted = "approval.granted";
    public const string ApprovalDenied = "approval.denied";
    public const string ApprovalCancelled = "approval.cancelled";
    public const string ApprovalRevoked = "approval.revoked";
    public const string ApprovalConsumed = "approval.consumed";
    public const string ApprovalExpired = "approval.expired";
    public const string ApprovalRejected = "approval.rejected";
    public const string ApprovalReplayed = "approval.replayed";
    public const string ApprovalBindingMismatch = "approval.bind_mismatch";
    public const string EmergencyStop = "emergency.stop";
    public const string EmergencyStopCleared = "emergency.cleared";
    public const string EmergencyStopDenied = "emergency.denied";
    public const string AuditIntegrityFailure = "audit.integrity_fail";
    public const string AuditAppendFailure = "audit.append_failure";
    public const string AuditRecovered = "audit.recovered";
    public const string AuditCheckpointMismatch = "audit.head_mismatch";
    public const string VaultOperationFailed = "vault.op_failed";
    public const string VaultSecretStored = "vault.secret_stored";
    public const string VaultSecretRotated = "vault.secret_rotated";
    public const string VaultSecretDeleted = "vault.secret_deleted";
    public const string PolicyVersionMismatch = "policy.version_bad";
    public const string StartupVerified = "startup.verified";
    public const string StartupRefused = "startup.refused";
    public const string KernelUnavailable = "kernel.unavailable";
}
