using Aeron.Contracts;
using Xunit;

namespace Aeron.Security.Tests;

public sealed class SecurityPolicyTests
{
    private static readonly byte[] Evidence = [1, 2, 3];
    private const string Version = PolicyDecisionEngine.CurrentPolicyVersion;

    private sealed class TestIdentityProvider(VerifiedIdentity? identity, bool throws = false) : ITrustedIdentityProvider
    {
        public bool TryAuthenticate(ReadOnlyMemory<byte> evidence, out VerifiedIdentity? result)
        {
            if (throws)
            {
                throw new InvalidOperationException("synthetic authentication failure");
            }

            result = identity;
            return !evidence.IsEmpty && result is not null;
        }
    }

    private sealed class TestGrantStore(bool granted, bool throws = false) : IPermissionGrantStore
    {
        public bool IsGranted(VerifiedIdentity identity, SecurityAction action)
        {
            if (throws)
            {
                throw new InvalidOperationException("synthetic store failure");
            }

            return granted;
        }
    }

    private static PolicyDecisionEngine Engine(IdentityKind kind = IdentityKind.Owner, bool granted = true) =>
        new(new TestIdentityProvider(new VerifiedIdentity("subject", kind)), new TestGrantStore(granted));

    private static SecurityRequest Request(SecurityAction action, DataSensitivity sensitivity = DataSensitivity.Routine) =>
        new(action, sensitivity, Version);

    [Fact]
    public void MissingIdentityAndGrantProvidersDoNotAuthenticateOrGrant()
    {
        var decision = new PolicyDecisionEngine(null, null).Evaluate(Request(SecurityAction.ReadApplicationState), Evidence);
        Assert.Equal(PolicyDisposition.Deny, decision.Disposition);
        Assert.Equal(PolicyReason.IdentityProviderUnavailable, decision.Reason);
        Assert.False(new OwnerAuthorityGuard(null).VerifyOwner(Evidence));
    }

    [Fact]
    public void BadOrUnavailableIdentityCannotImpersonateOwner()
    {
        Assert.False(new OwnerAuthorityGuard(new TestIdentityProvider(new VerifiedIdentity("agent", IdentityKind.Agent))).VerifyOwner(Evidence));
        Assert.False(new OwnerAuthorityGuard(new TestIdentityProvider(new VerifiedIdentity("owner", IdentityKind.Owner))).VerifyOwner(ReadOnlyMemory<byte>.Empty));
        Assert.False(new OwnerAuthorityGuard(new TestIdentityProvider(null, throws: true)).VerifyOwner(Evidence));
        Assert.False(new OwnerAuthorityGuard(new TestIdentityProvider(new VerifiedIdentity(" ", IdentityKind.Owner))).VerifyOwner(Evidence));
        Assert.True(new OwnerAuthorityGuard(new TestIdentityProvider(new VerifiedIdentity("owner", IdentityKind.Owner))).VerifyOwner(Evidence));
    }

    [Fact]
    public void UnrecognizedActionClassificationAndPolicyVersionAreDenied()
    {
        var engine = Engine();
        Assert.Equal(PolicyReason.InvalidRequest, engine.Evaluate(null, Evidence).Reason);
        Assert.Equal(PolicyReason.InvalidRequest, engine.Evaluate(Request((SecurityAction)999), Evidence).Reason);
        Assert.Equal(PolicyReason.InvalidRequest, engine.Evaluate(Request(SecurityAction.ReadLocalData, (DataSensitivity)999), Evidence).Reason);
        Assert.Equal(PolicyReason.InvalidRequest, engine.Evaluate(Request(SecurityAction.ReadLocalData, DataSensitivity.Unknown), Evidence).Reason);
        Assert.Equal(PolicyReason.PolicyVersionMismatch, engine.Evaluate(new SecurityRequest(SecurityAction.ReadLocalData, DataSensitivity.Routine, "old"), Evidence).Reason);
    }

    [Fact]
    public void MissingPermissionOrFailedStoreDeniesEvenOwner()
    {
        var request = Request(SecurityAction.ReadApplicationState);
        Assert.Equal(PolicyReason.PermissionDenied, Engine(granted: false).Evaluate(request, Evidence).Reason);
        var engine = new PolicyDecisionEngine(new TestIdentityProvider(new VerifiedIdentity("owner", IdentityKind.Owner)), new TestGrantStore(true, throws: true));
        Assert.Equal(PolicyReason.PermissionStoreUnavailable, engine.Evaluate(request, Evidence).Reason);
        Assert.Equal(PolicyDisposition.Allow, Engine().Evaluate(request, Evidence).Disposition);
    }

    [Fact]
    public void AgentCannotChangeAuthorityAndOwnerCannotSkipApproval()
    {
        var request = Request(SecurityAction.ChangeAuthority);
        Assert.Equal(PolicyReason.OwnerRequired, Engine(IdentityKind.Agent).Evaluate(request, Evidence).Reason);
        Assert.Equal(PolicyDisposition.RequireOwnerApproval, Engine().Evaluate(request, Evidence).Disposition);
    }

    [Theory]
    [InlineData(SecurityAction.ModifyLocalData)]
    [InlineData(SecurityAction.DiscloseData)]
    [InlineData(SecurityAction.SendCommunication)]
    [InlineData(SecurityAction.ModifyExternalAccount)]
    public void ConsequentialActionsRequireApprovalEvenForOwner(SecurityAction action)
    {
        Assert.Equal(PolicyDisposition.RequireOwnerApproval, Engine().Evaluate(Request(action), Evidence).Disposition);
    }

    [Fact]
    public void PrivateExternalCommunicationRequiresBothG2AndG3()
    {
        var result = Engine().Evaluate(Request(SecurityAction.SendCommunication, DataSensitivity.LocalPrivate), Evidence);
        Assert.Equal(PolicyDisposition.RequireOwnerApproval, result.Disposition);
        Assert.Equal(GovernanceDomain.G2 | GovernanceDomain.G3, result.Domains);
    }

    [Fact]
    public void SecretsScriptsAndUnverifiedInferenceNeverPass()
    {
        var engine = Engine();
        Assert.Equal(PolicyReason.SecretDisclosureForbidden, engine.Evaluate(Request(SecurityAction.DiscloseData, DataSensitivity.Secret), Evidence).Reason);
        Assert.Equal(PolicyReason.ForbiddenAction, engine.Evaluate(Request(SecurityAction.ExecuteSkillScript), Evidence).Reason);
        Assert.Equal(PolicyReason.GatewayPolicyUnavailable, engine.Evaluate(Request(SecurityAction.RemoteInference), Evidence).Reason);
        Assert.Equal(PolicyReason.ScopeValidationUnavailable, engine.Evaluate(Request(SecurityAction.ReadLocalData), Evidence).Reason);
    }
}
