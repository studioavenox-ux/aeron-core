using System.Security.Cryptography;
using System.Text;
using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// In-memory single-use nonce store. Volatile by design: restarting AERON invalidates
/// every outstanding challenge, which fails closed. Bounded by an explicit capacity so a
/// flood cannot exhaust memory; saturation denies new challenges instead of evicting
/// live nonces.
/// </summary>
public sealed class InMemoryNonceStore : INonceStore
{
    public const int DefaultCapacity = 4096;

    private readonly Lock _sync = new();
    private readonly Dictionary<string, DateTimeOffset> _seen = new(StringComparer.Ordinal);
    private readonly int _capacity;

    public InMemoryNonceStore(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    public bool TryConsume(string purpose, ReadOnlySpan<byte> nonce, DateTimeOffset observedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(purpose) || nonce.IsEmpty)
        {
            return false;
        }

        var key = BuildKey(purpose, nonce);

        lock (_sync)
        {
            if (_seen.ContainsKey(key))
            {
                return false;
            }

            if (_seen.Count >= _capacity)
            {
                // Fail closed rather than evicting a live nonce.
                return false;
            }

            _seen.Add(key, observedAtUtc);
            return true;
        }
    }

    public int PurgeExpired(DateTimeOffset horizon)
    {
        lock (_sync)
        {
            var stale = _seen
                .Where(pair => pair.Value < horizon)
                .Select(pair => pair.Key)
                .ToArray();

            foreach (var key in stale)
            {
                _seen.Remove(key);
            }

            return stale.Length;
        }
    }

    internal static string BuildKey(string purpose, ReadOnlySpan<byte> nonce)
    {
        var purposeBytes = Encoding.UTF8.GetBytes(purpose);
        var material = new byte[purposeBytes.Length + 1 + nonce.Length];
        try
        {
            purposeBytes.CopyTo(material, 0);
            material[purposeBytes.Length] = 0x00;
            nonce.CopyTo(material.AsSpan(purposeBytes.Length + 1));
            return Convert.ToHexString(SHA256.HashData(material));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(material);
        }
    }
}

/// <summary>
/// <see cref="IPermissionStore"/> implementation with default-deny semantics.
/// Grants live in memory for M2 and are re-established by the Owner at each start;
/// restart is therefore a default-deny event, not a failure.
/// </summary>
public sealed class InMemoryPermissionStore : IPermissionStore
{
    private readonly Lock _sync = new();
    private readonly HashSet<GrantKey> _grants = [];

    public bool TryGrant(string subjectId, IdentityKind kind, SecurityCapability capability)
    {
        if (!IsValid(subjectId, kind, capability))
        {
            return false;
        }

        lock (_sync)
        {
            return _grants.Add(new GrantKey(subjectId, kind, capability));
        }
    }

    public bool TryRevoke(string subjectId, IdentityKind kind, SecurityCapability capability)
    {
        if (!IsValid(subjectId, kind, capability))
        {
            return false;
        }

        lock (_sync)
        {
            return _grants.Remove(new GrantKey(subjectId, kind, capability));
        }
    }

    public bool IsGranted(string subjectId, IdentityKind kind, SecurityCapability capability)
    {
        if (!IsValid(subjectId, kind, capability))
        {
            return false;
        }

        lock (_sync)
        {
            return _grants.Contains(new GrantKey(subjectId, kind, capability));
        }
    }

    public IReadOnlyList<SecurityCapability> ListGrants(string subjectId, IdentityKind kind)
    {
        if (string.IsNullOrWhiteSpace(subjectId) || !Enum.IsDefined(kind))
        {
            return [];
        }

        lock (_sync)
        {
            return _grants
                .Where(grant => grant.Matches(subjectId, kind))
                .Select(static grant => grant.Capability)
                .OrderBy(static capability => (int)capability)
                .ToArray();
        }
    }

    public int RevokeAll(string subjectId, IdentityKind kind)
    {
        if (string.IsNullOrWhiteSpace(subjectId) || !Enum.IsDefined(kind))
        {
            return 0;
        }

        lock (_sync)
        {
            return _grants.RemoveWhere(grant => grant.Matches(subjectId, kind));
        }
    }

    private static bool IsValid(string subjectId, IdentityKind kind, SecurityCapability capability) =>
        !string.IsNullOrWhiteSpace(subjectId) &&
        subjectId.Length <= VerifiedActor.MaxSubjectLength &&
        kind is IdentityKind.Owner or IdentityKind.Agent &&
        Enum.IsDefined(capability) &&
        capability != SecurityCapability.Unknown;

    private readonly record struct GrantKey(string SubjectId, IdentityKind Kind, SecurityCapability Capability)
    {
        public bool Matches(string subjectId, IdentityKind kind) =>
            string.Equals(SubjectId, subjectId, StringComparison.Ordinal) && Kind == kind;
    }
}

/// <summary>
/// <see cref="IOwnerFactorPolicy"/> placeholder that is deliberately NOT approved.
/// Windows account presence alone is never authentication; M2 has no approved MFA
/// factor/enrollment/recovery design, so Owner sessions stay unavailable until the
/// Owner resolves the decision register entry and registers a real policy.
/// </summary>
public sealed class UnapprovedOwnerFactorPolicy : IOwnerFactorPolicy
{
    public string PolicyId => "unapproved";

    public bool IsApproved => false;

    public bool TryVerifyFactors(
        WindowsAccountIdentity account,
        IReadOnlyList<OwnerFactorEvidence> factors,
        out string? verifiedSubjectId)
    {
        // Never authenticates: no approved factor design exists yet.
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(factors);
        verifiedSubjectId = null;
        return false;
    }
}

/// <summary>
/// Agent signature verifier using ECDSA P-256 / SHA-256 over the exact challenge bytes.
/// The registration stores the agent's SubjectPublicKeyInfo; private keys never leave
/// the agent's own trusted boundary and AERON cannot mint signatures for it.
/// </summary>
public sealed class EcdsaAgentSignatureVerifier : IAgentSignatureVerifier
{
    public bool TryVerify(AgentRegistration registration, ReadOnlySpan<byte> challenge, ReadOnlySpan<byte> signature)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (registration.IsRevoked ||
            challenge.IsEmpty ||
            signature.IsEmpty ||
            registration.PublicKey.Length == 0 ||
            registration.PublicKey.Length > AgentRegistration.MaxPublicKeyBytes)
        {
            return false;
        }

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(registration.PublicKey, out var bytesRead);
            if (bytesRead != registration.PublicKey.Length)
            {
                return false;
            }

            return ecdsa.VerifyData(challenge, signature, HashAlgorithmName.SHA256);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
