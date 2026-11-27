using System.Security.Cryptography;
using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// Durable, single-use, exact-operation-bound approval engine.
///
/// Invariants enforced on every path:
/// * Only a registered approver trust marker (held by the approved Owner UI) can approve
///   or deny; the requesting actor can never approve its own request.
/// * An approval binds the approval id, action, actor subject and kind, target, policy
///   version, and the raw payload bytes through a keyed HMAC. The payload is never stored.
/// * Consumption re-verifies the actor session identity, the policy version, the expiry,
///   the preemption state, the current permission grant, and the binding, then performs
///   one atomic compare-and-set Approved→Consumed. Concurrency therefore yields exactly
///   one winner.
/// * Every state change durably appends to the audit ledger first. If audit is unavailable
///   the operation is refused rather than silently proceeding.
/// * A consumed approval is <em>not</em> dispatch authority on its own: the action
///   boundary must still pass <see cref="ActionDispatcher"/>'s immediate revalidation.
/// </summary>
public sealed class ApprovalEngine
{
    public const int MaxPayloadBytes = 64 * 1024;

    public static readonly TimeSpan MinimumLifetime = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan MaximumLifetime = TimeSpan.FromMinutes(30);

    private readonly IApprovalStore _store;
    private readonly IOperationBinding _binding;
    private readonly IdentityAuthority _identityAuthority;
    private readonly IPermissionStore _permissions;
    private readonly IDurableAuditLedger _audit;
    private readonly IPreemptionState _preemption;
    private readonly ApproverTrustRegistry _approvers;
    private readonly IClock _clock;
    private readonly SecurityPolicyEvaluator _policy;

    public ApprovalEngine(
        IApprovalStore store,
        IOperationBinding binding,
        IdentityAuthority identityAuthority,
        IPermissionStore permissions,
        IDurableAuditLedger audit,
        IPreemptionState preemption,
        ApproverTrustRegistry approvers,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(identityAuthority);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(preemption);
        ArgumentNullException.ThrowIfNull(approvers);
        ArgumentNullException.ThrowIfNull(clock);

        _store = store;
        _binding = binding;
        _identityAuthority = identityAuthority;
        _permissions = permissions;
        _audit = audit;
        _preemption = preemption;
        _approvers = approvers;
        _clock = clock;
        _policy = new SecurityPolicyEvaluator(permissions);
    }

    /// <summary>
    /// A requesting agent (or the Owner) asks for approval of one exact operation.
    /// Returns the approval id on success. The exact payload is bound, never stored.
    /// </summary>
    public ApprovalOutcome Request(
        Guid actorSessionId,
        OperationIdentity operation,
        ReadOnlyMemory<byte> exactParameters,
        TimeSpan lifetime)
    {
        if (exactParameters.Length > MaxPayloadBytes)
        {
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.InvalidRequest);
        }

        if (!_identityAuthority.TryResolveActor(actorSessionId, out var actor) || actor is null)
        {
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.SessionInvalid);
        }

        if (_preemption.IsEmergencyStopActive)
        {
            Record(DurableAuditKind.ApprovalRejected, SecurityCodes.ApprovalRejected);
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.EmergencyStopActive);
        }

        if (!_audit.IsHealthy)
        {
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.AuditUnavailable);
        }

        var evaluation = _policy.Evaluate(actor, operation, _identityAuthority.PolicyVersion);

        if (evaluation.Disposition == PolicyDisposition.Deny)
        {
            Record(DurableAuditKind.ApprovalRejected, SecurityCodes.ApprovalRejected);
            return ApprovalOutcome.Failure(MapDenial(evaluation.DenialReason));
        }

        if (evaluation.Disposition == PolicyDisposition.Allow)
        {
            // Asking for approval of an operation that does not require it is a policy bug
            // or an escalation attempt; refuse rather than creating approval surface.
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.PolicyNotApprovalRequired);
        }

        var clampedLifetime = ClampLifetime(lifetime);
        var approvalId = Guid.NewGuid();
        byte[] bindingHash;

        try
        {
            bindingHash = _binding.Compute(
                approvalId,
                operation.Action,
                actor.SubjectId,
                actor.Kind,
                operation.Target,
                operation.PolicyVersion,
                exactParameters.Span);
        }
        catch (Exception)
        {
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.InvalidRequest);
        }

        var createdAt = _clock.UtcNow;
        var record = new ApprovalRecord(
            approvalId,
            operation.Action,
            operation.Sensitivity,
            operation.Target,
            actor.SubjectId,
            actor.Kind,
            operation.PolicyVersion,
            bindingHash,
            createdAt,
            createdAt.Add(clampedLifetime),
            ApprovalLifecycleState.Pending,
            ConsumedBySessionId: null);

        if (!_store.TryInsert(record))
        {
            CryptographicOperations.ZeroMemory(bindingHash);
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.StoreUnavailable);
        }

        if (!RecordWithId(DurableAuditKind.ApprovalRequested, SecurityCodes.ApprovalRequested, approvalId))
        {
            _store.TryTransition(approvalId, ApprovalLifecycleState.Pending, ApprovalLifecycleState.Revoked);
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.AuditUnavailable);
        }

        return ApprovalOutcome.Success(approvalId);
    }

    /// <summary>
    /// The Owner (through the trusted approver UI, proven by the trust marker) approves or
    /// denies a pending request. Never callable by the requesting agent.
    /// </summary>
    public ApprovalOutcome Decide(Guid approvalId, ReadOnlyMemory<byte> approverMarker, bool approve)
    {
        if (approvalId == Guid.Empty || !_approvers.IsRegisteredApprover(approverMarker.Span))
        {
            Record(DurableAuditKind.ApprovalRejected, SecurityCodes.ApprovalRejected);
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.OwnerRequired, approvalId);
        }

        if (!_store.TryGet(approvalId, out var record) || record is null)
        {
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.NotFound, approvalId);
        }

        if (record.State != ApprovalLifecycleState.Pending)
        {
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.AlreadyDecided, approvalId);
        }

        if (_clock.UtcNow >= record.ExpiresAtUtc)
        {
            _store.TryTransition(approvalId, ApprovalLifecycleState.Pending, ApprovalLifecycleState.Expired);
            Record(DurableAuditKind.ApprovalExpired, SecurityCodes.ApprovalExpired, approvalId);
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.Expired, approvalId);
        }

        if (!string.Equals(record.PolicyVersion, _identityAuthority.PolicyVersion.Value, StringComparison.Ordinal))
        {
            _store.TryTransition(approvalId, ApprovalLifecycleState.Pending, ApprovalLifecycleState.Revoked);
            Record(DurableAuditKind.PolicyVersionMismatch, SecurityCodes.PolicyVersionMismatch, approvalId);
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.PolicyVersionMismatch, approvalId);
        }

        if (_preemption.IsEmergencyStopActive)
        {
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.EmergencyStopActive, approvalId);
        }

        var next = approve ? ApprovalLifecycleState.Approved : ApprovalLifecycleState.Denied;

        if (!_store.TryTransition(approvalId, ApprovalLifecycleState.Pending, next))
        {
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.ConcurrencyConflict, approvalId);
        }

        if (!RecordWithId(
                approve ? DurableAuditKind.ApprovalGranted : DurableAuditKind.ApprovalDenied,
                approve ? SecurityCodes.ApprovalGranted : SecurityCodes.ApprovalDenied,
                approvalId))
        {
            // Withdraw the decision so an unaudited approval cannot exist.
            _store.TryTransition(approvalId, next, ApprovalLifecycleState.Revoked);
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.AuditUnavailable, approvalId);
        }

        return ApprovalOutcome.Success(approvalId);
    }

    /// <summary>Owner cancels or revokes a pending or approved request.</summary>
    public ApprovalOutcome Invalidate(Guid approvalId, ReadOnlyMemory<byte> approverMarker, bool revoked)
    {
        if (approvalId == Guid.Empty || !_approvers.IsRegisteredApprover(approverMarker.Span))
        {
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.OwnerRequired, approvalId);
        }

        if (!_store.TryGet(approvalId, out var record) || record is null)
        {
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.NotFound, approvalId);
        }

        var from = record.State;
        var to = revoked ? ApprovalLifecycleState.Revoked : ApprovalLifecycleState.Cancelled;

        if (from is not (ApprovalLifecycleState.Pending or ApprovalLifecycleState.Approved))
        {
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.AlreadyDecided, approvalId);
        }

        if (!_store.TryTransition(approvalId, from, to))
        {
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.ConcurrencyConflict, approvalId);
        }

        if (!RecordWithId(
                revoked ? DurableAuditKind.ApprovalRevoked : DurableAuditKind.ApprovalCancelled,
                revoked ? SecurityCodes.ApprovalRevoked : SecurityCodes.ApprovalCancelled,
                approvalId))
        {
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.AuditUnavailable, approvalId);
        }

        return ApprovalOutcome.Success(approvalId);
    }

    /// <summary>
    /// Redeems an approved request for one exact operation. This performs the full
    /// immediate revalidation and atomically consumes the approval exactly once.
    /// Returns the persisted approval record on success; a caller must still go through
    /// <see cref="ActionDispatcher"/> to execute the side effect.
    /// </summary>
    public ApprovalOutcome TryConsume(
        Guid approvalId,
        Guid actorSessionId,
        OperationIdentity operation,
        ReadOnlyMemory<byte> exactParameters)
    {
        if (approvalId == Guid.Empty || exactParameters.Length > MaxPayloadBytes)
        {
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.InvalidRequest, approvalId);
        }

        if (!_identityAuthority.TryResolveActor(actorSessionId, out var actor) || actor is null)
        {
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.SessionInvalid, approvalId);
        }

        if (_preemption.IsEmergencyStopActive)
        {
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.EmergencyStopActive, approvalId);
        }

        if (!_audit.IsHealthy)
        {
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.AuditUnavailable, approvalId);
        }

        if (!_store.TryGet(approvalId, out var record) || record is null)
        {
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.NotFound, approvalId);
        }

        if (record.State == ApprovalLifecycleState.Consumed)
        {
            Record(DurableAuditKind.ApprovalReplayDetected, SecurityCodes.ApprovalReplayed, approvalId);
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.AlreadyConsumed, approvalId);
        }

        if (record.State != ApprovalLifecycleState.Approved)
        {
            return ApprovalOutcome.Failure(
                record.State == ApprovalLifecycleState.Expired
                    ? ApprovalOutcomeCode.Expired
                    : ApprovalOutcomeCode.NotFound,
                approvalId);
        }

        if (_clock.UtcNow >= record.ExpiresAtUtc)
        {
            _store.TryTransition(approvalId, ApprovalLifecycleState.Approved, ApprovalLifecycleState.Expired);
            Record(DurableAuditKind.ApprovalExpired, SecurityCodes.ApprovalExpired, approvalId);
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.Expired, approvalId);
        }

        if (!string.Equals(record.PolicyVersion, _identityAuthority.PolicyVersion.Value, StringComparison.Ordinal) ||
            !string.Equals(record.PolicyVersion, operation.PolicyVersion, StringComparison.Ordinal))
        {
            Record(DurableAuditKind.PolicyVersionMismatch, SecurityCodes.PolicyVersionMismatch, approvalId);
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.PolicyVersionMismatch, approvalId);
        }

        if (!string.Equals(record.ActorSubject, actor.SubjectId, StringComparison.Ordinal) || record.ActorKind != actor.Kind)
        {
            Record(DurableAuditKind.ApprovalRejected, SecurityCodes.ApprovalRejected, approvalId);
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.ActorMismatch, approvalId);
        }

        if (record.Action != operation.Action ||
            record.Sensitivity != operation.Sensitivity ||
            !string.Equals(record.Target, operation.Target, StringComparison.Ordinal))
        {
            Record(DurableAuditKind.BindingMismatch, SecurityCodes.ApprovalBindingMismatch, approvalId);
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.BindingMismatch, approvalId);
        }

        bool bindingValid;

        try
        {
            bindingValid = _binding.Verify(
                record.BindingHash,
                approvalId,
                operation.Action,
                actor.SubjectId,
                actor.Kind,
                operation.Target,
                operation.PolicyVersion,
                exactParameters.Span);
        }
        catch (Exception)
        {
            bindingValid = false;
        }

        if (!bindingValid)
        {
            Record(DurableAuditKind.BindingMismatch, SecurityCodes.ApprovalBindingMismatch, approvalId);
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.BindingMismatch, approvalId);
        }

        // Re-evaluate current permission and policy state: a grant withdrawn after approval
        // must deny consumption even though the approval itself is still valid.
        var evaluation = _policy.Evaluate(actor, operation, _identityAuthority.PolicyVersion);

        if (evaluation.Disposition != PolicyDisposition.RequireOwnerApproval)
        {
            Record(DurableAuditKind.ApprovalRejected, SecurityCodes.ApprovalRejected, approvalId);
            return ApprovalOutcome.Failure(MapDenial(evaluation.DenialReason), approvalId);
        }

        if (!_store.TryConsume(approvalId, actorSessionId))
        {
            Record(DurableAuditKind.ApprovalRejected, SecurityCodes.ApprovalRejected, approvalId);
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.ConcurrencyConflict, approvalId);
        }

        if (!RecordWithId(DurableAuditKind.ApprovalConsumed, SecurityCodes.ApprovalConsumed, approvalId))
        {
            // The approval is consumed and cannot be re-deemed; report the audit failure.
            return ApprovalOutcome.Failure(ApprovalOutcomeCode.AuditUnavailable, approvalId);
        }

        return ApprovalOutcome.Success(approvalId);
    }

    public IReadOnlyList<ApprovalRecord> ListOutstanding(int maxRecords) =>
        _store.ListPending(_clock.UtcNow, maxRecords);

    public bool TryGet(Guid approvalId, out ApprovalRecord? record) => _store.TryGet(approvalId, out record);

    private static TimeSpan ClampLifetime(TimeSpan lifetime) =>
        lifetime < MinimumLifetime ? MinimumLifetime
        : lifetime > MaximumLifetime ? MaximumLifetime
        : lifetime;

    private static ApprovalOutcomeCode MapDenial(SecurityDenialReason reason) => reason switch
    {
        SecurityDenialReason.CapabilityNotGranted => ApprovalOutcomeCode.CapabilityNotGranted,
        SecurityDenialReason.IdentityKindNotPermitted or SecurityDenialReason.AgentRevoked => ApprovalOutcomeCode.PermissionDenied,
        SecurityDenialReason.EmergencyStopActive or SecurityDenialReason.OwnerPreempted => ApprovalOutcomeCode.EmergencyStopActive,
        SecurityDenialReason.AuditUnavailable or SecurityDenialReason.AuditIntegrityFailure => ApprovalOutcomeCode.AuditUnavailable,
        SecurityDenialReason.PolicyVersionMismatch => ApprovalOutcomeCode.PolicyVersionMismatch,
        SecurityDenialReason.SessionMissing or SecurityDenialReason.SessionExpired or SecurityDenialReason.SessionRevoked => ApprovalOutcomeCode.SessionInvalid,
        SecurityDenialReason.ApprovalRequired => ApprovalOutcomeCode.PolicyNotApprovalRequired,
        SecurityDenialReason.ForbiddenAction => ApprovalOutcomeCode.PermissionDenied,
        _ => ApprovalOutcomeCode.InvalidRequest
    };

    private bool Record(DurableAuditKind kind, string detailCode, Guid approvalId = default) =>
        RecordWithId(kind, detailCode, approvalId);

    private bool RecordWithId(DurableAuditKind kind, string detailCode, Guid approvalId)
    {
        try
        {
            return _audit.TryAppend(
                kind,
                AuditSubject.For(Guid.NewGuid(), approvalId),
                detailCode);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
