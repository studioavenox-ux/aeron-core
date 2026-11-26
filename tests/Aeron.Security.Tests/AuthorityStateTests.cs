using Aeron.Contracts;
using Xunit;

namespace Aeron.Security.Tests;

public sealed class AuthorityStateTests
{
    private static readonly byte[] Owner = [1];
    private static readonly byte[] Agent = [2];
    private static readonly VerifiedIdentity AgentIdentity = new("agent", IdentityKind.Agent);

    private sealed class IdentityProvider : ITrustedIdentityProvider
    {
        public bool TryAuthenticate(ReadOnlyMemory<byte> evidence, out VerifiedIdentity? identity)
        {
            identity = evidence.Span.SequenceEqual(Owner) ? new("owner", IdentityKind.Owner) :
                evidence.Span.SequenceEqual(Agent) ? AgentIdentity : null;
            return identity is not null;
        }
    }

    private sealed class Sink : ISecurityAuditSink
    {
        public bool Available { get; set; } = true;
        public List<SecurityAuditKind> Events { get; } = [];
        public bool TryAppend(SecurityAuditKind kind, Guid correlationId)
        {
            if (!Available) return false;
            Events.Add(kind);
            return correlationId != Guid.Empty;
        }
    }

    [Fact]
    public void OwnerProvisioningIsExplicitAndRevocationsAreRevalidated()
    {
        var audit = new Sink();
        var state = new AuthorityState(new IdentityProvider(), audit);
        Assert.True(state.TryOpenSession(Agent, out var session));
        Assert.False(state.IsCurrentlyGranted(session, Agent, SecurityAction.DiscloseData));
        Assert.False(state.Grant(Agent, AgentIdentity, SecurityAction.DiscloseData));
        Assert.True(state.Grant(Owner, AgentIdentity, SecurityAction.DiscloseData));
        Assert.True(state.IsCurrentlyGranted(session, Agent, SecurityAction.DiscloseData));
        Assert.False(state.IsCurrentlyGranted(session, Owner, SecurityAction.DiscloseData));
        Assert.False(state.IsCurrentlyGranted(session, Agent, SecurityAction.ModifyLocalData));
        Assert.True(state.RevokePermission(Owner, AgentIdentity, SecurityAction.DiscloseData));
        Assert.False(state.IsCurrentlyGranted(session, Agent, SecurityAction.DiscloseData));
        Assert.True(state.Grant(Owner, AgentIdentity, SecurityAction.DiscloseData));
        Assert.True(state.RevokeSession(Owner, session));
        Assert.False(state.IsCurrentlyGranted(session, Agent, SecurityAction.DiscloseData));
        Assert.True(state.TryOpenSession(Agent, out session));
        Assert.True(state.RevokeAgent(Owner, AgentIdentity));
        Assert.False(state.IsCurrentlyGranted(session, Agent, SecurityAction.DiscloseData));
        Assert.False(state.TryOpenSession(Agent, out _));
        Assert.False(state.Grant(Owner, AgentIdentity, SecurityAction.DiscloseData));
        Assert.Contains(SecurityAuditKind.AgentRevoked, audit.Events);
    }

    [Fact]
    public void AuditFailureLatchesDenyAndRevocationRemainsEffective()
    {
        var audit = new Sink();
        var state = new AuthorityState(new IdentityProvider(), audit);
        Assert.True(state.Grant(Owner, AgentIdentity, SecurityAction.DiscloseData));
        Assert.True(state.TryOpenSession(Agent, out var session));
        audit.Available = false;
        Assert.False(state.RevokePermission(Owner, AgentIdentity, SecurityAction.DiscloseData));
        audit.Available = true;
        Assert.False(state.IsCurrentlyGranted(session, Agent, SecurityAction.DiscloseData));
        Assert.False(state.Grant(Owner, AgentIdentity, SecurityAction.DiscloseData));
    }
}
