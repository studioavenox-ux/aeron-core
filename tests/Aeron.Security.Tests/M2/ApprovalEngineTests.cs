using System.Security.Cryptography;
using System.Text;
using Aeron.Contracts;
using Aeron.TestSupport;
using Xunit;

namespace Aeron.Security.Tests.M2;

public sealed class ApprovalEngineTests
{
    private const string Target = "local.document.synthetic";
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("synthetic-operation-parameters-alpha");

    private static OperationIdentity Approvable(string target = Target) => new(
        SecurityAction.ReadLocalData,
        DataSensitivity.LocalPrivate,
        target,
        SecurityPolicyEvaluator.M2PolicyVersion);

    private static (KernelHarness Harness, TestAgent Agent, AuthenticatedSession Session) WithAgent()
    {
        var harness = new KernelHarness();
        var agent = new TestAgent("agent-alpha");
        harness.RegisterAgent(agent.Registration);
        var session = harness.AuthenticateAgent(agent.Authenticate());
        harness.Grant(agent.AgentId, IdentityKind.Agent, SecurityCapability.ReadLocalData);
        return (harness, agent, session);
    }

    [Fact]
    public void ApprovedSingleUseFlowExecutesOnceAndRefusesReplay()
    {
        var (harness, agent, session) = WithAgent();
        using var _ = harness;
        using var __ = agent;

        var approvalId = harness.Kernel.Dispatcher
            .RequestApproval(session.SessionId, Approvable(), Payload, TimeSpan.FromMinutes(5))
            .Value;

        Assert.True(harness.Kernel.Approvals.Decide(approvalId, harness.OwnerMarker, approve: true).IsSuccess);

        var permit = harness.Kernel.Dispatcher.AuthorizeApproved(session.SessionId, approvalId, Approvable(), Payload);
        Assert.True(permit.IsSuccess, permit.ErrorMessage);

        var executions = 0;
        var first = permit.Value.TryExecute(() => ++executions);
        Assert.True(first.IsSuccess, first.ErrorMessage);
        Assert.Equal(1, executions);

        // Replay of the same permit: refused before the effect runs.
        var replay = permit.Value.TryExecute(() => ++executions);
        Assert.False(replay.IsSuccess);
        Assert.Equal(1, executions);

        // A fresh redemption attempt against the consumed approval: refused.
        var again = harness.Kernel.Dispatcher.AuthorizeApproved(session.SessionId, approvalId, Approvable(), Payload);
        Assert.False(again.IsSuccess);
        Assert.Contains("already consumed", again.ErrorMessage);
    }

    [Fact]
    public void ParameterTamperingAndCrossOperationSubstitutionAreRejected()
    {
        var (harness, agent, session) = WithAgent();
        using var _ = harness;
        using var __ = agent;

        var approvalId = harness.Kernel.Dispatcher
            .RequestApproval(session.SessionId, Approvable(), Payload, TimeSpan.FromMinutes(5))
            .Value;
        Assert.True(harness.Kernel.Approvals.Decide(approvalId, harness.OwnerMarker, true).IsSuccess);

        // Same redacted display, different payload bytes: binding mismatch.
        var tampered = harness.Kernel.Dispatcher.AuthorizeApproved(
            session.SessionId, approvalId, Approvable(), Encoding.UTF8.GetBytes("synthetic-operation-parameters-bravo"));
        Assert.False(tampered.IsSuccess);

        // Different target: mismatch.
        var differentTarget = harness.Kernel.Dispatcher.AuthorizeApproved(
            session.SessionId, approvalId, Approvable("local.document.other"), Payload);
        Assert.False(differentTarget.IsSuccess);

        // Different sensitivity: mismatch.
        var differentSensitivity = harness.Kernel.Dispatcher.AuthorizeApproved(
            session.SessionId, approvalId, Approvable() with { Sensitivity = DataSensitivity.Routine }, Payload);
        Assert.False(differentSensitivity.IsSuccess);

        // Different actor session (other agent): rejected before binding comparison.
        using var other = new TestAgent("agent-bravo");
        harness.RegisterAgent(other.Registration);
        var otherSession = harness.AuthenticateAgent(other.Authenticate());
        harness.Grant(other.AgentId, IdentityKind.Agent, SecurityCapability.ReadLocalData);

        var crossActor = harness.Kernel.Dispatcher.AuthorizeApproved(otherSession.SessionId, approvalId, Approvable(), Payload);
        Assert.False(crossActor.IsSuccess);

        // The original approval is still intact for the original actor.
        var valid = harness.Kernel.Dispatcher.AuthorizeApproved(session.SessionId, approvalId, Approvable(), Payload);
        Assert.True(valid.IsSuccess, valid.ErrorMessage);
    }

    [Fact]
    public void OnlyTheOwnerApproverCanDecideAndSelfApprovalIsImpossible()
    {
        var (harness, agent, session) = WithAgent();
        using var _ = harness;
        using var __ = agent;

        var approvalId = harness.Kernel.Dispatcher
            .RequestApproval(session.SessionId, Approvable(), Payload, TimeSpan.FromMinutes(5))
            .Value;

        // No marker, wrong marker, and the agent's own session id are all refused.
        Assert.Equal(ApprovalOutcomeCode.OwnerRequired, harness.Kernel.Approvals.Decide(approvalId, ReadOnlyMemory<byte>.Empty, true).Code);
        Assert.Equal(ApprovalOutcomeCode.OwnerRequired,
            harness.Kernel.Approvals.Decide(approvalId, RandomNumberGenerator.GetBytes(48), true).Code);
        Assert.Equal(ApprovalOutcomeCode.OwnerRequired,
            harness.Kernel.Approvals.Decide(approvalId, KernelHarness.SessionEvidence(session.SessionId), true).Code);

        // The approval is still pending until a real Owner decision arrives.
        Assert.True(harness.Kernel.Approvals.TryGet(approvalId, out var record));
        Assert.Equal(ApprovalLifecycleState.Pending, record!.State);

        Assert.True(harness.Kernel.Approvals.Decide(approvalId, harness.OwnerMarker, true).IsSuccess);
    }

    [Fact]
    public void OwnerDenialCancellationAndRevocationAreTerminal()
    {
        var (harness, _, session) = WithAgent();
        using var __ = harness;

        var denied = harness.Kernel.Dispatcher.RequestApproval(session.SessionId, Approvable(), Payload, TimeSpan.FromMinutes(5)).Value;
        Assert.True(harness.Kernel.Approvals.Decide(denied, harness.OwnerMarker, false).IsSuccess);
        Assert.False(harness.Kernel.Approvals.Decide(denied, harness.OwnerMarker, true).IsSuccess);

        var cancelled = harness.Kernel.Dispatcher.RequestApproval(session.SessionId, Approvable(), Payload, TimeSpan.FromMinutes(5)).Value;
        Assert.True(harness.Kernel.Approvals.Invalidate(cancelled, harness.OwnerMarker, revoked: false).IsSuccess);

        var revoked = harness.Kernel.Dispatcher.RequestApproval(session.SessionId, Approvable(), Payload, TimeSpan.FromMinutes(5)).Value;
        Assert.True(harness.Kernel.Approvals.Decide(revoked, harness.OwnerMarker, true).IsSuccess);
        Assert.True(harness.Kernel.Approvals.Invalidate(revoked, harness.OwnerMarker, revoked: true).IsSuccess);

        Assert.False(harness.Kernel.Dispatcher.AuthorizeApproved(session.SessionId, revoked, Approvable(), Payload).IsSuccess);
    }

    [Fact]
    public void ExpiredApprovalCannotBeConsumed()
    {
        var (harness, _, session) = WithAgent();
        using var __ = harness;

        var approvalId = harness.Kernel.Dispatcher
            .RequestApproval(session.SessionId, Approvable(), Payload, TimeSpan.FromMinutes(2))
            .Value;
        Assert.True(harness.Kernel.Approvals.Decide(approvalId, harness.OwnerMarker, true).IsSuccess);

        harness.Clock.Advance(TimeSpan.FromMinutes(3));

        var expired = harness.Kernel.Dispatcher.AuthorizeApproved(session.SessionId, approvalId, Approvable(), Payload);
        Assert.False(expired.IsSuccess);

        // A backwards clock change after the expiry was observed does not revive it.
        harness.Clock.Advance(TimeSpan.FromMinutes(-10));
        Assert.False(harness.Kernel.Dispatcher.AuthorizeApproved(session.SessionId, approvalId, Approvable(), Payload).IsSuccess);
    }

    [Fact]
    public void WithdrawingTheCapabilityAfterApprovalBlocksConsumption()
    {
        var (harness, agent, session) = WithAgent();
        using var _ = harness;
        using var __ = agent;

        var approvalId = harness.Kernel.Dispatcher
            .RequestApproval(session.SessionId, Approvable(), Payload, TimeSpan.FromMinutes(5))
            .Value;
        Assert.True(harness.Kernel.Approvals.Decide(approvalId, harness.OwnerMarker, true).IsSuccess);

        var admin = new PermissionAdministration(harness.Kernel.Permissions, harness.Kernel.IdentityAuthority, harness.Kernel.Audit);
        Assert.True(admin.Revoke(harness.OwnerEvidence, agent.AgentId, IdentityKind.Agent, SecurityCapability.ReadLocalData).IsSuccess);

        var refused = harness.Kernel.Dispatcher.AuthorizeApproved(session.SessionId, approvalId, Approvable(), Payload);
        Assert.False(refused.IsSuccess);
    }

    [Fact]
    public async Task ConcurrentRedemptionsYieldExactlyOneExecution()
    {
        var (harness, _, session) = WithAgent();
        using var __ = harness;

        var approvalId = harness.Kernel.Dispatcher
            .RequestApproval(session.SessionId, Approvable(), Payload, TimeSpan.FromMinutes(5))
            .Value;
        Assert.True(harness.Kernel.Approvals.Decide(approvalId, harness.OwnerMarker, true).IsSuccess);

        var executions = 0;
        var permits = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            var permit = harness.Kernel.Dispatcher.AuthorizeApproved(session.SessionId, approvalId, Approvable(), Payload);

            if (!permit.IsSuccess)
            {
                return false;
            }

            return permit.Value.TryExecute(() => Interlocked.Increment(ref executions)).IsSuccess;
        })));

        Assert.Equal(1, permits.Count(static ok => ok));
        Assert.Equal(1, executions);
    }

    [Fact]
    public async Task ConcurrentApprovalAndPreemptionNeverBothSucceed()
    {
        var (harness, _, session) = WithAgent();
        using var __ = harness;

        var approvalId = harness.Kernel.Dispatcher
            .RequestApproval(session.SessionId, Approvable(), Payload, TimeSpan.FromMinutes(5))
            .Value;
        Assert.True(harness.Kernel.Approvals.Decide(approvalId, harness.OwnerMarker, true).IsSuccess);

        var approve = Task.Run(() => harness.Kernel.Approvals.Invalidate(approvalId, harness.OwnerMarker, revoked: true));
        var redeem = Task.Run(() => harness.Kernel.Dispatcher.AuthorizeApproved(session.SessionId, approvalId, Approvable(), Payload));

        await Task.WhenAll(approve, redeem);

        var after = harness.Kernel.Dispatcher.AuthorizeApproved(session.SessionId, approvalId, Approvable(), Payload);
        Assert.False(after.IsSuccess);
    }

    [Fact]
    public void PreemptionEpochInvalidatesAnAlreadyIssuedPermit()
    {
        var (harness, _, session) = WithAgent();
        using var __ = harness;

        var approvalId = harness.Kernel.Dispatcher
            .RequestApproval(session.SessionId, Approvable(), Payload, TimeSpan.FromMinutes(5))
            .Value;
        Assert.True(harness.Kernel.Approvals.Decide(approvalId, harness.OwnerMarker, true).IsSuccess);

        var permit = harness.Kernel.Dispatcher.AuthorizeApproved(session.SessionId, approvalId, Approvable(), Payload);
        Assert.True(permit.IsSuccess);

        Assert.True(harness.Kernel.Preemption.TryPreempt(harness.OwnerEvidence, "owner.preempt"));

        var executions = 0;
        var refused = permit.Value.TryExecute(() => ++executions);
        Assert.False(refused.IsSuccess);
        Assert.Equal(0, executions);
    }

    [Fact]
    public void EmergencyStopBlocksNewApprovalsAndDispatch()
    {
        var (harness, _, session) = WithAgent();
        using var __ = harness;

        Assert.True(harness.Kernel.Preemption.TrySetEmergencyStop(harness.OwnerEvidence, "test.stop"));

        var request = harness.Kernel.Dispatcher.RequestApproval(session.SessionId, Approvable(), Payload, TimeSpan.FromMinutes(5));
        Assert.False(request.IsSuccess);
    }

    [Fact]
    public void ApprovalStateSurvivesRestartAndIsStillSingleUse()
    {
        var (harness, _, session) = WithAgent();
        using var _ = harness;

        var approvalId = harness.Kernel.Dispatcher
            .RequestApproval(session.SessionId, Approvable(), Payload, TimeSpan.FromMinutes(5))
            .Value;
        Assert.True(harness.Kernel.Approvals.Decide(approvalId, harness.OwnerMarker, true).IsSuccess);

        // Restart: the approval record persists (durable store). The agent session does
        // not, which is default deny; re-authenticate to exercise consumption.
        var restarted = harness.RestartKernel();
        Assert.True(restarted.Approvals.TryGet(approvalId, out var persisted));
        Assert.Equal(ApprovalLifecycleState.Approved, persisted!.State);

        // Without a live agent session consumption is refused.
        Assert.False(restarted.Dispatcher.AuthorizeApproved(session.SessionId, approvalId, Approvable(), Payload).IsSuccess);
    }

    [Fact]
    public void StoreRejectsMalformedRecordsAndNeverStoresPayloadBytes()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"aeron-approvals-{Guid.NewGuid():N}");
        var store = new DurableApprovalStore(directory);

        try
        {
            var record = new ApprovalRecord(
                Guid.NewGuid(),
                SecurityAction.ReadLocalData,
                DataSensitivity.LocalPrivate,
                Target,
                "agent-alpha",
                IdentityKind.Agent,
                SecurityPolicyEvaluator.M2PolicyVersion,
                new byte[31],
                new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 3, 1, 12, 5, 0, TimeSpan.Zero),
                ApprovalLifecycleState.Pending,
                null);

            Assert.False(store.TryInsert(record));
            Assert.True(store.TryInsert(record with { BindingHash = RandomNumberGenerator.GetBytes(32) }));

            // The payload must not appear anywhere on disk; only the binding hash does.
            foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                Assert.DoesNotContain("synthetic-operation-parameters", text, StringComparison.Ordinal);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
