namespace Aeron.Contracts;

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
/// A trusted audit sink must durably append or return false. The application has no
/// production implementation yet; without one the approval service is unavailable.
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
