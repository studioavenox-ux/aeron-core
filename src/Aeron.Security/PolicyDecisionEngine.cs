using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// A conservative engineering policy for the constraints in CONSTRAINTS.md. Requests
/// must originate at a trusted action boundary. RequireOwnerApproval is not permission
/// to execute; approval consumption and dispatch are not yet implemented here.
/// </summary>
public sealed class PolicyDecisionEngine : ISecurityPolicy
{
    public const string CurrentPolicyVersion = "m2-boundary-1";

    private readonly OwnerAuthorityGuard _authority;
    private readonly IPermissionGrantStore? _grants;

    public PolicyDecisionEngine(ITrustedIdentityProvider? identityProvider, IPermissionGrantStore? grants)
    {
        _authority = new OwnerAuthorityGuard(identityProvider);
        _grants = grants;
    }

    public SecurityDecision Evaluate(SecurityRequest? request, ReadOnlyMemory<byte> identityEvidence) =>
        EvaluateAndIdentify(request, identityEvidence, out _);

    internal SecurityDecision EvaluateAndIdentify(
        SecurityRequest? request,
        ReadOnlyMemory<byte> identityEvidence,
        out VerifiedIdentity? identity)
    {
        identity = null;
        if (request is null ||
            !Enum.IsDefined(request.Action) || request.Action == SecurityAction.Unknown ||
            !Enum.IsDefined(request.Sensitivity) || request.Sensitivity == DataSensitivity.Unknown)
        {
            return Decision(PolicyDisposition.Deny, PolicyReason.InvalidRequest);
        }

        if (!string.Equals(request.PolicyVersion, CurrentPolicyVersion, StringComparison.Ordinal))
        {
            return Decision(PolicyDisposition.Deny, PolicyReason.PolicyVersionMismatch);
        }

        var (tier, domains) = MapAction(request.Action);
        if (tier == LocalAuthorityTier.L3)
        {
            return Decision(PolicyDisposition.Deny, PolicyReason.ForbiddenAction, tier, domains);
        }

        if (request.Sensitivity == DataSensitivity.Secret)
        {
            // No general-purpose operation is authorized to handle credentials in this
            // initial boundary. Vault access needs a separate, explicitly defined path.
            return Decision(PolicyDisposition.Deny, PolicyReason.SecretDisclosureForbidden, tier, domains);
        }

        if (request.Sensitivity == DataSensitivity.LocalPrivate && domains != GovernanceDomain.None)
        {
            domains |= GovernanceDomain.G2;
        }

        if (!_authority.TryAuthenticate(identityEvidence, out identity, out var identityReason))
        {
            return Decision(PolicyDisposition.Deny, identityReason, tier, domains);
        }

        if (request.Action == SecurityAction.ChangeAuthority && identity!.Kind != IdentityKind.Owner)
        {
            return Decision(PolicyDisposition.Deny, PolicyReason.OwnerRequired, tier, domains);
        }

        if (_grants is null)
        {
            return Decision(PolicyDisposition.Deny, PolicyReason.PermissionStoreUnavailable, tier, domains);
        }

        try
        {
            if (!_grants.IsGranted(identity!, request.Action))
            {
                return Decision(PolicyDisposition.Deny, PolicyReason.PermissionDenied, tier, domains);
            }
        }
        catch (Exception)
        {
            return Decision(PolicyDisposition.Deny, PolicyReason.PermissionStoreUnavailable, tier, domains);
        }

        // Local reads also require a trusted path/scope boundary; this policy does
        // not yet receive a resolved handle or protect against path substitution.
        if (request.Action == SecurityAction.ReadLocalData)
        {
            return Decision(PolicyDisposition.Deny, PolicyReason.ScopeValidationUnavailable, tier, domains);
        }

        // No verified $0 gateway/provider/model policy exists in this phase. Do not
        // substitute an unverified endpoint or a paid/local fallback.
        if ((domains & GovernanceDomain.G1) != 0)
        {
            return Decision(PolicyDisposition.Deny, PolicyReason.GatewayPolicyUnavailable, tier, domains);
        }

        if (tier == LocalAuthorityTier.L2 ||
            (domains & (GovernanceDomain.G2 | GovernanceDomain.G3 | GovernanceDomain.G4)) != 0)
        {
            return Decision(PolicyDisposition.RequireOwnerApproval, PolicyReason.ApprovalRequired, tier, domains);
        }

        return Decision(PolicyDisposition.Allow, PolicyReason.Allowed, tier, domains);
    }

    private static (LocalAuthorityTier Tier, GovernanceDomain Domains) MapAction(SecurityAction action) => action switch
    {
        SecurityAction.ReadApplicationState => (LocalAuthorityTier.L0, GovernanceDomain.None),
        SecurityAction.ReadLocalData => (LocalAuthorityTier.L1, GovernanceDomain.None),
        SecurityAction.ModifyLocalData => (LocalAuthorityTier.L2, GovernanceDomain.None),
        SecurityAction.RemoteInference => (LocalAuthorityTier.None, GovernanceDomain.G1),
        SecurityAction.DiscloseData => (LocalAuthorityTier.None, GovernanceDomain.G2),
        SecurityAction.SendCommunication => (LocalAuthorityTier.None, GovernanceDomain.G3),
        SecurityAction.ModifyExternalAccount => (LocalAuthorityTier.None, GovernanceDomain.G4),
        SecurityAction.ChangeAuthority => (LocalAuthorityTier.L2, GovernanceDomain.None),
        SecurityAction.ExecuteSkillScript => (LocalAuthorityTier.L3, GovernanceDomain.None),
        _ => (LocalAuthorityTier.L3, GovernanceDomain.None)
    };

    private static SecurityDecision Decision(
        PolicyDisposition disposition,
        PolicyReason reason,
        LocalAuthorityTier tier = LocalAuthorityTier.None,
        GovernanceDomain domains = GovernanceDomain.None) =>
        new(disposition, reason, CurrentPolicyVersion, tier, domains);
}
