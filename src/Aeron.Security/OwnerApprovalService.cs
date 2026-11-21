using System.Security.Cryptography;
using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// In-process, volatile approval state. Restart/disposal invalidates all approvals.
/// Requires trusted identity, grants, clock and durable audit providers; none are
/// configured by the production host yet. Consumption is not an external dispatch
/// boundary and must not by itself be treated as permission to perform a side effect.
/// </summary>
public sealed class OwnerApprovalService : IDisposable
{
    // An unavailable capacity is safer than unbounded memory growth. No retention
    // or deletion policy is implied by this volatile engineering limit.
    private const int MaxVolatileApprovals = 1024;

    private sealed class Entry(
        SecurityRequest request, string subject, byte[] binding, DateTimeOffset expiresAt)
    {
        public SecurityRequest Request { get; } = request;
        public string Subject { get; } = subject;
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
    private readonly OperationBinding _binding = new();
    private bool _disposed;
    private bool _insideAudit;
    private bool _auditUnhealthy;

    public OwnerApprovalService(ITrustedIdentityProvider identityProvider, IPermissionGrantStore grants, IClock clock, IApprovalAuditSink audit)
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
                var digest = _binding.Compute(id, request.Action, identity.SubjectId, target, request.PolicyVersion, exactParameters);
                if (!Record(id, ApprovalEventKind.Requested))
                {
                    CryptographicOperations.ZeroMemory(digest);
                    return AuditUnavailable();
                }

                _entries.Add(id, new Entry(request, identity.SubjectId, digest, expiresAt));
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
                if (!_entries.TryGetValue(id, out var entry) || entry.Status != ApprovalStatus.Approved || IsExpired(id, entry))
                {
                    return RejectAudited();
                }

                var decision = _policy.EvaluateAndIdentify(request, actorEvidence, out var identity);
                if (decision.Disposition != PolicyDisposition.RequireOwnerApproval || identity is null ||
                    request != entry.Request || !string.Equals(identity.SubjectId, entry.Subject, StringComparison.Ordinal) ||
                    !_binding.Verify(entry.Binding, id, request.Action, identity.SubjectId, target, request.PolicyVersion, exactParameters))
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

            // Revocation wins even if the audit sink is unhealthy. The caller is told
            // about audit failure rather than silently reverting the revocation.
            entry.Status = status;
            var recorded = Record(id, kind);
            return recorded && !_auditUnhealthy ? new ApprovalResult(ApprovalResultCode.Success, id) : AuditUnavailable();
        }
    }

    private bool IsExpired(Guid id, Entry entry)
    {
        if (entry.ExpiresAt <= _clock.UtcNow)
        {
            // Once observed expired, a later backwards wall-clock change cannot revive it.
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
