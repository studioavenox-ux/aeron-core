using System.Text;
using Aeron.Contracts;
using Aeron.TestSupport;
using Xunit;

namespace Aeron.Security.Tests.M2;

public sealed class AuthorizationTests
{
    private static readonly OperationIdentity StatusRead = new(
        SecurityAction.ReadApplicationState,
        DataSensitivity.Routine,
        "aeron.security.status",
        SecurityPolicyEvaluator.M2PolicyVersion);

    [Fact]
    public void DefaultDenyHoldsForEveryUnmappedUnversionedOrSecretOperation()
    {
        var store = new InMemoryPermissionStore();
        var evaluator = new SecurityPolicyEvaluator(store);
        var agent = new VerifiedActor("agent-alpha", IdentityKind.Agent);
        store.TryGrant("agent-alpha", IdentityKind.Agent, SecurityCapability.ViewSecurityStatus);

        // Unknown action.
        Assert.Equal(
            SecurityDenialReason.InvalidRequest,
            evaluator.Evaluate(agent, StatusRead with { Action = SecurityAction.Unknown }, SecurityPolicyEvaluator.Version).DenialReason);

        // Missing actor.
        Assert.Equal(
            SecurityDenialReason.IdentityRejected,
            evaluator.Evaluate(null, StatusRead, SecurityPolicyEvaluator.Version).DenialReason);

        // Stale policy version, both declared and embedded.
        Assert.Equal(
            SecurityDenialReason.PolicyVersionMismatch,
            evaluator.Evaluate(agent, StatusRead, new SecurityPolicyVersion("aeron-m2-0")).DenialReason);
        Assert.Equal(
            SecurityDenialReason.PolicyVersionMismatch,
            evaluator.Evaluate(agent, StatusRead with { PolicyVersion = "aeron-m2-0" }, SecurityPolicyEvaluator.Version).DenialReason);

        // Secrets never dispatch through a general operation.
        Assert.Equal(
            SecurityDenialReason.ForbiddenAction,
            evaluator.Evaluate(agent, StatusRead with { Sensitivity = DataSensitivity.Secret }, SecurityPolicyEvaluator.Version).DenialReason);

        // No permission store at all: deny.
        Assert.Equal(
            SecurityDenialReason.StoreUnavailable,
            new SecurityPolicyEvaluator(null).Evaluate(agent, StatusRead, SecurityPolicyEvaluator.Version).DenialReason);

        // Missing capability: deny.
        Assert.Equal(
            SecurityDenialReason.CapabilityNotGranted,
            evaluator.Evaluate(new VerifiedActor("agent-beta", IdentityKind.Agent), StatusRead, SecurityPolicyEvaluator.Version).DenialReason);

        // No M2 dispatch path exists for external effects or local mutation.
        foreach (var action in new[]
                 {
                     SecurityAction.RemoteInference, SecurityAction.DiscloseData,
                     SecurityAction.SendCommunication, SecurityAction.ModifyExternalAccount,
                     SecurityAction.ChangeAuthority, SecurityAction.ExecuteSkillScript,
                     SecurityAction.ModifyLocalData
                 })
        {
            var result = evaluator.Evaluate(agent, StatusRead with { Action = action }, SecurityPolicyEvaluator.Version);
            Assert.Equal(PolicyDisposition.Deny, result.Disposition);
            Assert.Equal(SecurityDenialReason.ForbiddenAction, result.DenialReason);
        }
    }

    [Fact]
    public void AgentsCannotGrantThemselvesCapabilitiesOrElevateToOwner()
    {
        using var harness = new KernelHarness();
        using var agent = new TestAgent("agent-alpha");
        harness.RegisterAgent(agent.Registration);
        var agentSession = harness.AuthenticateAgent(agent.Authenticate());

        var admin = new PermissionAdministration(
            harness.Kernel.Permissions, harness.Kernel.IdentityAuthority, harness.Kernel.Audit);

        // An agent session id used as Owner evidence is not Owner authority.
        var agentEvidence = KernelHarness.SessionEvidence(agentSession.SessionId);
        var selfGrant = admin.Grant(agentEvidence, agent.AgentId, IdentityKind.Agent, SecurityCapability.ViewSecurityStatus);
        Assert.False(selfGrant.IsSuccess);
        Assert.Equal("OWNER_REQUIRED", selfGrant.ErrorCode);

        // Owner-kind grants are refused even to the Owner (authority is intrinsic, not a grant).
        var ownerKindGrant = admin.Grant(harness.OwnerEvidence, "agent-alpha", IdentityKind.Owner, SecurityCapability.ViewSecurityStatus);
        Assert.Equal("INVALID_REQUEST", ownerKindGrant.ErrorCode);

        // Owner can grant an agent capability; the agent can never do it for itself.
        harness.Grant("agent-alpha", IdentityKind.Agent, SecurityCapability.ViewSecurityStatus);
        Assert.True(harness.Kernel.Permissions.IsGranted("agent-alpha", IdentityKind.Agent, SecurityCapability.ViewSecurityStatus));
    }

    [Fact]
    public void PermissionRevocationTakesEffectImmediatelyAtTheDispatchBoundary()
    {
        using var harness = new KernelHarness();
        using var agent = new TestAgent("agent-alpha");
        harness.RegisterAgent(agent.Registration);
        var session = harness.AuthenticateAgent(agent.Authenticate());

        harness.Grant(agent.AgentId, IdentityKind.Agent, SecurityCapability.ViewSecurityStatus);

        var allowed = harness.Kernel.Dispatcher.ExecuteDirect(session.SessionId, StatusRead, static () => "ok");
        Assert.True(allowed.IsSuccess, allowed.ErrorMessage);

        var admin = new PermissionAdministration(
            harness.Kernel.Permissions, harness.Kernel.IdentityAuthority, harness.Kernel.Audit);
        Assert.True(admin.Revoke(harness.OwnerEvidence, agent.AgentId, IdentityKind.Agent, SecurityCapability.ViewSecurityStatus).IsSuccess);

        var denied = harness.Kernel.Dispatcher.ExecuteDirect(session.SessionId, StatusRead, static () => "ok");
        Assert.False(denied.IsSuccess);
        Assert.Equal("SECURITY_DENIED", denied.ErrorCode);
    }

    [Fact]
    public void AgentIsolationPreventsOneAgentUsingAnotherAgentsSessionOrAuthority()
    {
        using var harness = new KernelHarness();
        using var alpha = new TestAgent("agent-alpha");
        using var bravo = new TestAgent("agent-bravo");

        harness.RegisterAgent(alpha.Registration);
        harness.RegisterAgent(bravo.Registration);

        var alphaSession = harness.AuthenticateAgent(alpha.Authenticate());
        var bravoSession = harness.AuthenticateAgent(bravo.Authenticate());

        harness.Grant(alpha.AgentId, IdentityKind.Agent, SecurityCapability.ViewSecurityStatus);

        // Bravo holds no capability: its own session is denied.
        var bravoDirect = harness.Kernel.Dispatcher.ExecuteDirect(bravoSession.SessionId, StatusRead, static () => "ok");
        Assert.False(bravoDirect.IsSuccess);

        // A session id is not a transferable selector. The id resolves to the agent that
        // authenticated it, so bravo cannot borrow alpha's identity by holding an id; what
        // bravo holds is alpha's session, which alpha legitimately owns.
        var alphaDirect = harness.Kernel.Dispatcher.ExecuteDirect(alphaSession.SessionId, StatusRead, static () => "ok");
        Assert.True(alphaDirect.IsSuccess);
        Assert.True(harness.Kernel.IdentityAuthority.TryResolveActor(alphaSession.SessionId, out var resolvedAlpha));
        Assert.Equal(alpha.AgentId, resolvedAlpha!.SubjectId);

        // Each session resolves only to the agent that authenticated it.
        Assert.True(harness.Kernel.IdentityAuthority.TryResolveActor(bravoSession.SessionId, out var bravoActor));
        Assert.NotNull(bravoActor);
        Assert.Equal(bravo.AgentId, bravoActor!.SubjectId);
        Assert.NotEqual(alpha.AgentId, bravoActor.SubjectId);
    }

    [Fact]
    public void DirectDispatchRefusesOperationsThatRequireOwnerApproval()
    {
        using var harness = new KernelHarness();
        using var agent = new TestAgent("agent-alpha");
        harness.RegisterAgent(agent.Registration);
        var session = harness.AuthenticateAgent(agent.Authenticate());

        // Grant the capability that would map to an approval-required operation family.
        harness.Grant(agent.AgentId, IdentityKind.Agent, SecurityCapability.ReadLocalData);

        var approvalRequired = new OperationIdentity(
            SecurityAction.ReadLocalData,
            DataSensitivity.LocalPrivate,
            "local.document",
            SecurityPolicyEvaluator.M2PolicyVersion);

        var direct = harness.Kernel.Dispatcher.ExecuteDirect(session.SessionId, approvalRequired, static () => "ok");
        Assert.False(direct.IsSuccess);

        // Asking for approval is allowed, but only when the actor holds the capability and
        // the operation actually requires approval.
        var requested = harness.Kernel.Dispatcher.RequestApproval(
            session.SessionId, approvalRequired, Encoding.UTF8.GetBytes("synthetic-payload"), TimeSpan.FromMinutes(5));
        Assert.True(requested.IsSuccess, requested.ErrorMessage);
    }

    [Fact]
    public void ExpiredOrRevokedSessionCannotDispatchEvenWithAGrant()
    {
        using var harness = new KernelHarness();
        using var agent = new TestAgent("agent-alpha");
        harness.RegisterAgent(agent.Registration);
        var session = harness.AuthenticateAgent(agent.Authenticate());
        harness.Grant(agent.AgentId, IdentityKind.Agent, SecurityCapability.ViewSecurityStatus);

        harness.Clock.Advance(SessionPolicy.Default.AgentSessionLifetime + TimeSpan.FromSeconds(1));

        var expired = harness.Kernel.Dispatcher.ExecuteDirect(session.SessionId, StatusRead, static () => "ok");
        Assert.False(expired.IsSuccess);

        // A fabricated session id is never valid.
        var fabricated = harness.Kernel.Dispatcher.ExecuteDirect(Guid.NewGuid(), StatusRead, static () => "ok");
        Assert.False(fabricated.IsSuccess);
    }

    [Fact]
    public void PrivateDataAlwaysRequiresOwnerApprovalEvenForAnOrdinaryRead()
    {
        var store = new InMemoryPermissionStore();
        var evaluator = new SecurityPolicyEvaluator(store);
        var agent = new VerifiedActor("agent-alpha", IdentityKind.Agent);

        store.TryGrant("agent-alpha", IdentityKind.Agent, SecurityCapability.ReadLocalData);

        var privateRead = new OperationIdentity(
            SecurityAction.ReadLocalData,
            DataSensitivity.LocalPrivate,
            "local.document",
            SecurityPolicyEvaluator.M2PolicyVersion);

        var evaluation = evaluator.Evaluate(agent, privateRead, SecurityPolicyEvaluator.Version);

        // ADR-AUTH-002: LocalPrivate/restricted disclosure requires G2 Owner approval. A
        // capability grant alone must never make a private read directly executable.
        Assert.Equal(PolicyDisposition.RequireOwnerApproval, evaluation.Disposition);
        Assert.Equal(SecurityDenialReason.ApprovalRequired, evaluation.DenialReason);
        Assert.True(evaluation.RequiresApproval);
        Assert.True(evaluation.IsApprovalRequired);
        Assert.False(evaluation.IsAllowedDirectly);
        Assert.True(evaluation.Domains.HasFlag(GovernanceDomain.G2));

        // Routine application state stays directly allowed for a granted agent.
        store.TryGrant("agent-alpha", IdentityKind.Agent, SecurityCapability.ViewSecurityStatus);
        var routine = evaluator.Evaluate(agent, StatusRead, SecurityPolicyEvaluator.Version);
        Assert.Equal(PolicyDisposition.Allow, routine.Disposition);
        Assert.True(routine.IsAllowedDirectly);
        Assert.False(routine.RequiresApproval);
        Assert.Equal(GovernanceDomain.None, routine.Domains);

        // The same routine read degrades to approval-required when it touches private data.
        var routineOnPrivateData = evaluator.Evaluate(
            agent, StatusRead with { Sensitivity = DataSensitivity.LocalPrivate }, SecurityPolicyEvaluator.Version);
        Assert.Equal(PolicyDisposition.RequireOwnerApproval, routineOnPrivateData.Disposition);

        // Unknown sensitivity is a denial, never a default.
        Assert.Equal(
            SecurityDenialReason.InvalidRequest,
            evaluator.Evaluate(agent, StatusRead with { Sensitivity = DataSensitivity.Unknown }, SecurityPolicyEvaluator.Version).DenialReason);
    }

    [Fact]
    public void EveryPolicyDecisionCarriesAVersionAndARuleCode()
    {
        var store = new InMemoryPermissionStore();
        var evaluator = new SecurityPolicyEvaluator(store);
        var agent = new VerifiedActor("agent-alpha", IdentityKind.Agent);
        store.TryGrant("agent-alpha", IdentityKind.Agent, SecurityCapability.ViewSecurityStatus);

        foreach (var evaluation in new[]
                 {
                     evaluator.Evaluate(agent, StatusRead, SecurityPolicyEvaluator.Version),
                     evaluator.Evaluate(agent, StatusRead with { Action = SecurityAction.Unknown }, SecurityPolicyEvaluator.Version),
                     evaluator.Evaluate(agent, StatusRead with { Sensitivity = DataSensitivity.LocalPrivate }, SecurityPolicyEvaluator.Version),
                     evaluator.Evaluate(null, StatusRead, SecurityPolicyEvaluator.Version),
                     evaluator.Evaluate(agent, StatusRead, new SecurityPolicyVersion("stale")),
                     new SecurityPolicyEvaluator(null).Evaluate(agent, StatusRead, SecurityPolicyEvaluator.Version)
                 })
        {
            Assert.Equal(SecurityPolicyEvaluator.M2PolicyVersion, evaluation.PolicyVersion);
            Assert.False(string.IsNullOrWhiteSpace(evaluation.RuleCode));
            Assert.StartsWith("rule.", evaluation.RuleCode, StringComparison.Ordinal);
        }
    }
}
