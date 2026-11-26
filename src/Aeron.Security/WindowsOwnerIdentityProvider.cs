using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// Composes two trusted dependencies; Windows account presence alone never authenticates
/// an Owner. No OS account source, MFA verifier or production registration exists yet.
/// </summary>
public sealed class WindowsOwnerIdentityProvider : ITrustedIdentityProvider
{
    private readonly IWindowsAccountIdentitySource _accounts;
    private readonly IWindowsOwnerVerifier _verifier;

    public WindowsOwnerIdentityProvider(IWindowsAccountIdentitySource accounts, IWindowsOwnerVerifier verifier)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(verifier);
        _accounts = accounts;
        _verifier = verifier;
    }

    public bool TryAuthenticate(ReadOnlyMemory<byte> evidence, out VerifiedIdentity? identity)
    {
        identity = null;
        if (evidence.IsEmpty) return false;
        try
        {
            if (!_accounts.TryGetCurrentAccount(out var account) || account is null ||
                string.IsNullOrWhiteSpace(account.Sid) || account.Sid.Length > 256 ||
                !_verifier.TryVerifyOwner(account, evidence, out var subject) ||
                string.IsNullOrWhiteSpace(subject) || subject.Length > 256) return false;
            identity = new VerifiedIdentity(subject, IdentityKind.Owner);
            return true;
        }
        catch (Exception) { return false; }
    }
}
