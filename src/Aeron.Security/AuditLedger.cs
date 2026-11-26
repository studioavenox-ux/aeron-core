using System.Buffers.Binary;
using System.Security.Cryptography;
using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// Fixed-format, hash-chained ledger foundation. Requires a pre-existing file and a
/// separately protected, rollback-resistant, durably provisioned checkpoint. Not wired
/// into the application. Crash between file flush and checkpoint update fails closed.
/// File access and checkpoint implementations must be protected against untrusted users.
/// </summary>
public sealed class AuditLedger : ISecurityAuditSink
{
    private const int RecordSize = 92; // sequence, kind, correlation id, previous hash, hash
    private readonly string _path;
    private readonly IProtectedAuditCheckpointStore _checkpoint;
    private readonly Lock _sync = new();
    private bool _unhealthy;

    public AuditLedger(string existingFilePath, IProtectedAuditCheckpointStore checkpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(existingFilePath);
        ArgumentNullException.ThrowIfNull(checkpoint);
        _path = existingFilePath;
        _checkpoint = checkpoint;
    }

    public bool TryAppend(SecurityAuditKind kind, Guid correlationId)
    {
        lock (_sync)
        {
            if (_unhealthy || !Enum.IsDefined(kind) || correlationId == Guid.Empty) return false;
            try
            {
                // Exclusive handle serializes cooperating processes and denies sharing on Windows.
                using var file = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                if (!_checkpoint.TryRead(out var anchoredSequence, out var anchoredHash) ||
                    anchoredSequence < 0 || anchoredHash is null || anchoredHash.Length != 32 ||
                    !Verify(file, out var sequence, out var lastHash) ||
                    sequence != anchoredSequence || !CryptographicOperations.FixedTimeEquals(lastHash, anchoredHash) ||
                    sequence == long.MaxValue)
                {
                    _unhealthy = true;
                    return false;
                }

                var record = new byte[RecordSize];
                BinaryPrimitives.WriteInt64BigEndian(record.AsSpan(0, 8), sequence + 1);
                BinaryPrimitives.WriteInt32BigEndian(record.AsSpan(8, 4), (int)kind);
                if (!correlationId.TryWriteBytes(record.AsSpan(12, 16))) throw new InvalidOperationException();
                lastHash.CopyTo(record, 28);
                SHA256.HashData(record.AsSpan(0, 60), record.AsSpan(60, 32));
                file.Position = file.Length;
                file.Write(record);
                file.Flush(flushToDisk: true);
                if (!_checkpoint.TryWrite(sequence + 1, record.AsSpan(60, 32)))
                {
                    _unhealthy = true;
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                _unhealthy = true;
                return false;
            }
        }
    }

    private static bool Verify(FileStream file, out long sequence, out byte[] lastHash)
    {
        sequence = 0;
        lastHash = new byte[32];
        if (file.Length % RecordSize != 0) return false;
        file.Position = 0;
        var record = new byte[RecordSize];
        Span<byte> actual = stackalloc byte[32];
        while (file.Position < file.Length)
        {
            file.ReadExactly(record);
            if (sequence == long.MaxValue || BinaryPrimitives.ReadInt64BigEndian(record.AsSpan(0, 8)) != sequence + 1 ||
                !Enum.IsDefined((SecurityAuditKind)BinaryPrimitives.ReadInt32BigEndian(record.AsSpan(8, 4))) ||
                new Guid(record.AsSpan(12, 16)) == Guid.Empty ||
                !CryptographicOperations.FixedTimeEquals(record.AsSpan(28, 32), lastHash)) return false;
            SHA256.HashData(record.AsSpan(0, 60), actual);
            if (!CryptographicOperations.FixedTimeEquals(actual, record.AsSpan(60, 32))) return false;
            record.AsSpan(60, 32).CopyTo(lastHash);
            sequence++;
        }

        return true;
    }
}
