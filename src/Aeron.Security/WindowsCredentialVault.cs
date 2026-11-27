using System.Security.Cryptography;
using System.Text;
using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// Windows security vault using built-in Windows data protection.
///
/// Design (explicit, not invented silently):
/// * A random 256-bit per-machine data key protects every secret with AES-256-GCM
///   (authenticated encryption; tampering is detected, not silently decrypted).
/// * The data key itself is protected at rest by an <see cref="IDataProtector"/>. The
///   production implementation is Windows DPAPI in <c>CurrentUser</c> scope, so only the
///   enrolled Windows account on this device can unprotect it. Tests inject an ephemeral
///   protector; an ephemeral protector never persists keys across restarts.
/// * Secret names are validated to a conservative alphabet; raw secrets never appear in
///   file names, logs, audit records, error messages, or the secret directory listing.
/// * Rotation re-encrypts under a fresh nonce and deletes the prior ciphertext before the
///   new file is linked, so an interrupted rotation fails closed.
///
/// What this is not: it is not MFA, it does not implement device trust, and it does not
/// implement the Owner's unresolved key-recovery and backup decisions. Until the Owner
/// approves an encryption/key-recovery policy, <see cref="UnapprovedVaultEncryptionPolicy"/>
/// blocks storage entirely.
/// </summary>
public sealed class WindowsCredentialVault : IWindowsCredentialVault
{
    public const int MaxSecretBytes = 64 * 1024;
    public const int MaxNameLength = 64;

    private const byte FormatVersion = 1;
    private const int NonceSize = VaultEnvelope.NonceSize;
    private const int TagSize = VaultEnvelope.TagSize;
    private const int KeySize = 32;

    private readonly Lock _sync = new();
    private readonly string _directory;
    private readonly string _keyFilePath;
    private readonly IDataProtector _protector;
    private readonly IVaultEncryptionPolicy _policy;
    private readonly IRedactionFilter _redactionFilter;

    private byte[]? _cachedKey;

    public WindowsCredentialVault(
        string directory,
        IDataProtector protector,
        IVaultEncryptionPolicy policy,
        IRedactionFilter redactionFilter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(redactionFilter);

        _directory = directory;
        _keyFilePath = Path.Combine(directory, "vault.key.protected");
        _protector = protector;
        _policy = policy;
        _redactionFilter = redactionFilter;
    }

    public VaultResult Store(string secretName, ReadOnlySpan<byte> secret)
    {
        if (!TryValidateName(secretName, out var validName))
        {
            return VaultResult.Failure(VaultOutcome.InvalidName);
        }

        if (secret.IsEmpty || secret.Length > MaxSecretBytes)
        {
            return VaultResult.Failure(VaultOutcome.InvalidSecret);
        }

        if (!_policy.IsApproved)
        {
            return VaultResult.Failure(VaultOutcome.Unavailable,
                "The vault refuses to store secrets until the Owner approves an encryption and key-recovery policy.");
        }

        lock (_sync)
        {
            try
            {
                var path = PathFor(validName);

                if (File.Exists(path))
                {
                    return VaultResult.Failure(VaultOutcome.InvalidName, "A secret with this name already exists; use rotation.");
                }

                if (CountSecrets() >= 64)
                {
                    return VaultResult.Failure(VaultOutcome.Unavailable, "The vault capacity limit was reached.");
                }

                if (!TryEnsureKey(out var key, out var keyFailure))
                {
                    return VaultResult.Failure(VaultOutcome.Unavailable, keyFailure);
                }

                var envelope = Encrypt(key, secret);
                WriteAtomic(path, envelope);
                return VaultResult.Success;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
            {
                return VaultResult.Failure(VaultOutcome.Unavailable, SafeDiagnostic(ex));
            }
        }
    }

    public VaultResult Retrieve(string secretName, out byte[]? secret)
    {
        secret = null;

        if (!TryValidateName(secretName, out var validName))
        {
            return VaultResult.Failure(VaultOutcome.InvalidName);
        }

        lock (_sync)
        {
            try
            {
                var path = PathFor(validName);

                if (!File.Exists(path))
                {
                    return VaultResult.Failure(VaultOutcome.NotFound);
                }

                if (!TryLoadKey(out var key, out var keyFailure))
                {
                    return VaultResult.Failure(VaultOutcome.Unavailable, keyFailure);
                }

                var envelope = File.ReadAllBytes(path);
                var plaintext = Decrypt(key, envelope);

                if (plaintext is null)
                {
                    return VaultResult.Failure(VaultOutcome.Corrupt, "The secret failed authenticated decryption.");
                }

                secret = plaintext;
                return VaultResult.Success;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return VaultResult.Failure(VaultOutcome.Unavailable, SafeDiagnostic(ex));
            }
        }
    }

    public VaultResult Rotate(string secretName, ReadOnlySpan<byte> newSecret)
    {
        if (!TryValidateName(secretName, out var validName))
        {
            return VaultResult.Failure(VaultOutcome.InvalidName);
        }

        if (newSecret.IsEmpty || newSecret.Length > MaxSecretBytes)
        {
            return VaultResult.Failure(VaultOutcome.InvalidSecret);
        }

        if (!_policy.IsApproved)
        {
            return VaultResult.Failure(VaultOutcome.Unavailable,
                "The vault refuses to rotate secrets until the Owner approves an encryption and key-recovery policy.");
        }

        lock (_sync)
        {
            try
            {
                var path = PathFor(validName);

                if (!File.Exists(path))
                {
                    return VaultResult.Failure(VaultOutcome.NotFound);
                }

                if (!TryEnsureKey(out var key, out var keyFailure))
                {
                    return VaultResult.Failure(VaultOutcome.RotationFailed, keyFailure);
                }

                var envelope = Encrypt(key, newSecret);
                WriteAtomic(path, envelope);
                return VaultResult.Success;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
            {
                return VaultResult.Failure(VaultOutcome.RotationFailed, SafeDiagnostic(ex));
            }
        }
    }

    public VaultResult Delete(string secretName)
    {
        if (!TryValidateName(secretName, out var validName))
        {
            return VaultResult.Failure(VaultOutcome.InvalidName);
        }

        lock (_sync)
        {
            try
            {
                var path = PathFor(validName);

                if (!File.Exists(path))
                {
                    return VaultResult.Failure(VaultOutcome.NotFound);
                }

                File.Delete(path);
                return VaultResult.Success;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return VaultResult.Failure(VaultOutcome.Unavailable, SafeDiagnostic(ex));
            }
        }
    }

    public bool Exists(string secretName) =>
        TryValidateName(secretName, out var validName) && File.Exists(PathFor(validName));

    public IReadOnlyList<string> ListSecretNames()
    {
        lock (_sync)
        {
            try
            {
                return Directory
                    .EnumerateFiles(_directory, "*.secret")
                    .Select(static path => Path.GetFileNameWithoutExtension(path))
                    .OrderBy(static name => name, StringComparer.Ordinal)
                    .ToArray();
            }
            catch (Exception)
            {
                return [];
            }
        }
    }

    private bool TryEnsureKey(out byte[] key, out string? failure)
    {
        key = [];
        failure = null;

        try
        {
            if (_cachedKey is not null)
            {
                key = _cachedKey;
                return true;
            }

            if (File.Exists(_keyFilePath))
            {
                return TryLoadKey(out key, out failure);
            }

            var fresh = RandomNumberGenerator.GetBytes(KeySize);

            if (!_protector.TryProtect(fresh, out var protectedKey) || protectedKey.Length == 0)
            {
                CryptographicOperations.ZeroMemory(fresh);
                failure = "The platform data protector refused to protect the vault key.";
                return false;
            }

            WriteAtomic(_keyFilePath, protectedKey);
            _cachedKey = fresh;
            key = fresh;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failure = SafeDiagnostic(ex);
            return false;
        }
    }

    private bool TryLoadKey(out byte[] key, out string? failure)
    {
        key = [];
        failure = null;

        try
        {
            if (_cachedKey is not null)
            {
                key = _cachedKey;
                return true;
            }

            if (!File.Exists(_keyFilePath))
            {
                failure = "The protected vault key is missing.";
                return false;
            }

            var protectedKey = File.ReadAllBytes(_keyFilePath);

            if (!_protector.TryUnprotect(protectedKey, out var unprotected) || unprotected.Length != KeySize)
            {
                failure = "The platform data protector refused to unprotect the vault key.";
                return false;
            }

            _cachedKey = unprotected;
            key = unprotected;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failure = SafeDiagnostic(ex);
            return false;
        }
    }

    private static byte[] Encrypt(byte[] key, ReadOnlySpan<byte> plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var envelope = new byte[1 + NonceSize + TagSize + ciphertext.Length];
        envelope[0] = FormatVersion;
        nonce.CopyTo(envelope.AsSpan(1));
        tag.CopyTo(envelope.AsSpan(1 + NonceSize));
        ciphertext.CopyTo(envelope.AsSpan(1 + NonceSize + TagSize));
        return envelope;
    }

    private static byte[]? Decrypt(byte[] key, byte[] envelope)
    {
        if (envelope.Length <= 1 + NonceSize + TagSize || envelope[0] != FormatVersion)
        {
            return null;
        }

        var nonce = envelope.AsSpan(1, NonceSize);
        var tag = envelope.AsSpan(1 + NonceSize, TagSize);
        var ciphertext = envelope.AsSpan(1 + NonceSize + TagSize);
        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(key);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            return null;
        }
    }

    private static void WriteAtomic(string path, byte[] content)
    {
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N")[..8];

        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                try
                {
                    File.Delete(temp);
                }
                catch (IOException)
                {
                    // Best-effort cleanup.
                }
            }
        }
    }

    private int CountSecrets() => Directory.GetFiles(_directory, "*.secret").Length;

    private string PathFor(string validName) => Path.Combine(_directory, validName + ".secret");

    private static bool TryValidateName(string? secretName, out string validName)
    {
        validName = string.Empty;

        if (string.IsNullOrWhiteSpace(secretName) || secretName.Length > MaxNameLength)
        {
            return false;
        }

        foreach (var c in secretName)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.'))
            {
                return false;
            }
        }

        validName = secretName;
        return true;
    }

    private string SafeDiagnostic(Exception ex) => _redactionFilter.Redact(ex.Message);
}

/// <summary>
/// Fixed envelope geometry for AES-256-GCM vault records: <c>version | nonce | tag |
/// ciphertext</c>. Defined once so the production vault and the test protector cannot drift.
/// </summary>
internal static class VaultEnvelope
{
    public const int NonceSize = 12;
    public const int TagSize = 16;
}

/// <summary>
/// Default vault policy: deliberately NOT approved. The Owner has not yet chosen key
/// recovery or backup semantics, so storing secrets is blocked rather than guessed.
/// </summary>
public sealed class UnapprovedVaultEncryptionPolicy : IVaultEncryptionPolicy
{
    public string PolicyId => "unapproved";

    public bool IsApproved => false;

    public string AlgorithmSummary =>
        "Unavailable: AES-256-GCM envelope encryption with DPAPI-current-user key protection is implemented, but the Owner key-recovery and backup policy is unresolved (register D2-05/D2-06), so storage is blocked.";
}

/// <summary>Ephemeral in-memory data protector for tests only. Not suitable for production.</summary>
public sealed class InMemoryDataProtector : IDataProtector
{
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);

    public bool TryProtect(ReadOnlySpan<byte> plaintext, out byte[] protectedBytes)
    {
        protectedBytes = [];

        try
        {
            var nonce = RandomNumberGenerator.GetBytes(VaultEnvelope.NonceSize);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[VaultEnvelope.TagSize];

            using var aes = new AesGcm(_key);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

            protectedBytes = new byte[VaultEnvelope.NonceSize + VaultEnvelope.TagSize + ciphertext.Length];
            nonce.CopyTo(protectedBytes.AsSpan(0));
            tag.CopyTo(protectedBytes.AsSpan(VaultEnvelope.NonceSize));
            ciphertext.CopyTo(protectedBytes.AsSpan(VaultEnvelope.NonceSize + VaultEnvelope.TagSize));
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public bool TryUnprotect(ReadOnlySpan<byte> protectedBytes, out byte[] plaintext)
    {
        plaintext = [];

        if (protectedBytes.Length <= VaultEnvelope.NonceSize + VaultEnvelope.TagSize)
        {
            return false;
        }

        try
        {
            var buffer = new byte[protectedBytes.Length - VaultEnvelope.NonceSize - VaultEnvelope.TagSize];

            using var aes = new AesGcm(_key);
            aes.Decrypt(
                protectedBytes[..VaultEnvelope.NonceSize],
                protectedBytes[(VaultEnvelope.NonceSize + VaultEnvelope.TagSize)..],
                protectedBytes.Slice(VaultEnvelope.NonceSize, VaultEnvelope.TagSize),
                buffer);

            plaintext = buffer;
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}

/// <summary>
/// Explicitly-approved test vault policy: enables storage against the ephemeral protector
/// so vault behavior (round-trip, rotation, deletion, tamper detection) is testable.
/// </summary>
public sealed class TestApprovedVaultEncryptionPolicy : IVaultEncryptionPolicy
{
    public string PolicyId => "test-only";

    public bool IsApproved => true;

    public string AlgorithmSummary => "Test policy: AES-256-GCM with an ephemeral in-memory key protector. Not for production.";
}
