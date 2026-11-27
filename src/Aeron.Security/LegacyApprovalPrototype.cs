using System.Security.Cryptography;
using Aeron.Contracts;

namespace Aeron.Security;

public enum ApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Denied = 2,
    Cancelled = 3,
    Consumed = 4,
    Revoked = 5
}

public enum ApprovalEventKind
{
    Requested = 1,
    Approved = 2,
    Denied = 3,
    Cancelled = 4,
    Consumed = 5,
    Revoked = 6,
    Rejected = 7,
    Expired = 8
}

/// <summary>No request parameters, target, evidence, or credentials are included.</summary>
public sealed record ApprovalAuditEvent(Guid ApprovalId, ApprovalEventKind Kind);

/// <summary>
/// Legacy volatile approval sink used by the M2 prototype. The production kernel writes
/// through <see cref="IDurableAuditLedger"/>; this contract is retained only for the
/// Phase B prototype tests and must not be wired into production composition.
/// </summary>
public interface IApprovalAuditSink
{
    bool TryAppend(ApprovalAuditEvent auditEvent);
}

public enum ApprovalResultCode
{
    Success = 0,
    Rejected = 1,
    AuditUnavailable = 2
}

public sealed record ApprovalResult(ApprovalResultCode Code, Guid ApprovalId);

/// <summary>
/// Legacy in-process approval prototype retained for its regression suite only.
///
/// It is explicitly superseded by <see cref="ApprovalEngine"/> + <see cref="ActionDispatcher"/>:
/// this service does not persist across restarts, does not separate the approve role from
/// the requester role, and treats consumption as a local state change rather than dispatch
/// authority. Production composition must never wire it.
/// </summary>
public sealed class OwnerApprovalService : IDisposable
{
    private const int MaxVolatileApprovals = 1024;

    private sealed class Entry(
        SecurityRequest request, VerifiedIdentity identity, byte[] binding, DateTimeOffset expiresAt)
    {
        public SecurityRequest Request { get; } = request;
        public VerifiedIdentity Identity { get; } = identity;
        public byte[] Binding { get; } = binding;
        public DateTimeOffset ExpiresAt { get; } = expiresAt;
        public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    }

    private readonly Lock _sync = new();
    private readonly Dictionary<Guid, Entry> _entries = [];
    private readonly PolicyDecisionEngine _policy;
    private readonly OwnerAuthorityGuard _authority;
    private readonly IClock _clock;
    private readonly IApprovalAuditSink _audit;
    private readonly OperationBinding _binding = OperationBinding.CreateVolatile();
    private bool _disposed;
    private bool _insideAudit;
    private bool _auditUnhealthy;

    public OwnerApprovalService(
        ITrustedIdentityProvider identityProvider,
        IPermissionGrantStore grants,
        IClock clock,
        IApprovalAuditSink audit)
    {
        ArgumentNullException.ThrowIfNull(identityProvider);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(audit);
        _policy = new PolicyDecisionEngine(identityProvider, grants);
        _authority = new OwnerAuthorityGuard(identityProvider);
        _clock = clock;
        _audit = audit;
    }

    public ApprovalResult Request(
        SecurityRequest request, ReadOnlyMemory<byte> actorEvidence, string target,
        ReadOnlySpan<byte> exactParameters, DateTimeOffset expiresAt)
    {
        lock (_sync)
        {
            if (_disposed || _insideAudit)
            {
                return Rejected();
            }

            if (_auditUnhealthy)
            {
                return AuditUnavailable();
            }

            try
            {
                var decision = _policy.EvaluateAndIdentify(request, actorEvidence, out var identity);

                if (decision.Disposition != PolicyDisposition.RequireOwnerApproval || identity is null ||
                    expiresAt <= _clock.UtcNow || _entries.Count >= MaxVolatileApprovals)
                {
                    return RejectAudited();
                }

                var id = Guid.NewGuid();
                var digest = _binding.Compute(
                    id, request.Action, identity.SubjectId, identity.Kind, target, request.PolicyVersion, exactParameters);

                if (!Record(id, ApprovalEventKind.Requested))
                {
                    CryptographicOperations.ZeroMemory(digest);
                    return AuditUnavailable();
                }

                _entries.Add(id, new Entry(request, identity, digest, expiresAt));
                return new ApprovalResult(ApprovalResultCode.Success, id);
            }
            catch (Exception)
            {
                return RejectAudited();
            }
        }
    }

    public ApprovalResult Decide(Guid id, ReadOnlyMemory<byte> ownerEvidence, bool approve)
    {
        lock (_sync)
        {
            if (_disposed || _insideAudit)
            {
                return Rejected();
            }

            if (_auditUnhealthy)
            {
                return AuditUnavailable();
            }

            try
            {
                if (!_authority.VerifyOwner(ownerEvidence) || !_entries.TryGetValue(id, out var entry) ||
                    entry.Status != ApprovalStatus.Pending || IsExpired(id, entry))
                {
                    return RejectAudited();
                }

                var kind = approve ? ApprovalEventKind.Approved : ApprovalEventKind.Denied;

                if (!Record(id, kind))
                {
                    return AuditUnavailable();
                }

                entry.Status = approve ? ApprovalStatus.Approved : ApprovalStatus.Denied;
                return new ApprovalResult(ApprovalResultCode.Success, id);
            }
            catch (Exception)
            {
                return RejectAudited();
            }
        }
    }

    public ApprovalResult Cancel(Guid id, ReadOnlyMemory<byte> ownerEvidence) =>
        Invalidate(id, ownerEvidence, ApprovalStatus.Cancelled, ApprovalEventKind.Cancelled);

    public ApprovalResult Revoke(Guid id, ReadOnlyMemory<byte> ownerEvidence) =>
        Invalidate(id, ownerEvidence, ApprovalStatus.Revoked, ApprovalEventKind.Revoked);

    public ApprovalResult Consume(
        Guid id, SecurityRequest request, ReadOnlyMemory<byte> actorEvidence,
        string target, ReadOnlySpan<byte> exactParameters)
    {
        lock (_sync)
        {
            if (_disposed || _insideAudit)
            {
                return Rejected();
            }

            if (_auditUnhealthy)
            {
                return AuditUnavailable();
            }

            try
            {
                if (!_entries.TryGetValue(id, out var entry) ||
                    entry.Status != ApprovalStatus.Approved || IsExpired(id, entry))
                {
                    return RejectAudited();
                }

                var decision = _policy.EvaluateAndIdentify(request, actorEvidence, out var identity);

                if (decision.Disposition != PolicyDisposition.RequireOwnerApproval || identity is null ||
                    request != entry.Request || identity != entry.Identity ||
                    !_binding.Verify(
                        entry.Binding, id, request.Action, identity.SubjectId, identity.Kind, target, request.PolicyVersion, exactParameters))
                {
                    return RejectAudited();
                }

                if (!Record(id, ApprovalEventKind.Consumed))
                {
                    return AuditUnavailable();
                }

                entry.Status = ApprovalStatus.Consumed;
                return new ApprovalResult(ApprovalResultCode.Success, id);
            }
            catch (Exception)
            {
                return RejectAudited();
            }
        }
    }

    private ApprovalResult Invalidate(Guid id, ReadOnlyMemory<byte> ownerEvidence, ApprovalStatus status, ApprovalEventKind kind)
    {
        lock (_sync)
        {
            if (_disposed || _insideAudit)
            {
                return Rejected();
            }

            if (!_authority.VerifyOwner(ownerEvidence) || !_entries.TryGetValue(id, out var entry) ||
                entry.Status is not (ApprovalStatus.Pending or ApprovalStatus.Approved))
            {
                return RejectAudited();
            }

            entry.Status = status;
            var recorded = Record(id, kind);
            return recorded && !_auditUnhealthy ? new ApprovalResult(ApprovalResultCode.Success, id) : AuditUnavailable();
        }
    }

    private bool IsExpired(Guid id, Entry entry)
    {
        if (entry.ExpiresAt <= _clock.UtcNow)
        {
            entry.Status = ApprovalStatus.Revoked;
            _ = Record(id, ApprovalEventKind.Expired);
            return true;
        }

        return false;
    }

    private bool Record(Guid id, ApprovalEventKind kind)
    {
        try
        {
            _insideAudit = true;
            var recorded = _audit.TryAppend(new ApprovalAuditEvent(id, kind)) && !_disposed;

            if (!recorded)
            {
                _auditUnhealthy = true;
            }

            return recorded;
        }
        catch (Exception)
        {
            _auditUnhealthy = true;
            return false;
        }
        finally
        {
            _insideAudit = false;
        }
    }

    private ApprovalResult RejectAudited() =>
        !_auditUnhealthy && Record(Guid.Empty, ApprovalEventKind.Rejected) ? Rejected() : AuditUnavailable();

    private static ApprovalResult Rejected() => new(ApprovalResultCode.Rejected, Guid.Empty);

    private static ApprovalResult AuditUnavailable() => new(ApprovalResultCode.AuditUnavailable, Guid.Empty);

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var entry in _entries.Values)
            {
                CryptographicOperations.ZeroMemory(entry.Binding);
            }

            _entries.Clear();
            _binding.Dispose();
        }
    }
}
