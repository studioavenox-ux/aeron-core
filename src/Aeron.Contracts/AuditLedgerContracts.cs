namespace Aeron.Contracts;

// Fixed vocabulary only: no free text, credentials, identity evidence or operation payloads.
public enum SecurityAuditKind
{
    PermissionGranted = 1,
    PermissionRevoked = 2,
    SessionRevoked = 3,
    AgentRevoked = 4
}

/// <summary>
/// Trusted external protected, rollback-resistant checkpoint. A missing/unreadable
/// checkpoint must return false, never silently initialize. Atomic durable writes and
/// protection against ledger-file attackers are implementation responsibilities.
/// No production implementation is provided.
/// </summary>
public interface IProtectedAuditCheckpointStore
{
    bool TryRead(out long sequence, out byte[] hash);
    bool TryWrite(long sequence, ReadOnlySpan<byte> hash);
}

public interface ISecurityAuditSink
{
    bool TryAppend(SecurityAuditKind kind, Guid correlationId);
}
