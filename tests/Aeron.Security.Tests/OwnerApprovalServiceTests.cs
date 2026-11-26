using System.Text;
using Aeron.Contracts;
using Xunit;

namespace Aeron.Security.Tests;

public sealed class OwnerApprovalServiceTests
{
    private static readonly byte[] Owner = [1];
    private static readonly byte[] Agent = [2];
    private static readonly byte[] OtherAgent = [3];
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("synthetic-operation-alpha");
    private static readonly SecurityRequest Request = new(SecurityAction.DiscloseData, DataSensitivity.LocalPrivate, PolicyDecisionEngine.CurrentPolicyVersion);

    private sealed class Identities : ITrustedIdentityProvider
    {
        public bool TryAuthenticate(ReadOnlyMemory<byte> evidence, out VerifiedIdentity? identity)
        {
            identity = evidence.Span.SequenceEqual(Owner) ? new VerifiedIdentity("owner", IdentityKind.Owner)
                : evidence.Span.SequenceEqual(Agent) ? new VerifiedIdentity("agent", IdentityKind.Agent)
                : evidence.Span.SequenceEqual(OtherAgent) ? new VerifiedIdentity("other", IdentityKind.Agent)
                : null;
            return identity is not null;
        }
    }

    private sealed class ChangingIdentity : ITrustedIdentityProvider
    {
        public IdentityKind Kind { get; set; } = IdentityKind.Agent;
        public bool TryAuthenticate(ReadOnlyMemory<byte> evidence, out VerifiedIdentity? identity)
        {
            identity = evidence.Span.SequenceEqual(Owner)
                ? new VerifiedIdentity("owner", IdentityKind.Owner)
                : new VerifiedIdentity("shared-subject", Kind);
            return true;
        }
    }

    private sealed class Grants : IPermissionGrantStore
    {
        public bool Enabled { get; set; } = true;
        public bool IsGranted(VerifiedIdentity identity, SecurityAction action) => Enabled;
    }

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class Audit : IApprovalAuditSink
    {
        public bool Available { get; set; } = true;
        public List<ApprovalAuditEvent> Events { get; } = [];
        public bool TryAppend(ApprovalAuditEvent auditEvent)
        {
            if (!Available) return false;
            Events.Add(auditEvent);
            return true;
        }
    }

    [Fact]
    public void ApprovalRequiresOwnerAndBindsActorTargetAndExactParametersWithSingleUse()
    {
        var clock = new Clock();
        var audit = new Audit();
        using var service = new OwnerApprovalService(new Identities(), new Grants(), clock, audit);
        var created = service.Request(Request, Agent, "destination", Payload, clock.UtcNow.AddMinutes(1));
        Assert.Equal(ApprovalResultCode.Success, created.Code);
        Assert.Equal(ApprovalResultCode.Rejected, service.Decide(created.ApprovalId, Agent, true).Code);
        Assert.Equal(ApprovalResultCode.Success, service.Decide(created.ApprovalId, Owner, true).Code);
        Assert.Equal(ApprovalResultCode.Rejected, service.Consume(created.ApprovalId, Request, OtherAgent, "destination", Payload).Code);
        Assert.Equal(ApprovalResultCode.Rejected, service.Consume(created.ApprovalId, Request, Agent, "different", Payload).Code);
        Assert.Equal(ApprovalResultCode.Rejected, service.Consume(created.ApprovalId, Request, Agent, "destination", Encoding.UTF8.GetBytes("synthetic-operation-bravo")).Code);
        Assert.Equal(ApprovalResultCode.Success, service.Consume(created.ApprovalId, Request, Agent, "destination", Payload).Code);
        Assert.Equal(ApprovalResultCode.Rejected, service.Consume(created.ApprovalId, Request, Agent, "destination", Payload).Code);
        Assert.Contains(audit.Events, e => e.Kind == ApprovalEventKind.Consumed);
    }

    [Fact]
    public void ExpiryRevocationAndPermissionWithdrawalDenyConsumption()
    {
        var clock = new Clock();
        var grants = new Grants();
        using var service = new OwnerApprovalService(new Identities(), grants, clock, new Audit());
        var expired = service.Request(Request, Agent, "destination", Payload, clock.UtcNow.AddSeconds(1)).ApprovalId;
        Assert.Equal(ApprovalResultCode.Success, service.Decide(expired, Owner, true).Code);
        clock.UtcNow = clock.UtcNow.AddSeconds(2);
        Assert.Equal(ApprovalResultCode.Rejected, service.Consume(expired, Request, Agent, "destination", Payload).Code);
        clock.UtcNow = clock.UtcNow.AddSeconds(-2);
        Assert.Equal(ApprovalResultCode.Rejected, service.Consume(expired, Request, Agent, "destination", Payload).Code);

        var revoked = service.Request(Request, Agent, "destination", Payload, clock.UtcNow.AddMinutes(1)).ApprovalId;
        Assert.Equal(ApprovalResultCode.Success, service.Decide(revoked, Owner, true).Code);
        Assert.Equal(ApprovalResultCode.Success, service.Revoke(revoked, Owner).Code);
        Assert.Equal(ApprovalResultCode.Rejected, service.Consume(revoked, Request, Agent, "destination", Payload).Code);

        var withdrawn = service.Request(Request, Agent, "destination", Payload, clock.UtcNow.AddMinutes(1)).ApprovalId;
        Assert.Equal(ApprovalResultCode.Success, service.Decide(withdrawn, Owner, true).Code);
        grants.Enabled = false;
        Assert.Equal(ApprovalResultCode.Rejected, service.Consume(withdrawn, Request, Agent, "destination", Payload).Code);
    }

    [Fact]
    public void CancellationAndOwnerDenialAreTerminal()
    {
        var clock = new Clock();
        using var service = new OwnerApprovalService(new Identities(), new Grants(), clock, new Audit());
        var cancelled = service.Request(Request, Agent, "destination", Payload, clock.UtcNow.AddMinutes(1)).ApprovalId;
        Assert.Equal(ApprovalResultCode.Rejected, service.Cancel(cancelled, Agent).Code);
        Assert.Equal(ApprovalResultCode.Success, service.Cancel(cancelled, Owner).Code);
        Assert.Equal(ApprovalResultCode.Rejected, service.Decide(cancelled, Owner, true).Code);
        Assert.Equal(ApprovalResultCode.Rejected, service.Consume(cancelled, Request, Agent, "destination", Payload).Code);

        var denied = service.Request(Request, Agent, "destination", Payload, clock.UtcNow.AddMinutes(1)).ApprovalId;
        Assert.Equal(ApprovalResultCode.Success, service.Decide(denied, Owner, false).Code);
        Assert.Equal(ApprovalResultCode.Rejected, service.Decide(denied, Owner, true).Code);
        Assert.Equal(ApprovalResultCode.Rejected, service.Consume(denied, Request, Agent, "destination", Payload).Code);
    }

    [Fact]
    public void AuditFailureBlocksRequestsAndConsumptionEvenIfTheSinkReturns()
    {
        var clock = new Clock();
        var failedAudit = new Audit { Available = false };
        using var failedService = new OwnerApprovalService(new Identities(), new Grants(), clock, failedAudit);
        Assert.Equal(ApprovalResultCode.AuditUnavailable, failedService.Request(Request, Agent, "destination", Payload, clock.UtcNow.AddMinutes(1)).Code);
        failedAudit.Available = true;
        Assert.Equal(ApprovalResultCode.AuditUnavailable, failedService.Request(Request, Agent, "destination", Payload, clock.UtcNow.AddMinutes(1)).Code);

        var audit = new Audit();
        using var service = new OwnerApprovalService(new Identities(), new Grants(), clock, audit);
        var id = service.Request(Request, Agent, "destination", Payload, clock.UtcNow.AddMinutes(1)).ApprovalId;
        Assert.Equal(ApprovalResultCode.Success, service.Decide(id, Owner, true).Code);
        audit.Available = false;
        Assert.Equal(ApprovalResultCode.AuditUnavailable, service.Consume(id, Request, Agent, "destination", Payload).Code);
        Assert.Equal(ApprovalResultCode.AuditUnavailable, service.Revoke(id, Owner).Code);
        audit.Available = true;
        Assert.Equal(ApprovalResultCode.AuditUnavailable, service.Consume(id, Request, Agent, "destination", Payload).Code);
    }

    [Theory]
    [InlineData(IdentityKind.Agent, IdentityKind.Owner)]
    [InlineData(IdentityKind.Owner, IdentityKind.Agent)]
    public void ChangedIdentityKindCannotConsumeAnApprovalForTheSameSubject(IdentityKind original, IdentityKind changed)
    {
        var clock = new Clock();
        var identities = new ChangingIdentity { Kind = original };
        using var service = new OwnerApprovalService(identities, new Grants(), clock, new Audit());
        var id = service.Request(Request, Agent, "destination", Payload, clock.UtcNow.AddMinutes(1)).ApprovalId;
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(ApprovalResultCode.Success, service.Decide(id, Owner, true).Code);
        identities.Kind = changed;
        Assert.Equal(ApprovalResultCode.Rejected, service.Consume(id, Request, Agent, "destination", Payload).Code);
        var wrongVersion = Request with { PolicyVersion = "old-policy" };
        Assert.Equal(ApprovalResultCode.Rejected, service.Consume(id, wrongVersion, Agent, "destination", Payload).Code);
        identities.Kind = original;
        Assert.Equal(ApprovalResultCode.Rejected, service.Consume(id, Request, Agent, "destination", [9]).Code);
        Assert.Equal(ApprovalResultCode.Success, service.Consume(id, Request, Agent, "destination", Payload).Code);
        Assert.Equal(ApprovalResultCode.Rejected, service.Consume(id, Request, Agent, "destination", Payload).Code);
    }

    [Fact]
    public async Task ConcurrentConsumesHaveExactlyOneWinner()
    {
        var clock = new Clock();
        using var service = new OwnerApprovalService(new Identities(), new Grants(), clock, new Audit());
        var id = service.Request(Request, Agent, "destination", Payload, clock.UtcNow.AddMinutes(1)).ApprovalId;
        Assert.Equal(ApprovalResultCode.Success, service.Decide(id, Owner, true).Code);
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ =>
            Task.Run(() => service.Consume(id, Request, Agent, "destination", Payload))));
        Assert.Equal(1, results.Count(r => r.Code == ApprovalResultCode.Success));
        Assert.Equal(15, results.Count(r => r.Code == ApprovalResultCode.Rejected));
    }

    [Fact]
    public async Task ConcurrentConsumeAndRevocationNeverBothSucceed()
    {
        var clock = new Clock();
        using var service = new OwnerApprovalService(new Identities(), new Grants(), clock, new Audit());
        var id = service.Request(Request, Agent, "destination", Payload, clock.UtcNow.AddMinutes(1)).ApprovalId;
        Assert.Equal(ApprovalResultCode.Success, service.Decide(id, Owner, true).Code);
        var consume = Task.Run(() => service.Consume(id, Request, Agent, "destination", Payload));
        var revoke = Task.Run(() => service.Revoke(id, Owner));
        var results = await Task.WhenAll(consume, revoke);
        Assert.Equal(1, results.Count(r => r.Code == ApprovalResultCode.Success));
        Assert.Equal(ApprovalResultCode.Rejected, service.Consume(id, Request, Agent, "destination", Payload).Code);
    }
}
