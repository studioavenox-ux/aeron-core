using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// Durable, append-only, hash-chained security audit ledger.
///
/// Record layout (fixed 160 bytes, big-endian):
/// <code>
/// offset  size  field
///      0     8  sequence (1-based, strictly increasing)
///      8     8  unix milliseconds UTC
///     16     4  kind (DurableAuditKind)
///     20    16  correlation id
///     36    16  subject id
///     52    16  secondary id
///     68    24  detail code (fixed width, validated machine-code alphabet)
///     92    32  previous record hash
///    124     4  reserved (zero)
///    128    32  record hash = SHA-256(bytes 0..127)
/// </code>
///
/// Integrity model:
/// * Each record links to its predecessor, so modification, reordering, or removal of
///   any record breaks verification.
/// * Truncation, rollback, and whole-file replacement are detectable only relative to an
///   independently protected checkpoint. Until that checkpoint is real, rollback
///   resistance is not claimed.
/// * Appends are ordered: write + flush the record, then advance the checkpoint. A crash
///   between the two leaves exactly one unanchored tail record. That state is
///   <em>not</em> auto-trusted: the ledger latches unhealthy and only an explicit
///   Owner-visible recovery re-anchors, because accepting an unanchored record would let
///   anyone with file-write access extend the chain.
/// * Any verification or append failure latches the ledger unhealthy. Recovery never
///   repairs, rewrites, or truncates records.
/// * Only fixed enums, opaque GUIDs, and filtered machine codes are stored; free text and
///   secrets cannot be represented.
/// </summary>
public sealed class DurableAuditLedger : IDurableAuditLedger
{
    public const int RecordSize = 160;
    public const int HashOffset = 128;
    public const int HashLength = 32;
    public const int DetailCodeLength = 24;

    public const string FailureChainBroken = "AUDIT_CHAIN_BROKEN";
    public const string FailureTruncated = "AUDIT_TRUNCATED";
    public const string FailureCheckpointMissing = "AUDIT_CHECKPOINT_MISSING";
    public const string FailureCheckpointMismatch = "AUDIT_CHECKPOINT_MISMATCH";
    public const string FailureMalformedRecord = "AUDIT_RECORD_MALFORMED";
    public const string FailureStorage = "AUDIT_STORAGE_FAILURE";
    public const string FailureRollback = "AUDIT_ROLLBACK_DETECTED";
    public const string FailureUnanchoredTail = "AUDIT_UNANCHORED_TAIL";

    private static readonly byte[] ZeroHash = new byte[HashLength];

    private readonly Lock _sync = new();
    private readonly string _path;
    private readonly IAuditCheckpointStore _checkpoint;
    private readonly IClock _clock;
    private readonly int _recentCapacity;
    private readonly Queue<AuditEntry> _recent = new();

    private long _lastSequence;
    private byte[] _lastHash = (byte[])ZeroHash.Clone();
    private bool _verified;
    private string? _latchCode;
    private string _diagnostic = "Not initialized.";

    public DurableAuditLedger(string filePath, IAuditCheckpointStore checkpoint, IClock clock, int recentCapacity = 200)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfLessThan(recentCapacity, 1);

        _path = filePath;
        _checkpoint = checkpoint;
        _clock = clock;
        _recentCapacity = recentCapacity;
    }

    bool IDurableAuditLedger.IsHealthy => IsHealthy;

    /// <summary>
    /// True only while the chain verified and no append or verification failure has
    /// latched the ledger. Verification never clears a latch; only
    /// <see cref="AttemptRecovery"/> can, and only for the single verified-tail case.
    /// </summary>
    public bool IsHealthy
    {
        get
        {
            lock (_sync)
            {
                return _verified && _latchCode is null;
            }
        }
    }

    public long LastVerifiedSequence
    {
        get
        {
            lock (_sync)
            {
                return _lastSequence;
            }
        }
    }

    public string Diagnostic
    {
        get
        {
            lock (_sync)
            {
                return _diagnostic;
            }
        }
    }

    public bool TryAppend(DurableAuditKind kind, AuditSubject subject, string detailCode)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (!Enum.IsDefined(kind) || kind == DurableAuditKind.Unknown ||
            subject.CorrelationId == Guid.Empty || !IsValidDetailCode(detailCode))
        {
            return false;
        }

        lock (_sync)
        {
            if (!_verified || _latchCode is not null)
            {
                return false;
            }

            if (_lastSequence == long.MaxValue)
            {
                Latch(FailureStorage, "Audit sequence space exhausted.");
                return false;
            }

            var sequence = _lastSequence + 1;
            var recordedAt = _clock.UtcNow;
            byte[] record;

            try
            {
                record = BuildRecord(sequence, recordedAt, kind, subject, detailCode, _lastHash);
            }
            catch (Exception)
            {
                Latch(FailureStorage, "Audit record could not be encoded.");
                return false;
            }

            try
            {
                WriteRecord(record);
            }
            catch (Exception)
            {
                Latch(FailureStorage, "Audit record could not be written.");
                return false;
            }

            var hash = record.AsSpan(HashOffset, HashLength).ToArray();

            try
            {
                if (!_checkpoint.TryWrite(sequence, hash))
                {
                    Latch(FailureCheckpointMismatch,
                        "The record was written but the protected checkpoint refused the new head; the ledger fails closed.");
                    return false;
                }
            }
            catch (Exception)
            {
                Latch(FailureCheckpointMismatch,
                    "The record was written but the protected checkpoint failed; the ledger fails closed.");
                return false;
            }

            Commit(sequence, hash, recordedAt, kind, subject, NormalizeDetailCode(detailCode));
            _diagnostic = "Healthy.";
            return true;
        }
    }

    public AuditVerificationResult VerifyIntegrity()
    {
        lock (_sync)
        {
            var result = VerifyCore(allowOwnerRecovery: false);
            _diagnostic = result.Diagnostic;
            return result;
        }
    }

    public IReadOnlyList<AuditRecordView> ReadRecent(int maxRecords)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRecords, 1);

        lock (_sync)
        {
            return _recent
                .Reverse()
                .Take(maxRecords)
                .Select(static entry => new AuditRecordView(
                    entry.Sequence,
                    entry.RecordedAtUtc,
                    entry.Kind,
                    entry.CorrelationId,
                    entry.SubjectId,
                    entry.SecondaryId,
                    entry.DetailCode,
                    (byte[])entry.PreviousHash.Clone(),
                    (byte[])entry.RecordHash.Clone()))
                .ToArray();
        }
    }

    /// <summary>
    /// Explicit Owner-visible recovery. Accepts a single verified, unanchored tail record
    /// (the crash window between append and checkpoint advance) and re-anchors it. Never
    /// repairs, rewrites, or removes records. Everything else stays failed.
    /// </summary>
    public AuditVerificationResult AttemptRecovery()
    {
        lock (_sync)
        {
            var result = VerifyCore(allowOwnerRecovery: true);
            _diagnostic = result.Diagnostic;

            if (!result.IsHealthy)
            {
                return result;
            }

            _latchCode = null;
            _verified = true;

            if (!TryAppendInternal(DurableAuditKind.RecoveryCompleted, AuditSubject.For(Guid.NewGuid()), SecurityCodes.AuditRecovered))
            {
                Latch(FailureStorage, "Recovery succeeded but the recovery marker could not be appended.");
                return AuditVerificationResult.Failed(FailureStorage, _diagnostic);
            }

            return AuditVerificationResult.Healthy(_lastSequence);
        }
    }

    private bool TryAppendInternal(DurableAuditKind kind, AuditSubject subject, string detailCode)
    {
        if (_latchCode is not null || !_verified || _lastSequence == long.MaxValue)
        {
            return false;
        }

        var sequence = _lastSequence + 1;
        var recordedAt = _clock.UtcNow;

        try
        {
            var record = BuildRecord(sequence, recordedAt, kind, subject, detailCode, _lastHash);
            WriteRecord(record);
            var hash = record.AsSpan(HashOffset, HashLength).ToArray();

            if (!_checkpoint.TryWrite(sequence, hash))
            {
                return false;
            }

            Commit(sequence, hash, recordedAt, kind, subject, NormalizeDetailCode(detailCode));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private AuditVerificationResult VerifyCore(bool allowOwnerRecovery)
    {
        _recent.Clear();

        if (!_checkpoint.TryRead(out var anchoredSequence, out var anchoredHash) ||
            anchoredHash is null ||
            anchoredHash.Length != HashLength ||
            anchoredSequence < 0)
        {
            _verified = false;
            return Latch(FailureCheckpointMissing,
                "The independently protected checkpoint is missing or unreadable. Rollback resistance is not available.");
        }

        if (!File.Exists(_path))
        {
            if (anchoredSequence == 0 && CryptographicOperations.FixedTimeEquals(anchoredHash, ZeroHash))
            {
                _lastSequence = 0;
                _lastHash = (byte[])ZeroHash.Clone();
                _verified = true;
                return AuditVerificationResult.Healthy(0);
            }

            return Latch(FailureTruncated,
                "The audit file is missing even though the checkpoint records a non-empty chain.");
        }

        long recordCount;
        byte[] previous = (byte[])ZeroHash.Clone();
        var buffer = new byte[RecordSize];
        var digest = new byte[HashLength];

        try
        {
            using var file = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var length = file.Length;

            if (length % RecordSize != 0)
            {
                return Latch(FailureMalformedRecord, "The audit file length is not a whole number of records.");
            }

            recordCount = length / RecordSize;

            for (long index = 0; index < recordCount; index++)
            {
                file.ReadExactly(buffer);

                if (BinaryPrimitives.ReadInt64BigEndian(buffer.AsSpan(0, 8)) != index + 1)
                {
                    return Latch(FailureChainBroken, "Audit sequence numbers are not contiguous.");
                }

                if (!CryptographicOperations.FixedTimeEquals(buffer.AsSpan(92, 32), previous))
                {
                    return Latch(FailureChainBroken, "An audit record does not link to its predecessor.");
                }

                SHA256.HashData(buffer.AsSpan(0, HashOffset), digest);

                if (!CryptographicOperations.FixedTimeEquals(digest, buffer.AsSpan(HashOffset, HashLength)))
                {
                    return Latch(FailureChainBroken, "An audit record hash does not match its content.");
                }

                previous = buffer.AsSpan(HashOffset, HashLength).ToArray();
                RememberFromRecord(buffer);
            }
        }
        catch (Exception)
        {
            return Latch(FailureStorage, "The audit file could not be read for verification.");
        }

        if (recordCount < anchoredSequence)
        {
            return Latch(FailureTruncated,
                "The audit file holds fewer records than the protected checkpoint; truncation detected.");
        }

        var tail = recordCount - anchoredSequence;

        if (tail > 1)
        {
            return Latch(FailureRollback,
                "The audit file is ahead of the protected checkpoint by more than one record; the state cannot be reconciled.");
        }

        if (tail == 1)
        {
            if (!allowOwnerRecovery)
            {
                return Latch(FailureUnanchoredTail,
                    "Exactly one unanchored tail record exists (interrupted append). Owner-visible recovery is required before audit-dependent operations resume.");
            }

            if (!_checkpoint.TryWrite(recordCount, previous))
            {
                return Latch(FailureCheckpointMismatch,
                    "The verified tail record could not be re-anchored in the protected checkpoint.");
            }

            _lastSequence = recordCount;
            _lastHash = previous;
            _verified = true;
            return new AuditVerificationResult(true, recordCount, string.Empty,
                "Recovered one verified, unanchored tail record and re-anchored the protected checkpoint.");
        }

        if (!CryptographicOperations.FixedTimeEquals(previous, anchoredHash))
        {
            return Latch(FailureCheckpointMismatch,
                "The audit chain head differs from the protected checkpoint (rollback or replacement detected).");
        }

        _lastSequence = recordCount;
        _lastHash = recordCount == 0 ? (byte[])ZeroHash.Clone() : previous;

        // A verification pass confirms the chain; it never clears a latch produced by a
        // failed append or failed checkpoint advance. Only AttemptRecovery clears a latch.
        _verified = true;
        return AuditVerificationResult.Healthy(recordCount);
    }

    private void WriteRecord(byte[] record)
    {
        using var file = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.None);
        file.Write(record);
        file.Flush(flushToDisk: true);
    }

    private void Commit(
        long sequence,
        byte[] hash,
        DateTimeOffset recordedAt,
        DurableAuditKind kind,
        AuditSubject subject,
        string detailCode)
    {
        _lastSequence = sequence;
        _lastHash = (byte[])hash.Clone();

        _recent.Enqueue(new AuditEntry(
            sequence,
            recordedAt,
            kind,
            subject.CorrelationId,
            subject.SubjectId,
            subject.SecondaryId,
            detailCode,
            (byte[])ZeroHash.Clone(),
            (byte[])hash.Clone()));

        TrimRecent();
    }

    private void RememberFromRecord(byte[] record)
    {
        var timestamp = BinaryPrimitives.ReadInt64BigEndian(record.AsSpan(8, 8));

        _recent.Enqueue(new AuditEntry(
            BinaryPrimitives.ReadInt64BigEndian(record.AsSpan(0, 8)),
            DateTimeOffset.FromUnixTimeMilliseconds(timestamp),
            (DurableAuditKind)BinaryPrimitives.ReadInt32BigEndian(record.AsSpan(16, 4)),
            new Guid(record.AsSpan(20, 16)),
            new Guid(record.AsSpan(36, 16)),
            new Guid(record.AsSpan(52, 16)),
            DecodeDetailCode(record.AsSpan(68, DetailCodeLength)),
            (byte[])ZeroHash.Clone(),
            record.AsSpan(HashOffset, HashLength).ToArray()));

        TrimRecent();
    }

    private void TrimRecent()
    {
        while (_recent.Count > _recentCapacity)
        {
            _recent.Dequeue();
        }
    }

    private static byte[] BuildRecord(
        long sequence,
        DateTimeOffset recordedAtUtc,
        DurableAuditKind kind,
        AuditSubject subject,
        string detailCode,
        byte[] previousHash)
    {
        var record = new byte[RecordSize];

        BinaryPrimitives.WriteInt64BigEndian(record.AsSpan(0, 8), sequence);
        BinaryPrimitives.WriteInt64BigEndian(record.AsSpan(8, 8), recordedAtUtc.ToUnixTimeMilliseconds());
        BinaryPrimitives.WriteInt32BigEndian(record.AsSpan(16, 4), (int)kind);

        if (!subject.CorrelationId.TryWriteBytes(record.AsSpan(20, 16)) ||
            !subject.SubjectId.TryWriteBytes(record.AsSpan(36, 16)) ||
            !subject.SecondaryId.TryWriteBytes(record.AsSpan(52, 16)))
        {
            throw new InvalidOperationException("Unable to encode audit subject.");
        }

        EncodeDetailCode(detailCode, record.AsSpan(68, DetailCodeLength));
        previousHash.AsSpan(0, HashLength).CopyTo(record.AsSpan(92, HashLength));
        SHA256.HashData(record.AsSpan(0, HashOffset), record.AsSpan(HashOffset, HashLength));
        return record;
    }

    /// <summary>
    /// A detail code is a machine code, not free text: it must already be lower-case
    /// <c>[a-z0-9._-]</c> and fit the fixed field. Anything else is refused rather than
    /// sanitized, because sanitizing would let caller text silently become audit content.
    /// </summary>
    public static bool IsValidDetailCode(string? detailCode)
    {
        if (string.IsNullOrEmpty(detailCode) || detailCode.Length > DetailCodeLength)
        {
            return false;
        }

        foreach (var c in detailCode)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '-'))
            {
                return false;
            }

            if (char.IsAsciiLetter(c) && char.IsUpper(c))
            {
                return false;
            }
        }

        return true;
    }

    private static void EncodeDetailCode(string detailCode, Span<byte> destination)
    {
        destination.Clear();

        if (!IsValidDetailCode(detailCode))
        {
            throw new ArgumentException("The audit detail code is not a valid machine code.", nameof(detailCode));
        }

        for (var index = 0; index < detailCode.Length; index++)
        {
            destination[index] = (byte)detailCode[index];
        }
    }

    private static string DecodeDetailCode(ReadOnlySpan<byte> source)
    {
        var builder = new StringBuilder(DetailCodeLength);

        foreach (var b in source)
        {
            if (b == 0)
            {
                break;
            }

            builder.Append((char)b);
        }

        return builder.ToString();
    }

    private static string NormalizeDetailCode(string detailCode) =>
        IsValidDetailCode(detailCode) ? detailCode : string.Empty;

    private AuditVerificationResult Latch(string code, string diagnostic)
    {
        _latchCode = code;
        _diagnostic = diagnostic;
        return AuditVerificationResult.Failed(code, diagnostic);
    }

    private readonly record struct AuditEntry(
        long Sequence,
        DateTimeOffset RecordedAtUtc,
        DurableAuditKind Kind,
        Guid CorrelationId,
        Guid SubjectId,
        Guid SecondaryId,
        string DetailCode,
        byte[] PreviousHash,
        byte[] RecordHash);
}
