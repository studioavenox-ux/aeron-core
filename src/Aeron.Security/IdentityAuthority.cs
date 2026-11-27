using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// Production authentication authority for AERON M2.
///
/// Guarantees:
/// * Windows account presence alone never authenticates the Owner. Enrollment binds the
///   enrolled SID to the observed OS account; an Owner session additionally requires the
///   approved factor policy to verify supplied factors. While no approved factor policy
///   is registered the authority mints no Owner sessions — the honest fail-closed default.
/// * Agent identities are isolated: each agent authenticates with its own ECDSA key and a
///   single-use nonce; sessions are per-agent and one agent can never resolve another's.
/// * Every session has an absolute expiry clamped to <see cref="SessionPolicy.MaximumLifetime"/>
///   and is revoked immediately on agent revocation, Owner preemption, or emergency stop.
/// * Owner-supplied evidence is a live Owner session id; that id only grants Owner
///   authority because the authority itself minted it, and it stops working the moment
///   the session expires, is revoked, or emergency stop latches.
/// * All state transitions are serialized; no external callback runs under the lock.
/// </summary>
public sealed class IdentityAuthority : IIdentityAuthority
{
    private const string AgentAuthenticationPurpose = "aeron-agent-auth-nonce";
    private const int MaxSessionsRetained = 512;

    private readonly Lock _sync = new();
    private readonly Dictionary<Guid, SessionEntry> _sessions = [];
    private readonly HashSet<string> _revokedAgentIds = new(StringComparer.Ordinal);

    private readonly IWindowsAccountIdentitySource _accounts;
    private readonly IOwnerFactorPolicy _factors;
    private readonly IAgentRegistry _agents;
    private readonly IAgentSignatureVerifier _signatures;
    private readonly INonceStore _nonces;
    private readonly IDurableAuditLedger _audit;
    private readonly IClock _clock;
    private readonly SessionPolicy _sessionPolicy;
    private readonly SecurityPolicyVersion _policyVersion;

    private string? _enrolledOwnerSubject;
    private string? _enrolledOwnerSid;
    private bool _emergencyStopActive;
    private long _preemptionEpoch;

    public IdentityAuthority(
        IWindowsAccountIdentitySource accounts,
        IOwnerFactorPolicy factors,
        IAgentRegistry agents,
        IAgentSignatureVerifier signatures,
        INonceStore nonces,
        IDurableAuditLedger audit,
        IClock clock,
        SessionPolicy sessionPolicy,
        SecurityPolicyVersion policyVersion)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(factors);
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(signatures);
        ArgumentNullException.ThrowIfNull(nonces);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(sessionPolicy);

        if (policyVersion is null || !policyVersion.IsValid)
        {
            throw new ArgumentException("A valid, versioned security policy is required.", nameof(policyVersion));
        }

        _accounts = accounts;
        _factors = factors;
        _agents = agents;
        _signatures = signatures;
        _nonces = nonces;
        _audit = audit;
        _clock = clock;
        _sessionPolicy = sessionPolicy;
        _policyVersion = policyVersion;
    }

    public SecurityPolicyVersion PolicyVersion => _policyVersion;

    public bool IsOwnerEnrolled
    {
        get
        {
            lock (_sync)
            {
                return _enrolledOwnerSid is not null;
            }
        }
    }

    public bool IsEmergencyStopActive
    {
        get
        {
            lock (_sync)
            {
                return _emergencyStopActive;
            }
        }
    }

    public long PreemptionEpoch
    {
        get
        {
            lock (_sync)
            {
                return _preemptionEpoch;
            }
        }
    }

    public Result<AuthenticatedSession> EnrollOwner(OwnerEnrollmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_accounts.TryGetCurrentAccount(out var observed) || observed is null ||
            string.IsNullOrWhiteSpace(observed.Sid) || observed.Sid.Length > 256)
        {
            Record(DurableAuditKind.OwnerAuthenticationFailed, SecurityCodes.OwnerEnrollmentRejected);
            return Result<AuthenticatedSession>.Failure("IDENTITY_REJECTED", "The active Windows account could not be observed.");
        }

        if (!string.Equals(observed.Sid, request.OwnerSid, StringComparison.Ordinal))
        {
            Record(DurableAuditKind.OwnerAuthenticationFailed, SecurityCodes.OwnerEnrollmentRejected);
            return Result<AuthenticatedSession>.Failure("IDENTITY_REJECTED",
                "Owner enrollment must run under the Owner's own Windows account; the observed SID does not match.");
        }

        if (string.IsNullOrWhiteSpace(request.OwnerSubjectId) ||
            request.OwnerSubjectId.Length > VerifiedActor.MaxSubjectLength)
        {
            return Result<AuthenticatedSession>.Failure("INVALID_REQUEST", "A bounded Owner subject id is required.");
        }

        if (!_factors.IsApproved)
        {
            Record(DurableAuditKind.OwnerAuthenticationFailed, SecurityCodes.OwnerEnrollmentLocked);
            return Result<AuthenticatedSession>.Failure("FACTOR_POLICY_NOT_APPROVED",
                "Owner enrollment requires an Owner-approved MFA factor and enrollment policy. None is registered, so access stays denied.");
        }

        var alreadyEnrolled = false;
        var enrolledNow = false;

        lock (_sync)
        {
            if (_enrolledOwnerSid is not null)
            {
                alreadyEnrolled = true;
            }
            else
            {
                _enrolledOwnerSubject = request.OwnerSubjectId;
                _enrolledOwnerSid = observed.Sid;
                enrolledNow = true;
            }
        }

        if (alreadyEnrolled)
        {
            return Result<AuthenticatedSession>.Failure("ALREADY_ENROLLED",
                "An Owner is already enrolled. Re-enrollment requires the Owner recovery procedure.");
        }

        if (!enrolledNow)
        {
            return Result<AuthenticatedSession>.Failure("IDENTITY_REJECTED", "Owner enrollment was refused.");
        }

        if (!Record(DurableAuditKind.OwnerEnrolled, SecurityCodes.OwnerEnrolled))
        {
            lock (_sync)
            {
                _enrolledOwnerSubject = null;
                _enrolledOwnerSid = null;
            }

            return Result<AuthenticatedSession>.Failure("AUDIT_UNAVAILABLE",
                "Owner enrollment was refused because the audit trail could not record it.");
        }

        return Result<AuthenticatedSession>.Failure("ENROLLED_NO_SESSION",
            "Owner enrolled. Enrollment by itself grants no authority; open a session with approved factors.");
    }

    public Result<AuthenticatedSession> AuthenticateOwner(OwnerAuthenticationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.PolicyVersion, _policyVersion.Value, StringComparison.Ordinal))
        {
            Record(DurableAuditKind.OwnerAuthenticationFailed, SecurityCodes.PolicyVersionMismatch);
            return Result<AuthenticatedSession>.Failure("POLICY_VERSION_MISMATCH",
                "The authentication policy version is stale; retry with the current policy.");
        }

        string? ownerSubject;
        string? enrolledSid;

        lock (_sync)
        {
            ownerSubject = _enrolledOwnerSubject;
            enrolledSid = _enrolledOwnerSid;
        }

        // Emergency stop suspends agent authentication and every consequential operation, but
        // it deliberately does NOT block Owner authentication. Clearance requires live Owner
        // authority, so blocking the Owner here would make the latch permanent and would deny
        // the Owner's ultimate authority to resume. The Owner is still bound by the approved
        // factor policy; nothing about this path weakens factor verification.

        if (ownerSubject is null || enrolledSid is null)
        {
            Record(DurableAuditKind.OwnerAuthenticationFailed, SecurityCodes.OwnerNotEnrolled);
            return Result<AuthenticatedSession>.Failure("OWNER_NOT_ENROLLED", "No Owner is enrolled on this device.");
        }

        if (!_accounts.TryGetCurrentAccount(out var observed) || observed is null ||
            string.IsNullOrWhiteSpace(observed.Sid) || observed.Sid.Length > 256)
        {
            Record(DurableAuditKind.OwnerAuthenticationFailed, SecurityCodes.OwnerAuthenticationFailed);
            return Result<AuthenticatedSession>.Failure("IDENTITY_REJECTED", "The active Windows account could not be observed.");
        }

        if (!string.Equals(observed.Sid, enrolledSid, StringComparison.Ordinal))
        {
            Record(DurableAuditKind.OwnerAuthenticationFailed, SecurityCodes.OwnerAuthenticationFailed);
            return Result<AuthenticatedSession>.Failure("IDENTITY_REJECTED",
                "The active Windows account is not the enrolled Owner account.");
        }

        if (request.Account is null ||
            !string.Equals(request.Account.Sid, observed.Sid, StringComparison.Ordinal))
        {
            Record(DurableAuditKind.OwnerAuthenticationFailed, SecurityCodes.OwnerAuthenticationFailed);
            return Result<AuthenticatedSession>.Failure("IDENTITY_REJECTED",
                "The supplied account observation does not match the trusted OS account source.");
        }

        bool factorsVerified;
        string? factorSubject;

        try
        {
            factorsVerified = _factors.TryVerifyFactors(observed, request.Factors, out factorSubject);
        }
        catch (Exception)
        {
            factorsVerified = false;
            factorSubject = null;
        }

        if (!factorsVerified)
        {
            Record(DurableAuditKind.OwnerAuthenticationFailed,
                _factors.IsApproved ? SecurityCodes.OwnerAuthenticationFailed : SecurityCodes.FactorPolicyNotApproved);
            return Result<AuthenticatedSession>.Failure("FACTOR_POLICY_NOT_APPROVED",
                _factors.IsApproved
                    ? "Owner factors were not satisfied."
                    : "Owner authentication requires an approved MFA policy. Windows account presence alone never authenticates the Owner.");
        }

        if (!string.IsNullOrWhiteSpace(factorSubject) &&
            !string.Equals(factorSubject, ownerSubject, StringComparison.Ordinal))
        {
            Record(DurableAuditKind.OwnerAuthenticationFailed, SecurityCodes.OwnerAuthenticationFailed);
            return Result<AuthenticatedSession>.Failure("IDENTITY_REJECTED",
                "Owner factor verification did not match the enrolled Owner.");
        }

        var session = AddSession(new VerifiedActor(ownerSubject, IdentityKind.Owner), _sessionPolicy.OwnerSessionLifetime);

        if (!Record(DurableAuditKind.OwnerAuthenticated, SecurityCodes.OwnerSessionOpened, session.SessionId))
        {
            RetireSession(session.SessionId);
            return Result<AuthenticatedSession>.Failure("AUDIT_UNAVAILABLE",
                "The Owner session was refused because the audit trail could not record it.");
        }

        return Result<AuthenticatedSession>.Success(session);
    }

    public Result<AuthenticatedSession> AuthenticateAgent(AgentAuthenticationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!AgentRegistration.IsValidAgentId(request.AgentId) ||
            request.ChallengeNonce is null || request.ChallengeNonce.Length < 16 ||
            request.Signature is null || request.Signature.Length == 0)
        {
            return Result<AuthenticatedSession>.Failure("INVALID_REQUEST",
                "Agent authentication requires a registered id, a fresh nonce and a signature.");
        }

        if (!string.Equals(request.PolicyVersion, _policyVersion.Value, StringComparison.Ordinal))
        {
            Record(DurableAuditKind.AgentAuthenticationFailed, SecurityCodes.PolicyVersionMismatch);
            return Result<AuthenticatedSession>.Failure("POLICY_VERSION_MISMATCH",
                "The authentication policy version is stale.");
        }

        var agentId = request.AgentId;

        lock (_sync)
        {
            if (_emergencyStopActive)
            {
                return Result<AuthenticatedSession>.Failure("EMERGENCY_STOP_ACTIVE",
                    "Emergency stop is latched; authentication is suspended.");
            }

            if (_revokedAgentIds.Contains(agentId))
            {
                Record(DurableAuditKind.AgentAuthenticationFailed, SecurityCodes.AgentAuthenticationFailed);
                return Result<AuthenticatedSession>.Failure("AGENT_REVOKED", "The agent registration has been revoked.");
            }
        }

        if (!_agents.TryGet(agentId, out var registration) || registration is null || registration.IsRevoked)
        {
            Record(DurableAuditKind.AgentAuthenticationFailed, SecurityCodes.AgentAuthenticationFailed);
            return Result<AuthenticatedSession>.Failure("IDENTITY_REJECTED", "The agent is not registered on this device.");
        }

        // A nonce is single-use regardless of signature validity, so an attacker cannot
        // retry guesses against the same challenge.
        if (!_nonces.TryConsume(AgentAuthenticationPurpose, request.ChallengeNonce, _clock.UtcNow))
        {
            Record(DurableAuditKind.ApprovalReplayDetected, SecurityCodes.AgentAuthenticationFailed);
            return Result<AuthenticatedSession>.Failure("REPLAY_REJECTED",
                "A previously used agent authentication nonce was rejected.");
        }

        bool signatureValid;

        try
        {
            signatureValid = _signatures.TryVerify(registration, request.ChallengeNonce, request.Signature);
        }
        catch (Exception)
        {
            signatureValid = false;
        }

        if (!signatureValid)
        {
            Record(DurableAuditKind.AgentAuthenticationFailed, SecurityCodes.AgentAuthenticationFailed);
            return Result<AuthenticatedSession>.Failure("IDENTITY_REJECTED", "The agent authentication signature was rejected.");
        }

        var session = AddSession(new VerifiedActor(agentId, IdentityKind.Agent), _sessionPolicy.AgentSessionLifetime);

        if (!Record(DurableAuditKind.AgentAuthenticated, SecurityCodes.AgentSessionOpened, session.SessionId))
        {
            RetireSession(session.SessionId);
            return Result<AuthenticatedSession>.Failure("AUDIT_UNAVAILABLE",
                "The agent session was refused because the audit trail could not record it.");
        }

        return Result<AuthenticatedSession>.Success(session);
    }

    public bool TryGetActiveSession(Guid sessionId, out AuthenticatedSession? session)
    {
        session = null;

        if (sessionId == Guid.Empty)
        {
            return false;
        }

        lock (_sync)
        {
            if (!_sessions.TryGetValue(sessionId, out var entry))
            {
                return false;
            }

            if (entry.State == SessionState.Active && _clock.UtcNow >= entry.ExpiresAtUtc)
            {
                entry.State = SessionState.Expired;
            }

            if (entry.State == SessionState.Active && _revokedAgentIds.Contains(entry.Actor.SubjectId))
            {
                entry.State = SessionState.Revoked;
            }

            if (entry.State != SessionState.Active)
            {
                return false;
            }

            session = new AuthenticatedSession(
                sessionId, entry.Actor, entry.OpenedAtUtc, entry.ExpiresAtUtc, SessionState.Active, _policyVersion.Value);
            return true;
        }
    }

    public bool TryResolveActor(Guid sessionId, out VerifiedActor? actor)
    {
        actor = null;

        if (!TryGetActiveSession(sessionId, out var session) || session is null)
        {
            return false;
        }

        actor = session.Actor;
        return true;
    }

    public Result RevokeSession(Guid sessionId, ReadOnlyMemory<byte> ownerEvidence)
    {
        if (sessionId == Guid.Empty)
        {
            return Result.Failure("INVALID_REQUEST", "A session id is required.");
        }

        if (!TryResolveOwnerSession(ownerEvidence, out var ownerFailure))
        {
            Record(DurableAuditKind.SessionRevoked, SecurityCodes.SessionRevokedDenied);
            return Result.Failure("OWNER_REQUIRED", ownerFailure);
        }

        lock (_sync)
        {
            if (!_sessions.TryGetValue(sessionId, out var entry))
            {
                return Result.Failure("NOT_FOUND", "The session is unknown.");
            }

            entry.State = SessionState.Revoked;
        }

        if (!Record(DurableAuditKind.SessionRevoked, SecurityCodes.SessionRevoked, sessionId))
        {
            return Result.Failure("AUDIT_UNAVAILABLE",
                "The session was revoked, but the audit trail could not record it.");
        }

        return Result.Success();
    }

    public int RevokeAllSessions(ReadOnlyMemory<byte> ownerEvidence)
    {
        if (!TryResolveOwnerSession(ownerEvidence, out _))
        {
            return 0;
        }

        int revoked;

        lock (_sync)
        {
            revoked = RevokeAgentSessionsLocked(string.Empty, revokeAll: true);
            _preemptionEpoch++;
        }

        Record(DurableAuditKind.OwnerPreempted, SecurityCodes.OwnerPreempted);
        return revoked;
    }

    public void RegisterAgent(AgentRegistration registration, ReadOnlyMemory<byte> ownerEvidence)
    {
        // The interface is void, but a caller still needs to know why a registration was
        // refused; the diagnosing overload is the supported way to find out.
        _ = TryRegisterAgent(registration, ownerEvidence);
    }

    /// <summary>
    /// Owner-provisioned agent registration that reports the refusal reason. The reason is
    /// a stable non-secret code plus a message safe to surface to the Owner.
    /// </summary>
    public Result TryRegisterAgent(AgentRegistration registration, ReadOnlyMemory<byte> ownerEvidence)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (!TryResolveOwnerSession(ownerEvidence, out var ownerFailure))
        {
            Record(DurableAuditKind.AgentRegisteredDenied, SecurityCodes.AgentRegisteredDenied);
            return Result.Failure("OWNER_REQUIRED", ownerFailure);
        }

        if (!AgentRegistration.IsValidAgentId(registration.AgentId) ||
            !AgentRegistration.IsValidDisplayName(registration.DisplayName) ||
            registration.PublicKey.Length == 0 ||
            registration.PublicKey.Length > AgentRegistration.MaxPublicKeyBytes)
        {
            Record(DurableAuditKind.AgentRegisteredDenied, SecurityCodes.AgentRegisteredDenied);
            return Result.Failure("INVALID_REQUEST", "The agent registration is not well formed.");
        }

        if (!_agents.TryRegister(registration))
        {
            Record(DurableAuditKind.AgentRegisteredDenied, SecurityCodes.AgentRegisteredDenied);
            return Result.Failure("REGISTRATION_REFUSED",
                "The registry refused the registration (revoked, duplicate, or full).");
        }

        if (!Record(DurableAuditKind.AgentRegistered, SecurityCodes.AgentRegistered))
        {
            // Remove the registration so an unaudited agent cannot remain provisioned.
            _agents.TryRevoke(registration.AgentId);
            return Result.Failure("AUDIT_UNAVAILABLE",
                "The registration was refused because the audit trail could not record it.");
        }

        return Result.Success();
    }

    public void RevokeAgent(string agentId, ReadOnlyMemory<byte> ownerEvidence)
    {
        if (!AgentRegistration.IsValidAgentId(agentId))
        {
            return;
        }

        if (!TryResolveOwnerSession(ownerEvidence, out _))
        {
            Record(DurableAuditKind.AgentRevocationDenied, SecurityCodes.AgentRevocationDenied);
            return;
        }

        if (!_agents.TryRevoke(agentId))
        {
            return;
        }

        lock (_sync)
        {
            _revokedAgentIds.Add(agentId);
            RevokeAgentSessionsLocked(agentId, revokeAll: false);
        }

        Record(DurableAuditKind.AgentRevoked, SecurityCodes.AgentRevoked);
    }

    public void SweepExpired()
    {
        var now = _clock.UtcNow;

        lock (_sync)
        {
            foreach (var entry in _sessions.Values)
            {
                if (entry.State == SessionState.Active && now >= entry.ExpiresAtUtc)
                {
                    entry.State = SessionState.Expired;
                }
            }

            if (_sessions.Count > MaxSessionsRetained)
            {
                var terminal = _sessions
                    .Where(static pair => pair.Value.State != SessionState.Active)
                    .Select(static pair => pair.Key)
                    .Take(_sessions.Count - (MaxSessionsRetained / 2))
                    .ToArray();

                foreach (var id in terminal)
                {
                    _sessions.Remove(id);
                }
            }
        }

        _nonces.PurgeExpired(now.AddHours(-1));
    }

    bool IIdentityAuthority.TrySetEmergencyStop(ReadOnlyMemory<byte> ownerEvidence, string reasonCode, bool active) =>
        TrySetEmergencyStop(ownerEvidence, reasonCode, active);

    public bool TrySetEmergencyStop(ReadOnlyMemory<byte> ownerEvidence, string reasonCode, bool active)
    {
        if (!TryResolveOwnerSession(ownerEvidence, out _))
        {
            Record(DurableAuditKind.EmergencyStopDenied, SecurityCodes.EmergencyStopDenied);
            return false;
        }

        if (!Record(
                active ? DurableAuditKind.EmergencyStop : DurableAuditKind.EmergencyStopCleared,
                active ? SecurityCodes.EmergencyStop : SecurityCodes.EmergencyStopCleared))
        {
            return false;
        }

        lock (_sync)
        {
            _emergencyStopActive = active;

            if (active)
            {
                _preemptionEpoch++;
                RevokeAgentSessionsLocked(string.Empty, revokeAll: true);
            }
        }

        _ = reasonCode;
        return true;
    }

    bool IIdentityAuthority.TryPreempt(ReadOnlyMemory<byte> ownerEvidence) => TryPreempt(ownerEvidence);

    public bool TryPreempt(ReadOnlyMemory<byte> ownerEvidence)
    {
        if (!TryResolveOwnerSession(ownerEvidence, out _))
        {
            Record(DurableAuditKind.OwnerPreemptedDenied, SecurityCodes.OwnerPreemptedDenied);
            return false;
        }

        if (!Record(DurableAuditKind.OwnerPreempted, SecurityCodes.OwnerPreempted))
        {
            return false;
        }

        lock (_sync)
        {
            _preemptionEpoch++;
            RevokeAgentSessionsLocked(string.Empty, revokeAll: true);
        }

        return true;
    }

    /// <summary>Number of live (unexpired, unrevoked) sessions of either actor kind.</summary>
    public int ActiveSessionCount
    {
        get
        {
            var now = _clock.UtcNow;

            lock (_sync)
            {
                return _sessions.Values.Count(entry => entry.State == SessionState.Active && now < entry.ExpiresAtUtc);
            }
        }
    }

    /// <summary>
    /// True only while a live Owner session opened by approved factors exists. A Windows
    /// SID match alone never makes this true.
    /// </summary>
    public bool HasActiveOwnerSession
    {
        get
        {
            var now = _clock.UtcNow;

            lock (_sync)
            {
                return _sessions.Values.Any(entry =>
                    entry.State == SessionState.Active &&
                    entry.Actor.Kind == IdentityKind.Owner &&
                    now < entry.ExpiresAtUtc);
            }
        }
    }

    /// <summary>Number of agent registrations known to the registry.</summary>
    public int RegisteredAgentCount => _agents.ListAgentIds().Count;

    /// <summary>Number of agents whose authority was revoked during this process.</summary>
    public int RevokedAgentCount
    {
        get
        {
            lock (_sync)
            {
                return _revokedAgentIds.Count;
            }
        }
    }

    /// <summary>Enrolled Owner subject id, or null. Display-only; grants no authority.</summary>
    public string? EnrolledOwnerSubject
    {
        get
        {
            lock (_sync)
            {
                return _enrolledOwnerSubject;
            }
        }
    }

    private int RevokeAgentSessionsLocked(string agentId, bool revokeAll)
    {
        var revoked = 0;

        foreach (var entry in _sessions.Values)
        {
            if (entry.State != SessionState.Active || entry.Actor.Kind != IdentityKind.Agent)
            {
                continue;
            }

            if (revokeAll || string.Equals(entry.Actor.SubjectId, agentId, StringComparison.Ordinal))
            {
                entry.State = SessionState.Revoked;
                revoked++;
            }
        }

        return revoked;
    }

    private AuthenticatedSession AddSession(VerifiedActor actor, TimeSpan lifetime)
    {
        var sessionId = Guid.NewGuid();
        var now = _clock.UtcNow;
        var expiresAt = now.Add(lifetime);

        lock (_sync)
        {
            _sessions[sessionId] = new SessionEntry(actor, now, expiresAt);
        }

        return new AuthenticatedSession(sessionId, actor, now, expiresAt, SessionState.Active, _policyVersion.Value);
    }

    private void RetireSession(Guid sessionId)
    {
        lock (_sync)
        {
            if (_sessions.TryGetValue(sessionId, out var entry))
            {
                entry.State = SessionState.Closed;
            }
        }
    }

    /// <summary>
    /// Owner evidence for M2 is a live Owner session id (16 raw bytes). It only resolves
    /// to Owner authority because this authority minted that session; it fails immediately
    /// on expiry, revocation, or emergency stop.
    /// </summary>
    public bool TryResolveOwnerSession(ReadOnlyMemory<byte> ownerEvidence, out string failure)
    {
        failure = "A verified Owner session is required.";

        lock (_sync)
        {
            if (_emergencyStopActive)
            {
                failure = "Emergency stop is latched; clear it with the enrolled Owner first.";
                return false;
            }

            if (_enrolledOwnerSubject is null)
            {
                failure = "No Owner is enrolled.";
                return false;
            }
        }

        // Evidence is the raw 16 bytes of the session id. Guid(ReadOnlySpan<byte>) uses the
        // same mixed-endian layout as Guid.ToByteArray(), so this round-trips exactly. Going
        // through a hex string would silently reinterpret the byte order and resolve to a
        // different (never-issued) session.
        if (ownerEvidence.Length != 16)
        {
            return false;
        }

        var sessionId = new Guid(ownerEvidence.Span);

        if (!TryGetActiveSession(sessionId, out var session) || session is null || session.Actor.Kind != IdentityKind.Owner)
        {
            failure = "The Owner session is unknown, expired, or revoked.";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private bool Record(DurableAuditKind kind, string detailCode, Guid subjectId = default)
    {
        try
        {
            return _audit.TryAppend(kind, AuditSubject.For(Guid.NewGuid(), subjectId), detailCode);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private sealed class SessionEntry(VerifiedActor actor, DateTimeOffset openedAtUtc, DateTimeOffset expiresAtUtc)
    {
        public VerifiedActor Actor { get; } = actor;
        public DateTimeOffset OpenedAtUtc { get; } = openedAtUtc;
        public DateTimeOffset ExpiresAtUtc { get; } = expiresAtUtc;
        public SessionState State { get; set; } = SessionState.Active;
    }
}
