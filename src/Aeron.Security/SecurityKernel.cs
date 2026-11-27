using System.Text.Json;
using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// File-backed audit checkpoint anchor with a deliberately explicit protection contract.
///
/// The checkpoint is written atomically (temp file + flush + rename) and holds only the
/// last verified sequence and chain head hash. Its protection level is reported honestly:
/// a file under the same current-user ACL as the audit ledger is <em>not</em> independent.
/// Real rollback resistance requires a checkpoint whose write authority the ledger cannot
/// use (separate OS account, separate device, or hardware-backed store). Until the Owner
/// chooses that mechanism (decision register D2-07), this store reports
/// <see cref="ProtectionLevel.WeakSameUser"/> and the security status surface says so.
/// </summary>
public sealed class FileAuditCheckpointStore : IAuditCheckpointStore
{
    public enum ProtectionLevel
    {
        WeakSameUser = 0,
        Independent = 1
    }

    private readonly Lock _sync = new();
    private readonly string _path;

    public FileAuditCheckpointStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _path = filePath;
    }

    public ProtectionLevel Protection => ProtectionLevel.WeakSameUser;

    public bool TryRead(out long sequence, out byte[] headHash)
    {
        sequence = 0;
        headHash = [];

        try
        {
            lock (_sync)
            {
                if (!File.Exists(_path))
                {
                    // Never silently initialize: a missing anchor must read as missing.
                    return false;
                }

                var bytes = File.ReadAllBytes(_path);

                if (bytes.Length != 40)
                {
                    return false;
                }

                sequence = BitConverter.ToInt64(bytes, 0);
                headHash = bytes[8..40];
                return sequence >= 0 && headHash.Length == 32;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Initializes the genesis anchor (sequence 0, zero hash) for a brand-new installation.
    /// This is the only write that may create the anchor from nothing, and it is refused
    /// when an anchor already exists so it can never be used to reset a live chain.
    /// </summary>
    public bool TryInitializeGenesis()
    {
        try
        {
            lock (_sync)
            {
                if (File.Exists(_path))
                {
                    return false;
                }
            }
        }
        catch (Exception)
        {
            return false;
        }

        return TryWrite(0, new byte[32]);
    }

    public bool TryWrite(long sequence, ReadOnlySpan<byte> headHash)
    {
        if (sequence < 0 || headHash.Length != 32)
        {
            return false;
        }

        var buffer = new byte[40];
        BitConverter.TryWriteBytes(buffer, sequence);
        headHash.CopyTo(buffer.AsSpan(8));

        var tempPath = _path + ".tmp";

        try
        {
            lock (_sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(buffer);
                    stream.Flush(flushToDisk: true);
                }

                File.Move(tempPath, _path, overwrite: true);
                return true;
            }
        }
        catch (Exception)
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (Exception)
            {
                // Best-effort cleanup.
            }

            return false;
        }
    }
}

/// <summary>
/// Composes the M2 security kernel from its parts and exposes the only supported
/// production entry point. Construction is fail-closed: if ledger verification fails at
/// startup the kernel is still constructed but reports
/// <see cref="IsOperational"/> = false and denies every operation.
/// </summary>
public sealed class SecurityKernel : IDisposable
{
    private readonly Lock _sync = new();
    private readonly IClock _clock;

    private SecurityKernel(
        IdentityAuthority identityAuthority,
        DurableAuditLedger audit,
        DurableApprovalStore approvalStore,
        OperationBinding binding,
        InMemoryPermissionStore permissions,
        PreemptionState preemption,
        ApprovalEngine approvals,
        ActionDispatcher dispatcher,
        ApproverTrustRegistry approvers,
        InMemoryAgentRegistry agentRegistry,
        AuditVerificationResult startupVerification,
        IClock clock)
    {
        IdentityAuthority = identityAuthority;
        Audit = audit;
        ApprovalStore = approvalStore;
        Binding = binding;
        Permissions = permissions;
        Preemption = preemption;
        Approvals = approvals;
        Dispatcher = dispatcher;
        Approvers = approvers;
        AgentRegistry = agentRegistry;
        StartupVerification = startupVerification;
        _clock = clock;
    }

    public IdentityAuthority IdentityAuthority { get; }

    public DurableAuditLedger Audit { get; }

    public DurableApprovalStore ApprovalStore { get; }

    public OperationBinding Binding { get; }

    public InMemoryPermissionStore Permissions { get; }

    public PreemptionState Preemption { get; }

    public ApprovalEngine Approvals { get; }

    public ActionDispatcher Dispatcher { get; }

    public ApproverTrustRegistry Approvers { get; }

    public InMemoryAgentRegistry AgentRegistry { get; }

    public AuditVerificationResult StartupVerification { get; }

    /// <summary>
    /// True only while the startup verification passed and no append or verification
    /// failure has latched the ledger. A kernel can never return to operational on its own.
    /// </summary>
    public bool IsOperational => StartupVerification.IsHealthy && Audit.IsHealthy;

    /// <summary>
    /// Constructs and verifies the kernel. The caller supplies the durable paths, the
    /// checkpoint anchor, and the platform primitives; nothing is defaulted implicitly in
    /// production composition.
    /// </summary>
    public static SecurityKernel Create(SecurityKernelOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // First run on a clean install: create the genesis anchor *before* verification.
        // This is not a silent repair; an existing anchor is never overwritten, and a
        // store that cannot be initialized simply fails verification below.
        if (!options.CheckpointStore.TryRead(out _, out _) &&
            options.CheckpointStore is FileAuditCheckpointStore fileCheckpoint)
        {
            _ = fileCheckpoint.TryInitializeGenesis();
        }

        var audit = new DurableAuditLedger(options.AuditFilePath, options.CheckpointStore, options.Clock);
        var startupVerification = audit.VerifyIntegrity();
        var permissions = new InMemoryPermissionStore();
        var approvers = new ApproverTrustRegistry();
        var registry = new InMemoryAgentRegistry();

        var identity = new IdentityAuthority(
            options.AccountSource,
            options.FactorPolicy,
            registry,
            options.SignatureVerifier,
            options.NonceStore,
            audit,
            options.Clock,
            options.SessionPolicy,
            SecurityPolicyEvaluator.Version);

        var binding = options.BindingKey is { Length: OperationBinding.KeyLength } key
            ? new OperationBinding(key)
            : OperationBinding.CreateVolatile();

        var preemption = new PreemptionState(audit, identity);
        var approvalStore = new DurableApprovalStore(options.ApprovalDirectory);

        var approvals = new ApprovalEngine(
            approvalStore,
            binding,
            identity,
            permissions,
            audit,
            preemption,
            approvers,
            options.Clock);

        var dispatcher = new ActionDispatcher(identity, approvals, permissions, audit, preemption, options.Clock);

        // Record the startup outcome durably. A failed append latches the ledger, which is
        // the correct outcome: an unverifiable start must not claim operational status.
        if (startupVerification.IsHealthy)
        {
            audit.TryAppend(
                DurableAuditKind.StartupVerified,
                AuditSubject.For(Guid.NewGuid()),
                SecurityCodes.StartupVerified);
        }

        return new SecurityKernel(
            identity,
            audit,
            approvalStore,
            binding,
            permissions,
            preemption,
            approvals,
            dispatcher,
            approvers,
            registry,
            startupVerification,
            options.Clock);
    }

    public SecurityStatusSnapshot CaptureStatus()
    {
        lock (_sync)
        {
            var auditVerification = Audit.VerifyIntegrity();
            var pending = Approvals.ListOutstanding(32);
            var ownerSubject = IdentityAuthority.EnrolledOwnerSubject ?? string.Empty;

            return new SecurityStatusSnapshot(
                OwnerEnrolled: IdentityAuthority.IsOwnerEnrolled,
                OwnerSessionActive: IdentityAuthority.HasActiveOwnerSession,
                OwnerSubjectDisplay: ownerSubject,
                PolicyVersion: SecurityPolicyEvaluator.M2PolicyVersion,
                AuditStatus: auditVerification.IsHealthy ? SecuritySubsystemStatus.Healthy : SecuritySubsystemStatus.Tampered,
                AuditLastSequence: auditVerification.LastSequence,
                AuditDiagnostic: auditVerification.Diagnostic,
                ApprovalStatus: auditVerification.IsHealthy ? SecuritySubsystemStatus.Healthy : SecuritySubsystemStatus.Unavailable,
                PendingApprovals: pending.Count,
                VaultStatus: SecuritySubsystemStatus.Unavailable,
                VaultAlgorithmSummary: "Not configured: the vault encryption/key-recovery policy is unresolved (decision register D2-05/D2-06).",
                EmergencyStopActive: Preemption.IsEmergencyStopActive,
                ActiveSessionCount: IdentityAuthority.ActiveSessionCount,
                RegisteredAgentCount: IdentityAuthority.RegisteredAgentCount,
                RevokedAgentCount: IdentityAuthority.RevokedAgentCount,
                CapturedAtUtc: _clock.UtcNow);
        }
    }

    public IReadOnlyList<SecurityAuditEntryView> ReadAuditTrail(int maxRecords) =>
        Audit.ReadRecent(maxRecords)
            .Select(static record => new SecurityAuditEntryView(
                record.Sequence,
                record.RecordedAtUtc,
                record.Kind.ToString(),
                record.DetailCode,
                record.CorrelationId.ToString("N")[..8]))
            .ToArray();

    public void Dispose()
    {
        Binding.Dispose();
    }
}

/// <summary>Explicit construction inputs for the kernel. No security-relevant value is defaulted silently.</summary>
public sealed class SecurityKernelOptions
{
    public required string AuditFilePath { get; init; }

    public required string ApprovalDirectory { get; init; }

    public required IAuditCheckpointStore CheckpointStore { get; init; }

    public required IWindowsAccountIdentitySource AccountSource { get; init; }

    public required IOwnerFactorPolicy FactorPolicy { get; init; }

    public required IAgentSignatureVerifier SignatureVerifier { get; init; }

    public required INonceStore NonceStore { get; init; }

    public required IClock Clock { get; init; }

    public SessionPolicy SessionPolicy { get; init; } = SessionPolicy.Default;

    /// <summary>
    /// 32-byte binding key from the platform secret store. When null the kernel uses a
    /// volatile per-process key, and every approval becomes unverifiable on restart.
    /// </summary>
    public byte[]? BindingKey { get; init; }
}
