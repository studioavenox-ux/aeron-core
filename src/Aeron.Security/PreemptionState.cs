using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// Single shared preemption/emergency-stop authority.
///
/// * Emergency stop latches: it blocks new authentication, approvals, and dispatch until
///   the enrolled Owner explicitly clears it. Latched state is honest about what it can
///   promise: an already-started external side effect cannot be rolled back, only recorded.
/// * Every preemption bumps a monotonic epoch. In-flight permits capture the epoch at
///   authorization time and refuse to execute if it changed, so a permit issued before
///   preemption cannot be redeemed after it.
/// * Setting or clearing requires a live Owner session and a durable audit append; both
///   fail closed.
/// </summary>
public sealed class PreemptionState : IPreemptionState
{
    private readonly Lock _sync = new();
    private readonly IDurableAuditLedger _audit;
    private readonly IdentityAuthority _identityAuthority;

    private bool _emergencyStop;
    private long _epoch;

    public PreemptionState(IDurableAuditLedger audit, IdentityAuthority identityAuthority)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(identityAuthority);
        _audit = audit;
        _identityAuthority = identityAuthority;
    }

    public bool IsEmergencyStopActive
    {
        get
        {
            lock (_sync)
            {
                return _emergencyStop;
            }
        }
    }

    public long PreemptionEpoch
    {
        get
        {
            lock (_sync)
            {
                return _epoch;
            }
        }
    }

    public bool TrySetEmergencyStop(ReadOnlyMemory<byte> ownerEvidence, string reasonCode)
    {
        if (!_identityAuthority.TryResolveOwnerSession(ownerEvidence, out _))
        {
            _audit.TryAppend(DurableAuditKind.EmergencyStopDenied, AuditSubject.For(Guid.NewGuid()), SecurityCodes.EmergencyStopDenied);
            return false;
        }

        lock (_sync)
        {
            if (_emergencyStop)
            {
                return false;
            }
        }

        if (!_audit.TryAppend(DurableAuditKind.EmergencyStop, AuditSubject.For(Guid.NewGuid()), SecurityCodes.EmergencyStop))
        {
            return false;
        }

        lock (_sync)
        {
            _emergencyStop = true;
            _epoch++;
        }

        _ = reasonCode;
        return true;
    }

    public bool TryClearEmergencyStop(ReadOnlyMemory<byte> ownerEvidence, string reasonCode)
    {
        if (!_identityAuthority.TryResolveOwnerSession(ownerEvidence, out _))
        {
            _audit.TryAppend(DurableAuditKind.EmergencyStopDenied, AuditSubject.For(Guid.NewGuid()), SecurityCodes.EmergencyStopDenied);
            return false;
        }

        lock (_sync)
        {
            if (!_emergencyStop)
            {
                return false;
            }
        }

        if (!_audit.TryAppend(DurableAuditKind.EmergencyStopCleared, AuditSubject.For(Guid.NewGuid()), SecurityCodes.EmergencyStopCleared))
        {
            return false;
        }

        lock (_sync)
        {
            _emergencyStop = false;
            _epoch++;
        }

        _ = reasonCode;
        return true;
    }

    public bool TryPreempt(ReadOnlyMemory<byte> ownerEvidence, string reasonCode)
    {
        if (!_identityAuthority.TryPreempt(ownerEvidence))
        {
            return false;
        }

        lock (_sync)
        {
            _epoch++;
        }

        _ = reasonCode;
        return true;
    }
}

/// <summary>
/// Permission administration facade. Grants require a live Owner session and a durable
/// audit append; nothing in this type can be invoked by an agent because every method
/// resolves Owner evidence through <see cref="IdentityAuthority"/>.
/// </summary>
public sealed class PermissionAdministration
{
    private readonly Lock _sync = new();
    private readonly IPermissionStore _store;
    private readonly IdentityAuthority _identityAuthority;
    private readonly IDurableAuditLedger _audit;

    public PermissionAdministration(IPermissionStore store, IdentityAuthority identityAuthority, IDurableAuditLedger audit)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(identityAuthority);
        ArgumentNullException.ThrowIfNull(audit);
        _store = store;
        _identityAuthority = identityAuthority;
        _audit = audit;
    }

    public Result Grant(ReadOnlyMemory<byte> ownerEvidence, string subjectId, IdentityKind kind, SecurityCapability capability)
    {
        if (!_identityAuthority.TryResolveOwnerSession(ownerEvidence, out var failure))
        {
            RecordDenied();
            return Result.Failure("OWNER_REQUIRED", failure);
        }

        if (kind == IdentityKind.Owner)
        {
            // The Owner does not need a capability grant to be the Owner, and granting
            // Owner-kind capabilities would let a caller manufacture Owner authority.
            return Result.Failure("INVALID_REQUEST", "Owner-kind capability grants are not permitted; Owner authority is intrinsic.");
        }

        if (kind != IdentityKind.Agent)
        {
            return Result.Failure("INVALID_REQUEST", "Only agent capabilities can be provisioned.");
        }

        lock (_sync)
        {
            if (!_store.TryGrant(subjectId, kind, capability))
            {
                return Result.Failure("STORE_UNAVAILABLE", "The permission store rejected the grant.");
            }
        }

        if (!_audit.TryAppend(DurableAuditKind.PermissionGranted, AuditSubject.For(Guid.NewGuid()), SecurityCodes.PermissionGranted))
        {
            // The grant already exists in memory; remove it so an unaudited grant cannot
            // remain, and report the failure honestly.
            _store.TryRevoke(subjectId, kind, capability);
            return Result.Failure("AUDIT_UNAVAILABLE", "The grant was not applied because the audit trail could not record it.");
        }

        return Result.Success();
    }

    public Result Revoke(ReadOnlyMemory<byte> ownerEvidence, string subjectId, IdentityKind kind, SecurityCapability capability)
    {
        if (!_identityAuthority.TryResolveOwnerSession(ownerEvidence, out var failure))
        {
            RecordDenied();
            return Result.Failure("OWNER_REQUIRED", failure);
        }

        lock (_sync)
        {
            _store.TryRevoke(subjectId, kind, capability);
        }

        if (!_audit.TryAppend(DurableAuditKind.PermissionRevoked, AuditSubject.For(Guid.NewGuid()), SecurityCodes.PermissionRevoked))
        {
            return Result.Failure("AUDIT_UNAVAILABLE", "The revocation applied, but the audit trail could not record it.");
        }

        return Result.Success();
    }

    public IReadOnlyList<SecurityCapability> ListGrants(string subjectId, IdentityKind kind) =>
        _store.ListGrants(subjectId, kind);

    private void RecordDenied() =>
        _audit.TryAppend(DurableAuditKind.PermissionDenied, AuditSubject.For(Guid.NewGuid()), SecurityCodes.PermissionDenied);
}
