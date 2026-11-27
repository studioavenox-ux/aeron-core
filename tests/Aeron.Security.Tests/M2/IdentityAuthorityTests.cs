using System.Text;
using Aeron.Contracts;
using Aeron.TestSupport;
using Xunit;

namespace Aeron.Security.Tests.M2;

public sealed class IdentityAuthorityTests
{
    [Fact]
    public void WindowsAccountPresenceAloneNeverAuthenticatesTheOwner()
    {
        using var harness = new KernelHarness(authenticateOwner: false);

        // Enrollment itself is refused while no approved factor policy exists.
        var unapproved = SecurityKernel.Create(KernelHarness.CreateOptions(
            Path.Combine(harness.Root, "unapproved"),
            harness.Clock,
            factorPolicy: new UnapprovedOwnerFactorPolicy(),
            accountSource: harness.Accounts));

        var enroll = unapproved.IdentityAuthority.EnrollOwner(new OwnerEnrollmentRequest(KernelHarness.OwnerSubject, KernelHarness.OwnerSid));
        Assert.False(enroll.IsSuccess);
        Assert.Equal("FACTOR_POLICY_NOT_APPROVED", enroll.ErrorCode);
        Assert.False(unapproved.IdentityAuthority.IsOwnerEnrolled);

        // With an approved policy, enrollment succeeds but grants no session: the result is
        // explicitly ENROLLED_NO_SESSION, which is a failure of "authenticated now".
        using var kernel = SecurityKernel.Create(KernelHarness.CreateOptions(
            Path.Combine(harness.Root, "mixed"),
            harness.Clock,
            factorPolicy: new TestApprovedFactorPolicy(),
            accountSource: harness.Accounts));

        var approvedEnrollment = kernel.IdentityAuthority.EnrollOwner(
            new OwnerEnrollmentRequest(KernelHarness.OwnerSubject, KernelHarness.OwnerSid));

        Assert.True(approvedEnrollment.IsFailure);
        Assert.Equal("ENROLLED_NO_SESSION", approvedEnrollment.ErrorCode);
        Assert.True(kernel.IdentityAuthority.IsOwnerEnrolled);
        Assert.False(kernel.IdentityAuthority.HasActiveOwnerSession);

        var replacedPolicy = new ToggleableFactorPolicy { Approved = false };
        using var denied = SecurityKernel.Create(KernelHarness.CreateOptions(
            Path.Combine(harness.Root, "denied"),
            harness.Clock,
            factorPolicy: replacedPolicy,
            accountSource: harness.Accounts));

        var result = denied.IdentityAuthority.AuthenticateOwner(new OwnerAuthenticationRequest(
            new WindowsAccountIdentity(KernelHarness.OwnerSid),
            [new OwnerFactorEvidence("any", Encoding.UTF8.GetBytes("x"))],
            SecurityPolicyEvaluator.M2PolicyVersion));

        Assert.False(result.IsSuccess);
        Assert.Equal("OWNER_NOT_ENROLLED", result.ErrorCode);
    }

    [Fact]
    public void OwnerAuthenticationRequiresTheEnrolledOsAccountAndApprovedFactors()
    {
        using var harness = new KernelHarness();

        // Wrong OS account: the enrolled SID does not match the observed account.
        var wrongAccount = SecurityKernel.Create(KernelHarness.CreateOptions(
            Path.Combine(harness.Root, "other"),
            harness.Clock,
            factorPolicy: new TestApprovedFactorPolicy(),
            accountSource: new FixedAccountSource("S-1-5-21-9999")));

        // Owner cannot be re-enrolled after enrollment. The request presents the account the
        // OS actually observes, so the refusal is specifically ALREADY_ENROLLED and not a
        // generic identity rejection.
        var reenroll = harness.Kernel.IdentityAuthority.EnrollOwner(
            new OwnerEnrollmentRequest("intruder", KernelHarness.OwnerSid));
        Assert.Equal("ALREADY_ENROLLED", reenroll.ErrorCode);

        // A re-enrollment attempt from a different OS account is rejected on identity.
        var foreignReenroll = harness.Kernel.IdentityAuthority.EnrollOwner(
            new OwnerEnrollmentRequest("intruder", "S-1-5-21-9999"));
        Assert.Equal("IDENTITY_REJECTED", foreignReenroll.ErrorCode);

        // Correct account, wrong factor: denied.
        var badFactor = harness.Kernel.IdentityAuthority.AuthenticateOwner(new OwnerAuthenticationRequest(
            new WindowsAccountIdentity(KernelHarness.OwnerSid),
            [new OwnerFactorEvidence(TestApprovedFactorPolicy.TestFactorId, Encoding.UTF8.GetBytes("wrong"))],
            SecurityPolicyEvaluator.M2PolicyVersion));

        // The enrolled Owner's factor does not match: the approved policy denies. M2
        // deliberately reports a single non-secret code for factor failure so the refusal
        // cannot be used to distinguish "wrong factor" from "policy not approved".
        Assert.False(badFactor.IsSuccess);
        Assert.Equal("FACTOR_POLICY_NOT_APPROVED", badFactor.ErrorCode);

        // Stale policy version: denied.
        var stalePolicy = harness.Kernel.IdentityAuthority.AuthenticateOwner(new OwnerAuthenticationRequest(
            new WindowsAccountIdentity(KernelHarness.OwnerSid),
            [new OwnerFactorEvidence(TestApprovedFactorPolicy.TestFactorId, Encoding.UTF8.GetBytes(TestApprovedFactorPolicy.TestFactorValue))],
            "aeron-m2-old"));

        Assert.Equal("POLICY_VERSION_MISMATCH", stalePolicy.ErrorCode);

        // A supplied account observation that disagrees with the trusted OS source: denied.
        var spoofed = harness.Kernel.IdentityAuthority.AuthenticateOwner(new OwnerAuthenticationRequest(
            new WindowsAccountIdentity("S-1-5-21-9999"),
            [new OwnerFactorEvidence(TestApprovedFactorPolicy.TestFactorId, Encoding.UTF8.GetBytes(TestApprovedFactorPolicy.TestFactorValue))],
            SecurityPolicyEvaluator.M2PolicyVersion));

        Assert.Equal("IDENTITY_REJECTED", spoofed.ErrorCode);

        // Approved account plus approved factor: success.
        var success = harness.AuthenticateOwner();
        Assert.Equal(IdentityKind.Owner, success.Actor.Kind);
        Assert.True(harness.Kernel.IdentityAuthority.TryResolveOwnerSession(KernelHarness.SessionEvidence(success.SessionId), out var failure), failure);
    }

    [Fact]
    public void SessionsExpireAndOwnerPreemptionRevokesEveryAgentSession()
    {
        using var harness = new KernelHarness();
        using var agent = new TestAgent("agent-alpha");

        harness.RegisterAgent(agent.Registration);
        var agentSession = harness.AuthenticateAgent(agent.Authenticate());

        // Expiry is observed by the clock and is terminal.
        harness.Clock.Advance(SessionPolicy.Default.AgentSessionLifetime + TimeSpan.FromSeconds(1));
        Assert.False(harness.Kernel.IdentityAuthority.TryResolveActor(agentSession.SessionId, out _));

        // A fresh agent session then gets preempted by the Owner.
        var second = harness.AuthenticateAgent(agent.Authenticate());
        Assert.True(harness.Kernel.Preemption.TryPreempt(harness.OwnerEvidence, "owner.preempt_test"));
        Assert.False(harness.Kernel.IdentityAuthority.TryResolveActor(second.SessionId, out _));
    }

    [Fact]
    public void AgentAuthenticationRejectsReplayUnknownAndForgedKeys()
    {
        using var harness = new KernelHarness();
        using var agent = new TestAgent("agent-alpha");
        using var impostor = new TestAgent("agent-alpha");

        // Unknown agent: denied.
        var unknown = harness.Kernel.IdentityAuthority.AuthenticateAgent(agent.Authenticate());
        Assert.Equal("IDENTITY_REJECTED", unknown.ErrorCode);

        harness.RegisterAgent(agent.Registration);

        // Replay of a nonce: the store refuses the second use.
        var first = agent.Authenticate();
        Assert.True(harness.Kernel.IdentityAuthority.AuthenticateAgent(first).IsSuccess);
        var replay = harness.Kernel.IdentityAuthority.AuthenticateAgent(
            new AgentAuthenticationRequest(first.AgentId, first.ChallengeNonce, first.Signature, first.PolicyVersion));
        Assert.Equal("REPLAY_REJECTED", replay.ErrorCode);

        // A different key claiming the same agent id: signature verification fails.
        var forged = harness.Kernel.IdentityAuthority.AuthenticateAgent(impostor.Authenticate());
        Assert.Equal("IDENTITY_REJECTED", forged.ErrorCode);

        // Agent registration requires live Owner authority: empty evidence is refused with
        // a diagnosable reason and the agent stays unregistered.
        using var agent2 = new TestAgent("agent-beta");
        var unauthorized = harness.Kernel.IdentityAuthority.TryRegisterAgent(
            agent2.Registration, ReadOnlyMemory<byte>.Empty);
        Assert.False(unauthorized.IsSuccess);
        Assert.Equal("OWNER_REQUIRED", unauthorized.ErrorCode);
        Assert.Equal("IDENTITY_REJECTED",
            harness.Kernel.IdentityAuthority.AuthenticateAgent(agent2.Authenticate()).ErrorCode);
    }

    [Fact]
    public void RevokedAgentCannotAuthenticateOrKeepAnExistingSession()
    {
        using var harness = new KernelHarness();
        using var agent = new TestAgent("agent-alpha");

        harness.RegisterAgent(agent.Registration);
        var session = harness.AuthenticateAgent(agent.Authenticate());

        harness.Kernel.IdentityAuthority.RevokeAgent(agent.AgentId, harness.OwnerEvidence);

        Assert.False(harness.Kernel.IdentityAuthority.TryResolveActor(session.SessionId, out _));
        Assert.Equal("AGENT_REVOKED", harness.Kernel.IdentityAuthority.AuthenticateAgent(agent.Authenticate()).ErrorCode);

        // Revocation is terminal for the process: re-registration is refused and the agent
        // stays unusable.
        var reRegistration = harness.Kernel.IdentityAuthority.TryRegisterAgent(agent.Registration, harness.OwnerEvidence);
        Assert.False(reRegistration.IsSuccess);
        Assert.Equal("REGISTRATION_REFUSED", reRegistration.ErrorCode);
        Assert.Equal("AGENT_REVOKED", harness.Kernel.IdentityAuthority.AuthenticateAgent(agent.Authenticate()).ErrorCode);
    }

    [Fact]
    public void EmergencyStopLatchesAuthenticationAndDispatchUntilTheOwnerClearsIt()
    {
        using var harness = new KernelHarness();
        using var agent = new TestAgent("agent-alpha");

        harness.RegisterAgent(agent.Registration);

        Assert.True(harness.Kernel.Preemption.TrySetEmergencyStop(harness.OwnerEvidence, "test.stop"));
        Assert.True(harness.Kernel.Preemption.IsEmergencyStopActive);

        // Agents stay locked out for as long as the stop is engaged.
        Assert.Equal("EMERGENCY_STOP_ACTIVE", harness.Kernel.IdentityAuthority.AuthenticateAgent(agent.Authenticate()).ErrorCode);

        // Clearing requires live Owner authority, never anonymous evidence.
        Assert.False(harness.Kernel.Preemption.TryClearEmergencyStop(ReadOnlyMemory<byte>.Empty, "test.clear"));
        Assert.True(harness.Kernel.Preemption.IsEmergencyStopActive);

        // The Owner retains ultimate authority: emergency stop must not lock the Owner out
        // permanently, so an Owner session can still be opened while it is engaged, and that
        // session can clear it.
        var recovery = harness.AuthenticateOwner();
        harness.OwnerSessionId = recovery.SessionId;
        Assert.True(harness.Kernel.Preemption.TryClearEmergencyStop(harness.OwnerEvidence, "test.clear"));
        Assert.False(harness.Kernel.Preemption.IsEmergencyStopActive);
        Assert.True(harness.Kernel.IdentityAuthority.AuthenticateAgent(agent.Authenticate()).IsSuccess);
    }

    [Fact]
    public void SessionEvidenceIsTheRawSessionIdBytesAndOwnerEvidenceIsNotTransferable()
    {
        using var harness = new KernelHarness();
        using var agent = new TestAgent("agent-alpha");
        harness.RegisterAgent(agent.Registration);

        // The harness encodes evidence as Guid.ToByteArray(); the authority must decode it
        // back to the same session. A text encoding would silently resolve to another Guid.
        Assert.Equal(16, harness.OwnerEvidence.Length);
        Assert.True(
            harness.Kernel.IdentityAuthority.TryResolveOwnerSession(harness.OwnerEvidence, out var failure),
            failure);

        // An agent session id presented as Owner evidence is refused: the actor kind is
        // checked, not just the session's existence.
        var agentSession = harness.AuthenticateAgent(agent.Authenticate());
        var agentEvidence = KernelHarness.SessionEvidence(agentSession.SessionId);
        Assert.False(harness.Kernel.IdentityAuthority.TryResolveOwnerSession(agentEvidence, out _));

        // Wrong lengths and empty evidence are refused outright.
        Assert.False(harness.Kernel.IdentityAuthority.TryResolveOwnerSession(ReadOnlyMemory<byte>.Empty, out _));
        Assert.False(harness.Kernel.IdentityAuthority.TryResolveOwnerSession(new byte[15], out _));
        Assert.False(harness.Kernel.IdentityAuthority.TryResolveOwnerSession(new byte[32], out _));
    }

    private sealed class ToggleableFactorPolicy : IOwnerFactorPolicy
    {
        public bool Approved { get; set; }

        public string PolicyId => "toggle";

        public bool IsApproved => Approved;

        public bool TryVerifyFactors(
            WindowsAccountIdentity account,
            IReadOnlyList<OwnerFactorEvidence> factors,
            out string? verifiedSubjectId)
        {
            // Must match the enrolled Owner subject, or a correct factor would be
            // rejected as an identity mismatch rather than tested for the intended reason.
            verifiedSubjectId = Approved ? KernelHarness.OwnerSubject : null;
            return Approved;
        }
    }
}

internal static class KernelHarnessExtensions
{
    public static string AuthenticateOwnerExpectingFailure(this KernelHarness harness)
    {
        var result = harness.Kernel.IdentityAuthority.AuthenticateOwner(new OwnerAuthenticationRequest(
            new WindowsAccountIdentity(KernelHarness.OwnerSid),
            [new OwnerFactorEvidence(TestApprovedFactorPolicy.TestFactorId, Encoding.UTF8.GetBytes(TestApprovedFactorPolicy.TestFactorValue))],
            SecurityPolicyEvaluator.M2PolicyVersion));

        return result.IsSuccess ? "UNEXPECTED_SUCCESS" : result.ErrorCode;
    }
}
