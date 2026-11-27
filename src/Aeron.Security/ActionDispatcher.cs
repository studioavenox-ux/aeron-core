using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// The single authorized gateway to a real side effect in AERON M2.
///
/// Both entry points re-verify, immediately before invoking the callback:
/// live actor session, policy version, current capability grant, durable audit health,
/// emergency-stop state, and preemption epoch. Approved operations additionally redeem a
/// single-use approval through <see cref="ApprovalEngine"/>. A permit captures the
/// preemption epoch and refuses to execute if the epoch moved, which closes the window
/// between "approval consumed" and "effect executed".
///
/// Honest limitation: an external effect already started when preemption arrives cannot
/// be rolled back. The permit refuses <em>new</em> executions; it does not promise
/// reversal of an in-flight side effect.
/// </summary>
public sealed class ActionDispatcher : IActionDispatcher
{
    private static readonly TimeSpan PermitLifetime = TimeSpan.FromSeconds(30);

    private readonly IdentityAuthority _identityAuthority;
    private readonly ApprovalEngine _approvals;
    private readonly SecurityPolicyEvaluator _policy;
    private readonly IDurableAuditLedger _audit;
    private readonly IPreemptionState _preemption;
    private readonly IClock _clock;

    public ActionDispatcher(
        IdentityAuthority identityAuthority,
        ApprovalEngine approvals,
        IPermissionStore permissions,
        IDurableAuditLedger audit,
        IPreemptionState preemption,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(identityAuthority);
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(preemption);
        ArgumentNullException.ThrowIfNull(clock);

        _identityAuthority = identityAuthority;
        _approvals = approvals;
        _policy = new SecurityPolicyEvaluator(permissions);
        _audit = audit;
        _preemption = preemption;
        _clock = clock;
    }

    public Result<T> ExecuteDirect<T>(Guid actorSessionId, OperationIdentity operation, Func<T> effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        var refusal = Preflight(actorSessionId, operation, allowApprovalRequired: false, out _);

        if (refusal is not null)
        {
            return Result<T>.Failure("SECURITY_DENIED", refusal);
        }

        return Execute(actorSessionId, operation, effect);
    }

    public Result<IDispatchPermit> AuthorizeApproved(
        Guid actorSessionId,
        Guid approvalId,
        OperationIdentity operation,
        ReadOnlyMemory<byte> exactParameters)
    {
        var refusal = Preflight(actorSessionId, operation, allowApprovalRequired: true, out _);

        if (refusal is not null)
        {
            Record(DurableAuditKind.PreflightDenied, SecurityCodes.PermissionDenied);
            return Result<IDispatchPermit>.Failure("SECURITY_DENIED", refusal);
        }

        var outcome = _approvals.TryConsume(approvalId, actorSessionId, operation, exactParameters);

        if (!outcome.IsSuccess)
        {
            Record(DurableAuditKind.PreflightDenied, SecurityCodes.ApprovalRejected);
            return Result<IDispatchPermit>.Failure("APPROVAL_DENIED", DescribeOutcome(outcome.Code));
        }

        var permit = new DispatchPermit(
            this,
            approvalId,
            actorSessionId,
            operation,
            _preemption.PreemptionEpoch,
            _clock.UtcNow.Add(PermitLifetime));

        return Result<IDispatchPermit>.Success(permit);
    }

    public Result<Guid> RequestApproval(
        Guid actorSessionId,
        OperationIdentity operation,
        ReadOnlyMemory<byte> exactParameters,
        TimeSpan lifetime)
    {
        var refusal = Preflight(actorSessionId, operation, allowApprovalRequired: true, out var evaluation);

        if (refusal is not null)
        {
            Record(DurableAuditKind.PreflightDenied, SecurityCodes.PermissionDenied);
            return Result<Guid>.Failure("SECURITY_DENIED", refusal);
        }

        if (evaluation is null || !evaluation.IsApprovalRequired)
        {
            return Result<Guid>.Failure("NOT_APPROVABLE", "This operation does not require Owner approval and cannot be queued as one.");
        }

        var outcome = _approvals.Request(actorSessionId, operation, exactParameters, lifetime);
        return outcome.IsSuccess
            ? Result<Guid>.Success(outcome.ApprovalId)
            : Result<Guid>.Failure("APPROVAL_REFUSED", DescribeOutcome(outcome.Code));
    }

    internal Result<bool> RedeemPermit(
        Guid approvalId,
        Guid actorSessionId,
        OperationIdentity operation,
        long capturedEpoch,
        DateTimeOffset expiresAtUtc)
    {
        if (_preemption.IsEmergencyStopActive)
        {
            Record(DurableAuditKind.DispatchRefused, SecurityCodes.EmergencyStop);
            return Result<bool>.Failure("SECURITY_DENIED", "Emergency stop is latched; the dispatch was refused.");
        }

        if (_preemption.PreemptionEpoch != capturedEpoch)
        {
            Record(DurableAuditKind.DispatchRefused, SecurityCodes.OwnerPreempted);
            return Result<bool>.Failure("SECURITY_DENIED", "The Owner preempted AERON after this permit was issued; the dispatch was refused.");
        }

        if (_clock.UtcNow >= expiresAtUtc)
        {
            Record(DurableAuditKind.DispatchRefused, SecurityCodes.ApprovalExpired);
            return Result<bool>.Failure("SECURITY_DENIED", "The dispatch permit expired before execution.");
        }

        if (!_audit.IsHealthy)
        {
            Record(DurableAuditKind.DispatchRefused, SecurityCodes.AuditIntegrityFailure);
            return Result<bool>.Failure("SECURITY_DENIED", "The audit trail is unavailable; the dispatch was refused.");
        }

        if (!_identityAuthority.TryResolveActor(actorSessionId, out var actor) || actor is null)
        {
            Record(DurableAuditKind.DispatchRefused, SecurityCodes.SessionRevoked);
            return Result<bool>.Failure("SECURITY_DENIED", "The actor session is expired or revoked.");
        }

        if (!_approvals.TryGet(approvalId, out var record) || record is null ||
            record.State != ApprovalLifecycleState.Consumed ||
            record.ConsumedBySessionId != actorSessionId ||
            record.Action != operation.Action ||
            !string.Equals(record.Target, operation.Target, StringComparison.Ordinal))
        {
            Record(DurableAuditKind.DispatchRefused, SecurityCodes.ApprovalBindingMismatch);
            return Result<bool>.Failure("SECURITY_DENIED", "The approval does not match this exact operation.");
        }

        return Result<bool>.Success(true);
    }

    internal void RecordDispatch(DurableAuditKind kind, Guid approvalId)
    {
        try
        {
            _audit.TryAppend(
                kind,
                AuditSubject.For(Guid.NewGuid(), approvalId),
                kind == DurableAuditKind.DispatchSucceeded ? SecurityCodes.ApprovalConsumed : SecurityCodes.ApprovalRejected);
        }
        catch (Exception)
        {
            // The append failure latches the ledger; callers already executed or refused.
        }
    }

    private Result<T> Execute<T>(Guid actorSessionId, OperationIdentity operation, Func<T> effect)
    {
        if (!_audit.TryAppend(
                DurableAuditKind.DispatchAuthorized,
                AuditSubject.For(Guid.NewGuid(), actorSessionId),
                SecurityCodes.PermissionGranted))
        {
            // Audit-before-effect: if the ledger cannot durably record the authorization,
            // the effect is refused. This is the fail-closed ordering guarantee.
            return Result<T>.Failure("SECURITY_DENIED", "The audit trail could not record the dispatch; it was refused.");
        }

        try
        {
            var value = effect();

            if (value is null)
            {
                return Result<T>.Failure("EFFECT_FAILED", "The operation produced no result.");
            }

            return Result<T>.Success(value);
        }
        catch (Exception)
        {
            return Result<T>.Failure("EFFECT_FAILED", "The authorized operation failed; it was not retried automatically.");
        }
    }

    /// <summary>Returns null when authorized; otherwise a non-secret refusal message.</summary>
    private string? Preflight(
        Guid actorSessionId,
        OperationIdentity operation,
        bool allowApprovalRequired,
        out PolicyEvaluation? evaluation)
    {
        evaluation = null;

        if (_preemption.IsEmergencyStopActive)
        {
            return "Emergency stop is latched; no operation may execute.";
        }

        if (!_audit.IsHealthy)
        {
            return "The durable audit trail is unavailable or failed verification; all operations are denied.";
        }

        if (!_identityAuthority.TryResolveActor(actorSessionId, out var actor) || actor is null)
        {
            return "The actor session is unknown, expired, or revoked.";
        }

        evaluation = _policy.Evaluate(actor, operation, _identityAuthority.PolicyVersion);

        if (evaluation.Disposition == PolicyDisposition.Deny)
        {
            return $"Denied by policy ({evaluation.RuleCode}).";
        }

        if (evaluation.Disposition == PolicyDisposition.RequireOwnerApproval && !allowApprovalRequired)
        {
            return "This operation requires explicit Owner approval and cannot be executed directly.";
        }

        return null;
    }

    private static string DescribeOutcome(ApprovalOutcomeCode code) => code switch
    {
        ApprovalOutcomeCode.AlreadyConsumed => "The approval was already consumed (replay refused).",
        ApprovalOutcomeCode.Expired => "The approval has expired.",
        ApprovalOutcomeCode.BindingMismatch => "The approval is bound to different operation parameters.",
        ApprovalOutcomeCode.ActorMismatch => "The approval belongs to a different actor.",
        ApprovalOutcomeCode.PolicyVersionMismatch => "The approval was issued under a different policy version.",
        ApprovalOutcomeCode.AuditUnavailable => "The audit trail is unavailable.",
        ApprovalOutcomeCode.StoreUnavailable => "The approval store is unavailable.",
        ApprovalOutcomeCode.SessionInvalid => "The actor session is invalid.",
        ApprovalOutcomeCode.EmergencyStopActive => "Emergency stop is latched.",
        ApprovalOutcomeCode.CapabilityNotGranted => "The actor no longer holds the required capability.",
        ApprovalOutcomeCode.PermissionDenied => "Policy denied the operation.",
        ApprovalOutcomeCode.ConcurrencyConflict => "A concurrent decision changed the approval state.",
        ApprovalOutcomeCode.OwnerRequired => "Only the Owner may decide approvals.",
        _ => "The approval could not be redeemed."
    };

    private void Record(DurableAuditKind kind, string detailCode)
    {
        try
        {
            _audit.TryAppend(kind, AuditSubject.For(Guid.NewGuid()), detailCode);
        }
        catch (Exception)
        {
            // Ledger latches on failure; no further action is possible here.
        }
    }

    /// <summary>One-shot dispatch authority issued after a successful approval redemption.</summary>
    private sealed class DispatchPermit : IDispatchPermit
    {
        private readonly ActionDispatcher _owner;
        private readonly OperationIdentity _operation;
        private readonly long _capturedEpoch;
        private readonly DateTimeOffset _expiresAtUtc;
        private int _redeemed;

        public DispatchPermit(
            ActionDispatcher owner,
            Guid approvalId,
            Guid actorSessionId,
            OperationIdentity operation,
            long capturedEpoch,
            DateTimeOffset expiresAtUtc)
        {
            _owner = owner;
            ApprovalId = approvalId;
            ActorSessionId = actorSessionId;
            _operation = operation;
            _capturedEpoch = capturedEpoch;
            _expiresAtUtc = expiresAtUtc;
        }

        public Guid ApprovalId { get; }

        public Guid ActorSessionId { get; }

        public Result<T> TryExecute<T>(Func<T> effect)
        {
            ArgumentNullException.ThrowIfNull(effect);

            if (Interlocked.CompareExchange(ref _redeemed, 1, 0) != 0)
            {
                return Result<T>.Failure("SECURITY_DENIED", "This dispatch permit was already redeemed.");
            }

            try
            {
                var redemption = _owner.RedeemPermit(ApprovalId, ActorSessionId, _operation, _capturedEpoch, _expiresAtUtc);

                if (redemption.IsFailure)
                {
                    return Result<T>.Failure("SECURITY_DENIED", redemption.ErrorMessage);
                }

                var value = effect();

                if (value is null)
                {
                    _owner.RecordDispatch(DurableAuditKind.DispatchRefused, ApprovalId);
                    return Result<T>.Failure("EFFECT_FAILED", "The operation produced no result.");
                }

                _owner.RecordDispatch(DurableAuditKind.DispatchSucceeded, ApprovalId);
                return Result<T>.Success(value);
            }
            catch (Exception)
            {
                _owner.RecordDispatch(DurableAuditKind.DispatchRefused, ApprovalId);
                return Result<T>.Failure("EFFECT_FAILED", "The authorized operation failed; it was not retried automatically.");
            }
        }
    }
}
