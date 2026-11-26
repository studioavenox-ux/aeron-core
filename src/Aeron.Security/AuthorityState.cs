using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// Volatile authority foundation, not an action dispatcher or production grant store.
/// All methods serialize with each other; external dispatch and external identity changes
/// are not atomic with this lock. Restart loses grants and sessions (default deny).
/// Trusted provider and audit callbacks must not call back into this instance.
/// </summary>
public sealed class AuthorityState
{
    private readonly Lock _sync = new();
    private readonly OwnerAuthorityGuard _authority;
    private readonly ISecurityAuditSink _audit;
    private readonly HashSet<(VerifiedIdentity Identity, SecurityAction Action)> _grants = [];
    private readonly Dictionary<Guid, VerifiedIdentity> _sessions = [];
    private readonly HashSet<VerifiedIdentity> _revokedAgents = [];
    private bool _busy;
    private bool _unhealthy;

    public AuthorityState(ITrustedIdentityProvider identityProvider, ISecurityAuditSink audit)
    {
        ArgumentNullException.ThrowIfNull(identityProvider);
        ArgumentNullException.ThrowIfNull(audit);
        _authority = new OwnerAuthorityGuard(identityProvider);
        _audit = audit;
    }

    public bool Grant(ReadOnlyMemory<byte> ownerEvidence, VerifiedIdentity target, SecurityAction action)
    {
        lock (_sync)
        {
            if (_busy || _unhealthy || !Valid(target, action)) return false;
            _busy = true;
            try
            {
                if (!_authority.VerifyOwner(ownerEvidence) || _revokedAgents.Contains(target) ||
                    !Record(SecurityAuditKind.PermissionGranted)) return false;
                _grants.Add((target, action));
                return true;
            }
            finally { _busy = false; }
        }
    }

    public bool RevokePermission(ReadOnlyMemory<byte> ownerEvidence, VerifiedIdentity target, SecurityAction action)
    {
        lock (_sync)
        {
            if (_busy || !Valid(target, action)) return false;
            _busy = true;
            try
            {
                if (!_authority.VerifyOwner(ownerEvidence)) return false;
                _grants.Remove((target, action));
                return Record(SecurityAuditKind.PermissionRevoked);
            }
            finally { _busy = false; }
        }
    }

    public bool RevokeAgent(ReadOnlyMemory<byte> ownerEvidence, VerifiedIdentity agent)
    {
        lock (_sync)
        {
            if (_busy || !ValidIdentity(agent) || agent.Kind != IdentityKind.Agent) return false;
            _busy = true;
            try
            {
                if (!_authority.VerifyOwner(ownerEvidence)) return false;
                _revokedAgents.Add(agent);
                _grants.RemoveWhere(g => g.Identity == agent);
                return Record(SecurityAuditKind.AgentRevoked);
            }
            finally { _busy = false; }
        }
    }

    public bool RevokeSession(ReadOnlyMemory<byte> ownerEvidence, Guid sessionId)
    {
        lock (_sync)
        {
            if (_busy || sessionId == Guid.Empty) return false;
            _busy = true;
            try
            {
                if (!_authority.VerifyOwner(ownerEvidence)) return false;
                _sessions.Remove(sessionId);
                return Record(SecurityAuditKind.SessionRevoked);
            }
            finally { _busy = false; }
        }
    }

    public bool TryOpenSession(ReadOnlyMemory<byte> evidence, out Guid sessionId)
    {
        lock (_sync)
        {
            sessionId = Guid.Empty;
            if (_busy || _unhealthy) return false;
            _busy = true;
            try
            {
                if (!_authority.TryAuthenticate(evidence, out var identity, out _) ||
                    identity is null || _revokedAgents.Contains(identity)) return false;
                sessionId = Guid.NewGuid();
                _sessions.Add(sessionId, identity);
                return true;
            }
            finally { _busy = false; }
        }
    }

    // A positive result is only a snapshot, never authority for a side effect.
    public bool IsCurrentlyGranted(Guid sessionId, ReadOnlyMemory<byte> evidence, SecurityAction action)
    {
        lock (_sync)
        {
            if (_busy || _unhealthy || !Enum.IsDefined(action) || action == SecurityAction.Unknown) return false;
            _busy = true;
            try
            {
                return _sessions.TryGetValue(sessionId, out var sessionIdentity) &&
                    _authority.TryAuthenticate(evidence, out var identity, out _) && identity == sessionIdentity &&
                    !_revokedAgents.Contains(sessionIdentity) && _grants.Contains((sessionIdentity, action));
            }
            finally { _busy = false; }
        }
    }

    private bool Record(SecurityAuditKind kind)
    {
        if (_unhealthy) return false;
        try
        {
            if (_audit.TryAppend(kind, Guid.NewGuid())) return true;
        }
        catch (Exception) { }
        _unhealthy = true;
        return false;
    }

    private static bool Valid(VerifiedIdentity identity, SecurityAction action) =>
        ValidIdentity(identity) && Enum.IsDefined(action) && action != SecurityAction.Unknown;

    private static bool ValidIdentity(VerifiedIdentity? identity) => identity is not null &&
        !string.IsNullOrWhiteSpace(identity.SubjectId) && identity.SubjectId.Length <= 256 &&
        identity.Kind is IdentityKind.Owner or IdentityKind.Agent;
}
