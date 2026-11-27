using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// Versioned, length-framed keyed binding of the exact operation (HMAC-SHA-256).
///
/// * The key is supplied by the platform secure store (DPAPI-protected in production) or
///   generated per-instance when a caller explicitly accepts a volatile key. A volatile
///   key invalidates every outstanding approval on restart, which fails closed.
/// * Every material field is length-framed so ("a","bc") and ("ab","c") cannot collide.
/// * Raw payload bytes are covered by the binding and are never stored, logged, or
///   displayed. Only the 32-byte binding hash is persisted.
/// * Strict UTF-8 rejects invalid surrogates instead of silently mapping distinct strings
///   onto the same replacement character.
/// </summary>
public sealed class OperationBinding : IOperationBinding, IDisposable
{
    public const int KeyLength = 32;
    public const int BindingLength = 32;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly byte[] Domain = Encoding.ASCII.GetBytes("AERON-OPERATION-BINDING-V3");

    private readonly Lock _sync = new();
    private readonly byte[] _key;
    private bool _disposed;

    /// <summary>Creates a binding with a caller-supplied key (for example from the vault).</summary>
    public OperationBinding(ReadOnlySpan<byte> key)
    {
        if (key.Length != KeyLength)
        {
            throw new ArgumentException($"A {KeyLength}-byte key is required.", nameof(key));
        }

        _key = key.ToArray();
    }

    /// <summary>
    /// Creates a volatile binding with a random process-local key. Every approval bound
    /// through it becomes unverifiable on restart, which fails closed. Production
    /// composition supplies a vault-protected key instead.
    /// </summary>
    public OperationBinding()
        : this(RandomNumberGenerator.GetBytes(KeyLength))
    {
    }

    /// <summary>Explicitly-named factory for a volatile binding.</summary>
    public static OperationBinding CreateVolatile() => new();

    public byte[] Compute(
        Guid approvalId,
        SecurityAction action,
        string actorSubject,
        IdentityKind actorKind,
        string target,
        string policyVersion,
        ReadOnlySpan<byte> exactParameters)
    {
        if (approvalId == Guid.Empty ||
            !Enum.IsDefined(action) || action == SecurityAction.Unknown ||
            actorKind is not (IdentityKind.Owner or IdentityKind.Agent))
        {
            throw new ArgumentException("A valid approval id, action, and actor kind are required.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(actorSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyVersion);

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            using var hash = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, _key);

            AppendBytes(hash, Domain);

            Span<byte> id = stackalloc byte[16];
            if (!approvalId.TryWriteBytes(id))
            {
                throw new InvalidOperationException("Unable to encode the approval id.");
            }

            AppendBytes(hash, id);

            Span<byte> encodedAction = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(encodedAction, (int)action);
            AppendBytes(hash, encodedAction);

            AppendText(hash, actorSubject);

            Span<byte> encodedKind = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(encodedKind, (int)actorKind);
            AppendBytes(hash, encodedKind);

            AppendText(hash, target);
            AppendText(hash, policyVersion);
            AppendBytes(hash, exactParameters);

            return hash.GetHashAndReset();
        }
    }

    public bool Verify(
        ReadOnlySpan<byte> expectedBinding,
        Guid approvalId,
        SecurityAction action,
        string actorSubject,
        IdentityKind actorKind,
        string target,
        string policyVersion,
        ReadOnlySpan<byte> exactParameters)
    {
        if (expectedBinding.Length != BindingLength)
        {
            return false;
        }

        try
        {
            var actual = Compute(approvalId, action, actorSubject, actorKind, target, policyVersion, exactParameters);

            try
            {
                return CryptographicOperations.FixedTimeEquals(expectedBinding, actual);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(actual);
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            CryptographicOperations.ZeroMemory(_key);
            _disposed = true;
        }
    }

    private static void AppendText(IncrementalHash hash, string value)
    {
        var bytes = StrictUtf8.GetBytes(value);

        try
        {
            AppendBytes(hash, bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static void AppendBytes(IncrementalHash hash, ReadOnlySpan<byte> bytes)
    {
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
