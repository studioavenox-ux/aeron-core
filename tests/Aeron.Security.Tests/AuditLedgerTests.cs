using Aeron.Contracts;
using Xunit;

namespace Aeron.Security.Tests;

public sealed class AuditLedgerTests
{
    private sealed class Checkpoint : IProtectedAuditCheckpointStore
    {
        public long Sequence { get; set; }
        public byte[] Hash { get; set; } = new byte[32];
        public bool Available { get; set; } = true;
        public bool WriteAvailable { get; set; } = true;
        public bool TryRead(out long sequence, out byte[] hash)
        {
            sequence = Sequence;
            hash = Hash.ToArray();
            return Available;
        }
        public bool TryWrite(long sequence, ReadOnlySpan<byte> hash)
        {
            if (!Available || !WriteAvailable) return false;
            Sequence = sequence;
            Hash = hash.ToArray();
            return true;
        }
    }

    [Fact]
    public void ChainsEventsAndRejectsTamperingAndTruncation()
    {
        var path = Path.GetTempFileName();
        try
        {
            var checkpoint = new Checkpoint();
            var ledger = new AuditLedger(path, checkpoint);
            Assert.True(ledger.TryAppend(SecurityAuditKind.PermissionGranted, Guid.NewGuid()));
            Assert.True(ledger.TryAppend(SecurityAuditKind.AgentRevoked, Guid.NewGuid()));
            Assert.Equal(2, checkpoint.Sequence);
            var original = File.ReadAllBytes(path);
            original[12] ^= 1;
            File.WriteAllBytes(path, original);
            Assert.False(new AuditLedger(path, checkpoint).TryAppend(SecurityAuditKind.SessionRevoked, Guid.NewGuid()));
            File.WriteAllBytes(path, []);
            Assert.False(new AuditLedger(path, checkpoint).TryAppend(SecurityAuditKind.SessionRevoked, Guid.NewGuid()));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void CheckpointWriteFailureAndSequenceTamperingFailClosedAfterReopen()
    {
        var path = Path.GetTempFileName();
        try
        {
            var checkpoint = new Checkpoint { WriteAvailable = false };
            var ledger = new AuditLedger(path, checkpoint);
            Assert.False(ledger.TryAppend(SecurityAuditKind.PermissionGranted, Guid.NewGuid()));
            checkpoint.WriteAvailable = true;
            Assert.False(new AuditLedger(path, checkpoint).TryAppend(SecurityAuditKind.PermissionGranted, Guid.NewGuid()));
            var bytes = File.ReadAllBytes(path);
            bytes[7] = 2;
            File.WriteAllBytes(path, bytes);
            Assert.False(new AuditLedger(path, checkpoint).TryAppend(SecurityAuditKind.PermissionGranted, Guid.NewGuid()));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void MissingOrFailingCheckpointAndInvalidEventsFailClosed()
    {
        var path = Path.GetTempFileName();
        try
        {
            var checkpoint = new Checkpoint { Available = false };
            var ledger = new AuditLedger(path, checkpoint);
            Assert.False(ledger.TryAppend(SecurityAuditKind.PermissionGranted, Guid.NewGuid()));
            checkpoint.Available = true;
            Assert.False(ledger.TryAppend(SecurityAuditKind.PermissionGranted, Guid.NewGuid()));
            var fresh = new AuditLedger(path, checkpoint);
            Assert.False(fresh.TryAppend((SecurityAuditKind)999, Guid.NewGuid()));
            Assert.False(fresh.TryAppend(SecurityAuditKind.PermissionGranted, Guid.Empty));
            Assert.True(fresh.TryAppend(SecurityAuditKind.PermissionGranted, Guid.NewGuid()));
        }
        finally { File.Delete(path); }
    }
}
