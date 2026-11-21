using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// Authenticates through an explicitly supplied trusted provider. This guard does not
/// provide MFA or OS isolation; a missing or failing provider is never an Owner identity.
/// </summary>
public sealed class OwnerAuthorityGuard
{
    private readonly ITrustedIdentityProvider? _identityProvider;

    public OwnerAuthorityGuard(ITrustedIdentityProvider? identityProvider)
    {
        _identityProvider = identityProvider;
    }

    public bool VerifyOwner(ReadOnlyMemory<byte> evidence) =>
        TryAuthenticate(evidence, out var identity, out _) && identity!.Kind == IdentityKind.Owner;

    internal bool TryAuthenticate(
        ReadOnlyMemory<byte> evidence,
        out VerifiedIdentity? identity,
        out PolicyReason reason)
    {
        identity = null;
        if (_identityProvider is null)
        {
            reason = PolicyReason.IdentityProviderUnavailable;
            return false;
        }

        if (evidence.IsEmpty)
        {
            reason = PolicyReason.IdentityRejected;
            return false;
        }

        try
        {
            if (!_identityProvider.TryAuthenticate(evidence, out var verified) ||
                verified is null ||
                string.IsNullOrWhiteSpace(verified.SubjectId) ||
                verified.SubjectId.Length > 256 ||
                (verified.Kind != IdentityKind.Owner && verified.Kind != IdentityKind.Agent))
            {
                reason = PolicyReason.IdentityRejected;
                return false;
            }

            identity = verified;
            reason = PolicyReason.Allowed;
            return true;
        }
        catch (Exception)
        {
            // Provider failures must not become authentication successes or leak evidence.
            reason = PolicyReason.IdentityRejected;
            return false;
        }
    }
}
