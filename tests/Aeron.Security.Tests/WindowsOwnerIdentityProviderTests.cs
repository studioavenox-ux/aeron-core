using Aeron.Contracts;
using Xunit;

namespace Aeron.Security.Tests;

public sealed class WindowsOwnerIdentityProviderTests
{
    private sealed class Account : IWindowsAccountIdentitySource
    {
        public bool Available { get; set; } = true;
        public bool TryGetCurrentAccount(out WindowsAccountIdentity? account)
        {
            account = Available ? new WindowsAccountIdentity("S-1-5-21-example") : null;
            return Available;
        }
    }

    private sealed class Verifier : IWindowsOwnerVerifier
    {
        public bool Valid { get; set; }
        public bool TryVerifyOwner(WindowsAccountIdentity account, ReadOnlyMemory<byte> evidence, out string? ownerSubjectId)
        {
            ownerSubjectId = Valid ? "owner" : null;
            return Valid;
        }
    }

    [Fact]
    public void WindowsLoginAloneDoesNotAuthenticateOwner()
    {
        var account = new Account();
        var verifier = new Verifier();
        var provider = new WindowsOwnerIdentityProvider(account, verifier);
        Assert.False(provider.TryAuthenticate(new byte[] { 1 }, out _));
        verifier.Valid = true;
        Assert.False(provider.TryAuthenticate(ReadOnlyMemory<byte>.Empty, out _));
        Assert.True(provider.TryAuthenticate(new byte[] { 1 }, out var identity));
        Assert.Equal(new VerifiedIdentity("owner", IdentityKind.Owner), identity);
        account.Available = false;
        Assert.False(provider.TryAuthenticate(new byte[] { 1 }, out _));
    }
}
