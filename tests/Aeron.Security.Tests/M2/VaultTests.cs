using System.Text;
using Aeron.Contracts;
using Aeron.TestSupport;
using Xunit;

namespace Aeron.Security.Tests.M2;

public sealed class VaultTests : IDisposable
{
    private readonly string _root;
    private readonly RedactionFilter _redactionFilter = new();

    public VaultTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"aeron-vault-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }

    private WindowsCredentialVault CreateVault(IVaultEncryptionPolicy? policy = null, IDataProtector? protector = null, string? subdirectory = null) =>
        new(
            subdirectory is null ? _root : Path.Combine(_root, subdirectory),
            protector ?? new InMemoryDataProtector(),
            policy ?? new TestApprovedVaultEncryptionPolicy(),
            _redactionFilter);

    [Fact]
    public void UnapprovedPolicyBlocksStorageSoSecretsAreNeverSilentlyWritten()
    {
        var vault = CreateVault(new UnapprovedVaultEncryptionPolicy());

        var stored = vault.Store("synthetic-key", Encoding.UTF8.GetBytes("synthetic-secret-value"));
        Assert.False(stored.IsSuccess);
        Assert.Equal(VaultOutcome.Unavailable, stored.Outcome);
        Assert.Empty(Directory.GetFiles(_root));

        var policy = new UnapprovedVaultEncryptionPolicy();
        Assert.False(policy.IsApproved);
        Assert.Contains("unresolved", policy.AlgorithmSummary);
    }

    [Fact]
    public void RoundTripRotationAndDeletionWorkUnderAnApprovedPolicy()
    {
        var vault = CreateVault();
        var secret = Encoding.UTF8.GetBytes("synthetic-secret-value-1");

        Assert.True(vault.Store("synthetic-key", secret).IsSuccess);
        Assert.True(vault.Exists("synthetic-key"));
        Assert.Equal(VaultOutcome.Success, vault.Retrieve("synthetic-key", out var loaded).Outcome);
        Assert.Equal(secret, loaded!);

        Assert.True(vault.Rotate("synthetic-key", Encoding.UTF8.GetBytes("synthetic-secret-value-2")).IsSuccess);
        Assert.True(vault.Retrieve("synthetic-key", out var rotated).IsSuccess);
        Assert.Equal("synthetic-secret-value-2", Encoding.UTF8.GetString(rotated!));

        Assert.True(vault.Delete("synthetic-key").IsSuccess);
        Assert.False(vault.Exists("synthetic-key"));
        Assert.Equal(VaultOutcome.NotFound, vault.Retrieve("synthetic-key", out _).Outcome);
    }

    [Fact]
    public void CiphertextIsAuthenticatedAndTamperingIsDetected()
    {
        var vault = CreateVault();
        Assert.True(vault.Store("synthetic-key", Encoding.UTF8.GetBytes("synthetic-secret-value")).IsSuccess);

        var cipherFile = Directory.GetFiles(_root, "*.secret").Single();
        var bytes = File.ReadAllBytes(cipherFile);
        bytes[^1] ^= 0xFF;
        File.WriteAllBytes(cipherFile, bytes);

        var result = vault.Retrieve("synthetic-key", out var secret);
        Assert.False(result.IsSuccess);
        Assert.Equal(VaultOutcome.Corrupt, result.Outcome);
        Assert.Null(secret);
    }

    [Fact]
    public void RawSecretsNeverAppearOnDiskInPlaintext()
    {
        var vault = CreateVault();
        var secretText = "synthetic-plaintext-secret-9876";
        Assert.True(vault.Store("synthetic-key", Encoding.UTF8.GetBytes(secretText)).IsSuccess);

        foreach (var file in Directory.GetFiles(_root, "*", SearchOption.AllDirectories))
        {
            var bytes = File.ReadAllBytes(file);
            var text = Encoding.UTF8.GetString(bytes);
            Assert.DoesNotContain(secretText, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void InvalidNamesAndOversizedSecretsAreRejectedWithoutTouchingTheVault()
    {
        var vault = CreateVault();

        foreach (var badName in new[] { "", "   ", "../escape", "with space", new string('a', 200) })
        {
            Assert.False(vault.Store(badName, Encoding.UTF8.GetBytes("value")).IsSuccess);
        }

        Assert.False(vault.Store("valid-name", []).IsSuccess);
        Assert.False(vault.Store("valid-name", new byte[WindowsCredentialVault.MaxSecretBytes + 1]).IsSuccess);
    }

    [Fact]
    public void KeyProtectionFailureIsReportedAndNeverFallsBackToPlaintext()
    {
        var vault = CreateVault(protector: new RejectingDataProtector());
        var stored = vault.Store("synthetic-key", Encoding.UTF8.GetBytes("synthetic-secret"));

        Assert.False(stored.IsSuccess);
        Assert.Empty(Directory.GetFiles(_root, "*.secret"));

        // A different protector cannot unprotect an existing key: retrieval fails closed.
        var healthyVault = CreateVault(subdirectory: "other");
        Assert.True(healthyVault.Store("synthetic-key", Encoding.UTF8.GetBytes("synthetic-secret")).IsSuccess);

        var differentProtector = CreateVault(protector: new InMemoryDataProtector(), subdirectory: "other");
        Assert.Equal(VaultOutcome.Unavailable, differentProtector.Retrieve("synthetic-key", out _).Outcome);
    }

    [Fact]
    public void VaultSecretCanSeedTheOperationBindingKey()
    {
        var vault = CreateVault();
        Assert.True(vault.Store("binding-key", RandomKey()).IsSuccess);
        Assert.True(vault.Retrieve("binding-key", out var key).IsSuccess);

        using var first = new OperationBinding(key!);
        using var second = new OperationBinding(key!);
        var id = Guid.NewGuid();
        var digest = first.Compute(id, SecurityAction.ReadLocalData, "agent-alpha", IdentityKind.Agent, "target", "policy", [1, 2, 3]);

        // The same vault-protected key reproduces the binding; a volatile key does not.
        Assert.True(second.Verify(digest, id, SecurityAction.ReadLocalData, "agent-alpha", IdentityKind.Agent, "target", "policy", [1, 2, 3]));
        using var volatileBinding = OperationBinding.CreateVolatile();
        Assert.False(volatileBinding.Verify(digest, id, SecurityAction.ReadLocalData, "agent-alpha", IdentityKind.Agent, "target", "policy", [1, 2, 3]));
    }

    private static byte[] RandomKey() => System.Security.Cryptography.RandomNumberGenerator.GetBytes(OperationBinding.KeyLength);

    private sealed class RejectingDataProtector : IDataProtector
    {
        public bool TryProtect(ReadOnlySpan<byte> plaintext, out byte[] protectedBytes)
        {
            protectedBytes = [];
            return false;
        }

        public bool TryUnprotect(ReadOnlySpan<byte> protectedBytes, out byte[] plaintext)
        {
            plaintext = [];
            return false;
        }
    }
}
