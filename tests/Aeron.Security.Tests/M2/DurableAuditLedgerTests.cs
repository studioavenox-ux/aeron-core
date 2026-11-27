using Aeron.Contracts;
using Aeron.TestSupport;
using Xunit;

namespace Aeron.Security.Tests.M2;

public sealed class DurableAuditLedgerTests : IDisposable
{
    private readonly string _root;

    public DurableAuditLedgerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"aeron-audit-{Guid.NewGuid():N}");
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

    private (DurableAuditLedger Ledger, FileAuditCheckpointStore Checkpoint, string Path) CreateLedger(TestClock? clock = null)
    {
        var path = Path.Combine(_root, "audit.auditlog");
        var checkpoint = new FileAuditCheckpointStore(Path.Combine(_root, "audit.head"));
        var ledger = new DurableAuditLedger(path, checkpoint, clock ?? new TestClock());
        return (ledger, checkpoint, path);
    }

    private static void SeedCheckpoint(FileAuditCheckpointStore checkpoint) =>
        Assert.True(checkpoint.TryWrite(0, new byte[32]));

    [Fact]
    public void MissingCheckpointFailsClosedInsteadOfInitializingSilently()
    {
        var (ledger, _, _) = CreateLedger();

        Assert.False(ledger.VerifyIntegrity().IsHealthy);
        Assert.False(ledger.TryAppend(DurableAuditKind.StartupVerified, AuditSubject.For(Guid.NewGuid()), SecurityCodes.StartupVerified));
        Assert.False(ledger.IsHealthy);
    }

    [Fact]
    public void AppendsChainAndVerifyAgainstTheCheckpoint()
    {
        var (ledger, checkpoint, _) = CreateLedger();
        SeedCheckpoint(checkpoint);

        Assert.True(ledger.VerifyIntegrity().IsHealthy);

        for (var index = 0; index < 5; index++)
        {
            Assert.True(ledger.TryAppend(DurableAuditKind.PermissionGranted, AuditSubject.For(Guid.NewGuid()), SecurityCodes.PermissionGranted));
        }

        var verification = ledger.VerifyIntegrity();
        Assert.True(verification.IsHealthy);
        Assert.Equal(5L, verification.LastSequence);
        Assert.Equal(5, ledger.ReadRecent(10).Count);
    }

    [Fact]
    public void ModifyReorderAndDeleteOfAnInteriorRecordAreDetected()
    {
        var (ledger, checkpoint, path) = CreateLedger();
        SeedCheckpoint(checkpoint);

        for (var index = 0; index < 4; index++)
        {
            Assert.True(ledger.TryAppend(DurableAuditKind.PermissionGranted, AuditSubject.For(Guid.NewGuid()), SecurityCodes.PermissionGranted));
        }

        var original = File.ReadAllBytes(path);

        // Flip one byte inside the payload of record 2.
        var modified = (byte[])original.Clone();
        modified[DurableAuditLedger.RecordSize * 2 + 40] ^= 0x01;
        File.WriteAllBytes(path, modified);
        Assert.False(new DurableAuditLedger(path, checkpoint, new TestClock()).VerifyIntegrity().IsHealthy);

        // Swap two records.
        var swapped = (byte[])original.Clone();
        var recordOne = original.AsSpan(0, DurableAuditLedger.RecordSize).ToArray();
        var recordTwo = original.AsSpan(DurableAuditLedger.RecordSize, DurableAuditLedger.RecordSize).ToArray();
        recordTwo.CopyTo(swapped.AsSpan(0));
        recordOne.CopyTo(swapped.AsSpan(DurableAuditLedger.RecordSize));
        File.WriteAllBytes(path, swapped);
        Assert.False(new DurableAuditLedger(path, checkpoint, new TestClock()).VerifyIntegrity().IsHealthy);

        // Delete an interior record.
        var removed = original.AsSpan(0, DurableAuditLedger.RecordSize).ToArray()
            .Concat(original.AsSpan(DurableAuditLedger.RecordSize * 2).ToArray())
            .ToArray();
        File.WriteAllBytes(path, removed);
        Assert.False(new DurableAuditLedger(path, checkpoint, new TestClock()).VerifyIntegrity().IsHealthy);
    }

    [Fact]
    public void TruncationAndWholeFileReplacementAreDetectedViaTheCheckpoint()
    {
        var (ledger, checkpoint, path) = CreateLedger();
        SeedCheckpoint(checkpoint);

        for (var index = 0; index < 3; index++)
        {
            Assert.True(ledger.TryAppend(DurableAuditKind.PermissionGranted, AuditSubject.For(Guid.NewGuid()), SecurityCodes.PermissionGranted));
        }

        var full = File.ReadAllBytes(path);

        // Truncate to two records while the checkpoint still anchors three.
        File.WriteAllBytes(path, full.AsSpan(0, DurableAuditLedger.RecordSize * 2).ToArray());
        var truncated = new DurableAuditLedger(path, checkpoint, new TestClock()).VerifyIntegrity();
        Assert.False(truncated.IsHealthy);
        Assert.Equal(DurableAuditLedger.FailureTruncated, truncated.FailureCode);

        // Replace the whole file with a different (self-consistent) chain.
        var forgedCheckpoint = new FileAuditCheckpointStore(Path.Combine(_root, "forged.head"));
        SeedCheckpoint(forgedCheckpoint);
        var forgedPath = Path.Combine(_root, "forged.auditlog");
        var forged = new DurableAuditLedger(forgedPath, forgedCheckpoint, new TestClock());
        Assert.True(forged.TryAppend(DurableAuditKind.PermissionRevoked, AuditSubject.For(Guid.NewGuid()), SecurityCodes.PermissionRevoked));

        File.WriteAllBytes(path, File.ReadAllBytes(forgedPath));
        var replaced = new DurableAuditLedger(path, checkpoint, new TestClock()).VerifyIntegrity();
        Assert.False(replaced.IsHealthy);
        Assert.Equal(DurableAuditLedger.FailureCheckpointMismatch, replaced.FailureCode);
    }

    [Fact]
    public void UnanchoredTailFromACrashRequiresExplicitRecoveryAndIsNeverAutoTrusted()
    {
        var (ledger, checkpoint, path) = CreateLedger();
        SeedCheckpoint(checkpoint);

        Assert.True(ledger.TryAppend(DurableAuditKind.PermissionGranted, AuditSubject.For(Guid.NewGuid()), SecurityCodes.PermissionGranted));

        // Simulate a crash between the file write and the checkpoint advance: append a
        // record behind the ledger's back without moving the anchor.
        var hiddenClock = new TestClock();
        var shadow = new DurableAuditLedger(path, new InMemoryNeverWriteCheckpoint(checkpoint), hiddenClock);
        Assert.True(shadow.VerifyIntegrity().IsHealthy);
        Assert.True(shadow.TryAppendRawForTest(DurableAuditKind.SessionOpened, SecurityCodes.SessionOpened));

        // Normal startup refuses the unanchored record.
        var restarted = new DurableAuditLedger(path, checkpoint, new TestClock());
        var verification = restarted.VerifyIntegrity();
        Assert.False(verification.IsHealthy);
        Assert.Equal(DurableAuditLedger.FailureUnanchoredTail, verification.FailureCode);
        Assert.False(restarted.TryAppend(DurableAuditKind.PermissionGranted, AuditSubject.For(Guid.NewGuid()), SecurityCodes.PermissionGranted));

        // Owner-visible recovery accepts the single verified tail, re-anchors, and records
        // the recovery. Only then does appending resume.
        var recovered = restarted.AttemptRecovery();
        Assert.True(recovered.IsHealthy, recovered.Diagnostic);
        Assert.True(restarted.TryAppend(DurableAuditKind.PermissionRevoked, AuditSubject.For(Guid.NewGuid()), SecurityCodes.PermissionRevoked));
        Assert.True(restarted.VerifyIntegrity().IsHealthy);
    }

    [Fact]
    public void CorruptionLatchesTheLedgerAndRecoveryNeverRepairsSilently()
    {
        var (ledger, checkpoint, path) = CreateLedger();
        SeedCheckpoint(checkpoint);
        Assert.True(ledger.TryAppend(DurableAuditKind.PermissionGranted, AuditSubject.For(Guid.NewGuid()), SecurityCodes.PermissionGranted));

        var corrupted = File.ReadAllBytes(path);
        corrupted[100] ^= 0xFF;
        File.WriteAllBytes(path, corrupted);

        var restarted = new DurableAuditLedger(path, checkpoint, new TestClock());
        Assert.False(restarted.VerifyIntegrity().IsHealthy);
        Assert.False(restarted.IsHealthy);

        // Recovery refuses to repair a broken chain.
        Assert.False(restarted.AttemptRecovery().IsHealthy);
    }

    [Fact]
    public void FreeTextAndSecretShapedDetailCodesCannotBeStored()
    {
        var (ledger, checkpoint, path) = CreateLedger();
        SeedCheckpoint(checkpoint);

        // Free text is refused outright, never sanitized into audit content.
        Assert.False(ledger.TryAppend(
            DurableAuditKind.PermissionGranted,
            AuditSubject.For(Guid.NewGuid()),
            "PASSWORD=synthetic-secret-value \r\n injection attempt"));

        Assert.False(DurableAuditLedger.IsValidDetailCode("PASSWORD=x"));
        Assert.False(DurableAuditLedger.IsValidDetailCode("UPPER"));
        Assert.False(DurableAuditLedger.IsValidDetailCode(new string('a', DurableAuditLedger.DetailCodeLength + 1)));
        Assert.False(DurableAuditLedger.IsValidDetailCode(string.Empty));
        Assert.False(DurableAuditLedger.IsValidDetailCode(null));

        Assert.True(DurableAuditLedger.IsValidDetailCode(SecurityCodes.StartupVerified));
        Assert.True(DurableAuditLedger.IsValidDetailCode("audit.recovered"));
        Assert.True(DurableAuditLedger.IsValidDetailCode(new string('a', DurableAuditLedger.DetailCodeLength)));

        // Every production code must fit the fixed field and the machine-code alphabet.
        Assert.All(TestCodes.AllSecurityCodes, static code =>
        {
            Assert.True(
                DurableAuditLedger.IsValidDetailCode(code),
                $"SecurityCodes value '{code}' does not fit the audit detail field.");
        });

        Assert.True(ledger.TryAppend(
            DurableAuditKind.PermissionGranted,
            AuditSubject.For(Guid.NewGuid()),
            SecurityCodes.PermissionGranted));

        var record = ledger.ReadRecent(1).Single();
        Assert.Equal(SecurityCodes.PermissionGranted, record.DetailCode);
    }

    [Fact]
    public void KernelRefusesToRunWhenTheChainIsTampered()
    {
        using var harness = new KernelHarness();
        var auditPath = Path.Combine(harness.Root, "audit.auditlog");

        Assert.True(harness.Kernel.IsOperational);
        var bytes = File.ReadAllBytes(auditPath);
        Assert.True(bytes.Length >= DurableAuditLedger.RecordSize);
        bytes[50] ^= 0x01;
        File.WriteAllBytes(auditPath, bytes);

        var restarted = harness.RestartKernel();
        Assert.False(restarted.IsOperational);

        // Every authorization path is denied while the ledger is unhealthy.
        Assert.False(restarted.Dispatcher.ExecuteDirect(
            Guid.NewGuid(),
            new OperationIdentity(SecurityAction.ReadApplicationState, DataSensitivity.Routine, "aeron.status", SecurityPolicyEvaluator.M2PolicyVersion),
            static () => "ok").IsSuccess);
    }

    private sealed class InMemoryNeverWriteCheckpoint : IAuditCheckpointStore
    {
        private long _sequence;
        private byte[] _hash;

        public InMemoryNeverWriteCheckpoint(FileAuditCheckpointStore source)
        {
            Assert.True(source.TryRead(out _sequence, out _hash));
        }

        public bool TryRead(out long sequence, out byte[] headHash)
        {
            sequence = _sequence;
            headHash = _hash.ToArray();
            return true;
        }

        public bool TryWrite(long sequence, ReadOnlySpan<byte> headHash)
        {
            _sequence = sequence;
            _hash = headHash.ToArray();
            return true;
        }
    }
}

internal static class DurableAuditLedgerTestExtensions
{
    /// <summary>Appends a raw record without advancing the (wrapping) checkpoint, simulating a crash.</summary>
    public static bool TryAppendRawForTest(this DurableAuditLedger ledger, DurableAuditKind kind, string detailCode) =>
        ledger.TryAppend(kind, AuditSubject.For(Guid.NewGuid(), Guid.NewGuid()), detailCode);
}

/// <summary>Reflects every declared <see cref="SecurityCodes"/> constant so the whole code
/// vocabulary is validated against the fixed-width audit field, not just the codes a happy
/// path happens to use.</summary>
internal static class TestCodes
{
    public static IReadOnlyList<string> AllSecurityCodes { get; } = typeof(SecurityCodes)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(static field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(static field => (string)field.GetRawConstantValue()!)
        .ToArray();
}
