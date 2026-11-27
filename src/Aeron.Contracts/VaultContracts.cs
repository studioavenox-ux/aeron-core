namespace Aeron.Contracts;

// AERON Milestone 2 — Windows security vault contracts.
// Secrets never enter application configuration, logs, audit records, or tests.
// The vault is current-user scoped; it does not supply MFA and never touches OmniRoute.

public enum VaultOutcome
{
    Success = 0,
    NotFound = 1,
    AccessDenied = 2,
    Unavailable = 3,
    Corrupt = 4,
    InvalidName = 5,
    InvalidSecret = 6,
    RotationFailed = 7,
    PlatformUnsupported = 8
}

public sealed record VaultResult(VaultOutcome Outcome, string? Diagnostic = null)
{
    public bool IsSuccess => Outcome == VaultOutcome.Success;

    public static VaultResult Success => new(VaultOutcome.Success);

    public static VaultResult Failure(VaultOutcome outcome, string? diagnostic = null) => new(outcome, diagnostic);
}

/// <summary>
/// Owner-approved envelope-encryption and key-recovery policy. M2 does not invent
/// algorithms, key recovery, or backup semantics: until this policy is approved and
/// registered, the vault refuses to store secrets.
/// </summary>
public interface IVaultEncryptionPolicy
{
    /// <summary>Stable policy identifier recorded in audit evidence.</summary>
    string PolicyId { get; }

    /// <summary>True only when the Owner has approved algorithms and key recovery.</summary>
    bool IsApproved { get; }

    /// <summary>Human-readable algorithm summary for the security status surface.</summary>
    string AlgorithmSummary { get; }
}

/// <summary>
/// Built-in Windows protection used for the vault's per-machine data key. The production
/// implementation is DPAPI (<c>CurrentUser</c> scope); tests inject an ephemeral protector.
/// </summary>
public interface IDataProtector
{
    bool TryProtect(ReadOnlySpan<byte> plaintext, out byte[] protectedBytes);

    bool TryUnprotect(ReadOnlySpan<byte> protectedBytes, out byte[] plaintext);
}

/// <summary>Current-user scoped secret vault with rotation and deletion.</summary>
public interface IWindowsCredentialVault
{
    VaultResult Store(string secretName, ReadOnlySpan<byte> secret);

    VaultResult Retrieve(string secretName, out byte[]? secret);

    /// <summary>Atomically replaces the secret and destroys prior ciphertext.</summary>
    VaultResult Rotate(string secretName, ReadOnlySpan<byte> newSecret);

    VaultResult Delete(string secretName);

    bool Exists(string secretName);

    IReadOnlyList<string> ListSecretNames();
}
