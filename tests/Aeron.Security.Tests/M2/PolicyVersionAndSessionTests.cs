using System.Text;
using Aeron.Contracts;
using Aeron.TestSupport;
using Xunit;

namespace Aeron.Security.Tests.M2;

public sealed class PolicyVersionAndSessionTests
{
    [Fact]
    public void PolicyVersionValidationIsStrictAndBounded()
    {
        Assert.True(SecurityPolicyEvaluator.Version.IsValid);
        Assert.False(new SecurityPolicyVersion(string.Empty).IsValid);
        Assert.False(new SecurityPolicyVersion(new string('a', 100)).IsValid);
        Assert.False(new SecurityPolicyVersion("bad version!").IsValid);

        var evaluator = new SecurityPolicyEvaluator(new InMemoryPermissionStore());
        var operation = new OperationIdentity(
            SecurityAction.ReadApplicationState,
            DataSensitivity.Routine,
            "aeron.status",
            SecurityPolicyEvaluator.M2PolicyVersion);

        // An invalid declared version is a denial, never an implicit "current".
        Assert.Equal(
            PolicyDisposition.Deny,
            evaluator.Evaluate(new VerifiedActor("owner", IdentityKind.Owner), operation, new SecurityPolicyVersion("???"))
                .Disposition);
    }

    [Fact]
    public void SessionPolicyClampsOutOfRangeLifetimes()
    {
        Assert.Equal(SessionPolicy.MinimumLifetime, SessionPolicy.Create(TimeSpan.Zero, TimeSpan.Zero).OwnerSessionLifetime);
        Assert.Equal(SessionPolicy.MaximumLifetime, SessionPolicy.Create(TimeSpan.FromDays(30), TimeSpan.FromDays(30)).AgentSessionLifetime);
    }

    [Fact]
    public void WrongLengthBindingKeysAreRefusedByTheBindingItself()
    {
        // The primitive refuses rather than padding, truncating, or hashing a short key.
        Assert.Throws<ArgumentException>(() => new OperationBinding(new byte[16]));
        Assert.Throws<ArgumentException>(() => new OperationBinding(new byte[OperationBinding.KeyLength + 1]));

        // A correct-length key is accepted and binds deterministically.
        var key = new byte[OperationBinding.KeyLength];
        key[0] = 7;

        using var binding = new OperationBinding(key);
        var approvalId = Guid.NewGuid();
        const string target = "local.document";
        const string policyVersion = SecurityPolicyEvaluator.M2PolicyVersion;
        var payload = Encoding.UTF8.GetBytes("payload");

        var first = binding.Compute(
            approvalId, SecurityAction.ReadLocalData, "agent-alpha", IdentityKind.Agent, target, policyVersion, payload);
        var second = binding.Compute(
            approvalId, SecurityAction.ReadLocalData, "agent-alpha", IdentityKind.Agent, target, policyVersion, payload);

        Assert.True(first.AsSpan().SequenceEqual(second));
        Assert.Equal(OperationBinding.BindingLength, first.Length);

        // A different payload, a different actor, and a different target all differ.
        Assert.False(first.AsSpan().SequenceEqual(binding.Compute(
            approvalId, SecurityAction.ReadLocalData, "agent-alpha", IdentityKind.Agent, target, policyVersion,
            Encoding.UTF8.GetBytes("payload2"))));
        Assert.False(first.AsSpan().SequenceEqual(binding.Compute(
            approvalId, SecurityAction.ReadLocalData, "agent-bravo", IdentityKind.Agent, target, policyVersion, payload)));
        Assert.False(first.AsSpan().SequenceEqual(binding.Compute(
            approvalId, SecurityAction.ReadLocalData, "agent-alpha", IdentityKind.Agent, "local.other", policyVersion, payload)));
        Assert.False(first.AsSpan().SequenceEqual(binding.Compute(
            approvalId, SecurityAction.ReadLocalData, "agent-alpha", IdentityKind.Owner, target, policyVersion, payload)));

        // Length framing: (a, bc) and (ab, c) must not collide.
        var shortTarget = binding.Compute(
            approvalId, SecurityAction.ReadLocalData, "agent", IdentityKind.Agent, "bcd", policyVersion,
            Encoding.UTF8.GetBytes("x"));
        var longTarget = binding.Compute(
            approvalId, SecurityAction.ReadLocalData, "agentb", IdentityKind.Agent, "cd", policyVersion,
            Encoding.UTF8.GetBytes("x"));
        Assert.False(shortTarget.AsSpan().SequenceEqual(longTarget));
    }

    [Fact]
    public void KernelFallsBackToAVolatileBindingKeyRatherThanAcceptingAWeakOne()
    {
        using var harness = new KernelHarness();

        using var shortKeyKernel = SecurityKernel.Create(KernelHarness.CreateOptions(
            Path.Combine(harness.Root, "short-key"),
            harness.Clock,
            bindingKey: new byte[16]));

        // The kernel is usable, but only through a volatile key, and it must be honest
        // about that: a wrong-length key is never silently used.
        Assert.NotNull(shortKeyKernel.Binding);
        Assert.True(shortKeyKernel.IsOperational);

        using var properKeyKernel = SecurityKernel.Create(KernelHarness.CreateOptions(
            Path.Combine(harness.Root, "good-key"),
            harness.Clock,
            bindingKey: new byte[OperationBinding.KeyLength]));

        Assert.NotNull(properKeyKernel.Binding);
        Assert.NotSame(shortKeyKernel.Binding, properKeyKernel.Binding);
    }

    [Fact]
    public void ApprovalLifetimeIsClampedToPolicyBounds()
    {
        using var harness = new KernelHarness();
        using var agent = new TestAgent("agent-alpha");
        harness.Kernel.IdentityAuthority.RegisterAgent(agent.Registration, harness.OwnerEvidence);
        var session = harness.Kernel.IdentityAuthority.AuthenticateAgent(agent.Authenticate()).Value;
        harness.Grant(agent.AgentId, IdentityKind.Agent, SecurityCapability.ReadLocalData);

        var operation = new OperationIdentity(
            SecurityAction.ReadLocalData,
            DataSensitivity.LocalPrivate,
            "local.document",
            SecurityPolicyEvaluator.M2PolicyVersion);

        var approvalId = harness.Kernel.Dispatcher
            .RequestApproval(session.SessionId, operation, Encoding.UTF8.GetBytes("payload"), TimeSpan.FromDays(7))
            .Value;

        Assert.True(harness.Kernel.Approvals.TryGet(approvalId, out var record));
        var lifetime = record!.ExpiresAtUtc - record.CreatedAtUtc;
        Assert.True(lifetime <= ApprovalEngine.MaximumLifetime);
        Assert.True(lifetime >= ApprovalEngine.MinimumLifetime);
    }
}
