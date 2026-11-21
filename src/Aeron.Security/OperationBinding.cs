using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// Versioned, length-framed HMAC for exact operation identity. The key is random and
/// process-local: all bindings become unverifiable after disposal or restart. It is not
/// a durable approval store and does not authenticate or dispatch an operation.
/// </summary>
public sealed class OperationBinding : IDisposable
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly byte[] Domain = Encoding.ASCII.GetBytes("AERON-OPERATION-BINDING-V1");
    private readonly Lock _sync = new();
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);
    private bool _disposed;

    public byte[] Compute(
        Guid approvalId,
        SecurityAction action,
        string requestingSubject,
        string target,
        string policyVersion,
        ReadOnlySpan<byte> exactParameters)
    {
        if (approvalId == Guid.Empty || !Enum.IsDefined(action) || action == SecurityAction.Unknown)
        {
            throw new ArgumentException("An approval ID and known action are required.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(requestingSubject);
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
                throw new InvalidOperationException("Unable to encode approval ID.");
            }

            AppendBytes(hash, id);
            Span<byte> encodedAction = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(encodedAction, (int)action);
            AppendBytes(hash, encodedAction);
            AppendText(hash, requestingSubject);
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
        string requestingSubject,
        string target,
        string policyVersion,
        ReadOnlySpan<byte> exactParameters)
    {
        if (expectedBinding.Length != 32)
        {
            return false;
        }

        var actual = Compute(approvalId, action, requestingSubject, target, policyVersion, exactParameters);
        try
        {
            return CryptographicOperations.FixedTimeEquals(expectedBinding, actual);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actual);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (!_disposed)
            {
                CryptographicOperations.ZeroMemory(_key);
                _disposed = true;
            }
        }
    }

    private static void AppendText(IncrementalHash hash, string value)
    {
        // Strict UTF-8 rejects invalid surrogate sequences instead of mapping distinct
        // strings to the same replacement character. No normalization is performed.
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
