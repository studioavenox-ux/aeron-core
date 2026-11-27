namespace Aeron.Contracts;

// AERON Milestone 2 — identity, authentication and session contracts.
// Windows account presence alone must never authenticate the Owner. Every contract
// here is fail-closed: a missing, unreadable, or failing dependency is a denial.

/// <summary>A candidate Owner factor as evaluated by the approved factor policy.</summary>
public sealed class OwnerFactorEvidence
{
    public OwnerFactorEvidence(string factorId, ReadOnlyMemory<byte> evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(factorId);
        FactorId = factorId.Trim();
        Evidence = evidence.ToArray();
    }

    public string FactorId { get; }

    /// <summary>Raw factor material. Callers must zero it after use; it is never logged.</summary>
    public byte[] Evidence { get; }
}

/// <summary>
/// The Owner-approved factor and enrollment policy. M2 does not invent factors,
/// providers, enrollment, or recovery semantics: until an implementation is approved
/// and registered, MFA-dependent operations stay denied.
/// </summary>
public interface IOwnerFactorPolicy
{
    /// <summary>Stable policy identifier recorded in audit evidence.</summary>
    string PolicyId { get; }

    /// <summary>True only when the Owner has approved factors, enrollment and recovery.</summary>
    bool IsApproved { get; }

    /// <summary>
    /// Verifies the supplied factors for the given OS account observation. Returns true
    /// only when the approved factor set is satisfied. Implementations must ignore the
    /// account SID for authorization purposes unless that is the approved design.
    /// </summary>
    bool TryVerifyFactors(
        WindowsAccountIdentity account,
        IReadOnlyList<OwnerFactorEvidence> factors,
        out string? verifiedSubjectId);
}

/// <summary>
/// A previously enrolled agent. <see cref="PublicKey"/> is the agent's SubjectPublicKeyInfo
/// (ECDSA P-256 for the built-in verifier); private keys never leave the agent boundary.
/// </summary>
public sealed record AgentRegistration(
    string AgentId,
    string DisplayName,
    byte[] PublicKey,
    DateTimeOffset RegisteredAtUtc,
    bool IsRevoked)
{
    public const int MaxAgentIdLength = 64;
    public const int MaxDisplayNameLength = 128;
    public const int MaxPublicKeyBytes = 512;

    public static bool IsValidAgentId(string? agentId) =>
        !string.IsNullOrWhiteSpace(agentId) &&
        agentId.Length <= MaxAgentIdLength &&
        agentId.All(static c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    public static bool IsValidDisplayName(string? displayName) =>
        !string.IsNullOrWhiteSpace(displayName) && displayName.Length <= MaxDisplayNameLength;
}

/// <summary>
/// Durable registry of enrolled agents. Implementations must be tamper-resistant and
/// current-user protected. A missing agent or an unavailable store denies access.
/// </summary>
public interface IAgentRegistry
{
    bool TryGet(string agentId, out AgentRegistration? registration);

    bool TryRegister(AgentRegistration registration);

    bool TryRevoke(string agentId);

    IReadOnlyList<string> ListAgentIds();
}

/// <summary>
/// Verifies a fresh agent authentication nonce signed with the agent's private key.
/// Implementations must reject replays by calling <see cref="INonceStore"/> before
/// accepting a signature.
/// </summary>
public interface IAgentSignatureVerifier
{
    bool TryVerify(AgentRegistration registration, ReadOnlySpan<byte> challenge, ReadOnlySpan<byte> signature);
}

/// <summary>
/// Single-use nonce store used for replay protection. A nonce that was already seen,
/// or a store that cannot answer, must cause authentication to fail closed.
/// </summary>
public interface INonceStore
{
    /// <summary>Atomically records the nonce; false when the nonce was already used or storage failed.</summary>
    bool TryConsume(string purpose, ReadOnlySpan<byte> nonce, DateTimeOffset observedAtUtc);

    /// <summary>Deletes nonces older than the supplied horizon. Returns the number removed.</summary>
    int PurgeExpired(DateTimeOffset horizon);
}

/// <summary>
/// The trusted authentication authority. It is the only component permitted to mint
/// <see cref="AuthenticatedSession"/> instances and verified actors in production.
/// Owner evidence everywhere in M2 is a live Owner session id (16 raw bytes) that only
/// this authority can mint; it is never a caller-supplied identity claim.
/// </summary>
public interface IIdentityAuthority
{
    SecurityPolicyVersion PolicyVersion { get; }

    bool IsOwnerEnrolled { get; }

    /// <summary>Resolves live Owner session evidence. False for unknown, expired, revoked, or non-Owner sessions.</summary>
    bool TryResolveOwnerSession(ReadOnlyMemory<byte> ownerEvidence, out string failure);

    /// <summary>Latches or clears the emergency stop. Clearing requires a live Owner session.</summary>
    bool TrySetEmergencyStop(ReadOnlyMemory<byte> ownerEvidence, string reasonCode, bool active);

    /// <summary>Owner preemption: revokes all agent sessions and bumps the preemption epoch.</summary>
    bool TryPreempt(ReadOnlyMemory<byte> ownerEvidence);

    /// <summary>Owner enrollment is a one-time, Owner-confirmed operation. Never automatic.</summary>
    Result<AuthenticatedSession> EnrollOwner(OwnerEnrollmentRequest request);

    /// <summary>Authenticates the Owner using the approved factor policy and an OS account observation.</summary>
    Result<AuthenticatedSession> AuthenticateOwner(OwnerAuthenticationRequest request);

    /// <summary>Authenticates a registered, non-revoked agent using a signed fresh nonce.</summary>
    Result<AuthenticatedSession> AuthenticateAgent(AgentAuthenticationRequest request);

    /// <summary>Returns the session only while it is Active; expired or revoked sessions return false.</summary>
    bool TryGetActiveSession(Guid sessionId, out AuthenticatedSession? session);

    /// <summary>
    /// Re-resolves the current verified actor for a session id. Returns false for
    /// unknown, expired, revoked, or closed sessions.
    /// </summary>
    bool TryResolveActor(Guid sessionId, out VerifiedActor? actor);

    Result RevokeSession(Guid sessionId, ReadOnlyMemory<byte> ownerEvidence);

    int RevokeAllSessions(ReadOnlyMemory<byte> ownerEvidence);

    void RegisterAgent(AgentRegistration registration, ReadOnlyMemory<byte> ownerEvidence);

    void RevokeAgent(string agentId, ReadOnlyMemory<byte> ownerEvidence);

    /// <summary>Periodic bookkeeping: expires due sessions and purges stale nonces.</summary>
    void SweepExpired();
}

/// <summary>Non-secret Owner enrollment request. No password or factor value is included.</summary>
public sealed record OwnerEnrollmentRequest(
    string OwnerSubjectId,
    string OwnerSid);

/// <summary>Owner authentication attempt using approved factors.</summary>
public sealed class OwnerAuthenticationRequest
{
    public OwnerAuthenticationRequest(
        WindowsAccountIdentity account,
        IReadOnlyList<OwnerFactorEvidence> factors,
        string policyVersion)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(factors);
        Account = account;
        Factors = factors;
        PolicyVersion = policyVersion;
    }

    public WindowsAccountIdentity Account { get; }

    public IReadOnlyList<OwnerFactorEvidence> Factors { get; }

    public string PolicyVersion { get; }
}

/// <summary>Agent authentication attempt using a signed fresh nonce.</summary>
public sealed record AgentAuthenticationRequest(
    string AgentId,
    byte[] ChallengeNonce,
    byte[] Signature,
    string PolicyVersion);

/// <summary>Session lifetime policy. Values beyond the maxima are clamped, never extended.</summary>
public sealed record SessionPolicy
{
    public static readonly TimeSpan MinimumLifetime = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan MaximumLifetime = TimeSpan.FromHours(12);

    private SessionPolicy(TimeSpan ownerLifetime, TimeSpan agentLifetime)
    {
        OwnerSessionLifetime = Clamp(ownerLifetime);
        AgentSessionLifetime = Clamp(agentLifetime);
    }

    public TimeSpan OwnerSessionLifetime { get; }

    public TimeSpan AgentSessionLifetime { get; }

    public static SessionPolicy Default { get; } = Create(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(15));

    public static SessionPolicy Create(TimeSpan ownerLifetime, TimeSpan agentLifetime) =>
        new(ownerLifetime, agentLifetime);

    public static TimeSpan Clamp(TimeSpan value) =>
        value < MinimumLifetime ? MinimumLifetime
        : value > MaximumLifetime ? MaximumLifetime
        : value;
}
