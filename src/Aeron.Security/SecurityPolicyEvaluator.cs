using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// Deterministic, versioned, default-deny authority policy for AERON M2.
///
/// Every exact operation maps to one fixed rule: a governance domain (G1–G4), a local
/// tier (L0–L3), and a capability the actor must hold. The evaluator has no side effects,
/// never consults agent or model output, and returns Deny for anything unmapped,
/// unversioned, unvalidated, or secret-bearing.
///
/// M2 deliberately exposes no dispatch path for external effects (G1–G4) or for local
/// mutation, authority change, or skill scripts: those modules do not exist yet, so each
/// evaluates to Deny with a specific rule code. That is an explicit, tested scope
/// boundary rather than a missing check.
/// </summary>
public sealed class SecurityPolicyEvaluator
{
    public const string M2PolicyVersion = "aeron-m2-1";

    public static readonly SecurityPolicyVersion Version = new(M2PolicyVersion);

    private static readonly HashSet<SecurityAction> OperationsWithoutM2Dispatch =
    [
        // M2 implements governance and control paths only.
        SecurityAction.ModifyLocalData,
        SecurityAction.RemoteInference,
        SecurityAction.DiscloseData,
        SecurityAction.SendCommunication,
        SecurityAction.ModifyExternalAccount,
        SecurityAction.ChangeAuthority,
        SecurityAction.ExecuteSkillScript
    ];

    private readonly IPermissionStore? _permissions;

    public SecurityPolicyEvaluator(IPermissionStore? permissions)
    {
        _permissions = permissions;
    }

    public PolicyEvaluation Evaluate(
        VerifiedActor? actor,
        OperationIdentity? operation,
        SecurityPolicyVersion? declaredPolicyVersion)
    {
        if (operation is null || !operation.IsWellFormed)
        {
            return Deny(SecurityDenialReason.InvalidRequest, "rule.invalid_operation");
        }

        if (declaredPolicyVersion is null ||
            !declaredPolicyVersion.IsValid ||
            !string.Equals(declaredPolicyVersion.Value, Version.Value, StringComparison.Ordinal) ||
            !string.Equals(operation.PolicyVersion, Version.Value, StringComparison.Ordinal))
        {
            return Deny(SecurityDenialReason.PolicyVersionMismatch, "rule.policy_version");
        }

        var (capability, tier, domains) = Map(operation.Action);

        if (OperationsWithoutM2Dispatch.Contains(operation.Action))
        {
            return Deny(SecurityDenialReason.ForbiddenAction, "rule.no_m2_dispatch_path", capability, tier, domains);
        }

        if (operation.Sensitivity == DataSensitivity.Secret)
        {
            // Secrets never travel through a general operation; the vault has its own path.
            return Deny(SecurityDenialReason.ForbiddenAction, "rule.secret_never_dispatchable", capability, tier, domains);
        }

        if (operation.Sensitivity == DataSensitivity.Unknown)
        {
            return Deny(SecurityDenialReason.InvalidRequest, "rule.unknown_sensitivity", capability, tier, domains);
        }

        if (actor is null || !actor.IsWellFormed)
        {
            return Deny(SecurityDenialReason.IdentityRejected, "rule.identity_missing", capability, tier, domains);
        }

        // ADR-AUTH-002 data classification: LocalPrivate/restricted disclosure always
        // carries G2, regardless of the action's base domain. Without this, a plain
        // ReadLocalData on private data would resolve to Allow with no Owner approval.
        if (operation.Sensitivity == DataSensitivity.LocalPrivate)
        {
            domains |= GovernanceDomain.G2;
        }

        if (_permissions is null)
        {
            return Deny(SecurityDenialReason.StoreUnavailable, "rule.permission_store_unavailable", capability, tier, domains);
        }

        bool granted;

        try
        {
            granted = capability != SecurityCapability.Unknown &&
                      _permissions.IsGranted(actor.SubjectId, actor.Kind, capability);
        }
        catch (Exception)
        {
            return Deny(SecurityDenialReason.StoreUnavailable, "rule.permission_store_unavailable", capability, tier, domains);
        }

        if (!granted)
        {
            return Deny(SecurityDenialReason.CapabilityNotGranted, "rule.capability_not_granted", capability, tier, domains);
        }

        if (tier == LocalAuthorityTier.L2 ||
            (domains & (GovernanceDomain.G1 | GovernanceDomain.G2 | GovernanceDomain.G3 | GovernanceDomain.G4)) != 0)
        {
            return new PolicyEvaluation(
                PolicyDisposition.RequireOwnerApproval,
                SecurityDenialReason.ApprovalRequired,
                capability,
                tier,
                domains,
                RequiresApproval: true,
                Version.Value,
                "rule.owner_approval_required");
        }

        return new PolicyEvaluation(
            PolicyDisposition.Allow,
            SecurityDenialReason.None,
            capability,
            tier,
            domains,
            RequiresApproval: false,
            Version.Value,
            "rule.allowed");
    }

    private static (SecurityCapability Capability, LocalAuthorityTier Tier, GovernanceDomain Domains) Map(SecurityAction action) =>
        action switch
        {
            SecurityAction.ReadApplicationState => (SecurityCapability.ViewSecurityStatus, LocalAuthorityTier.L0, GovernanceDomain.None),
            SecurityAction.ReadLocalData => (SecurityCapability.ReadLocalData, LocalAuthorityTier.L1, GovernanceDomain.None),
            SecurityAction.ModifyLocalData => (SecurityCapability.Unknown, LocalAuthorityTier.L2, GovernanceDomain.None),
            SecurityAction.RemoteInference => (SecurityCapability.Unknown, LocalAuthorityTier.None, GovernanceDomain.G1),
            SecurityAction.DiscloseData => (SecurityCapability.Unknown, LocalAuthorityTier.None, GovernanceDomain.G2),
            SecurityAction.SendCommunication => (SecurityCapability.Unknown, LocalAuthorityTier.None, GovernanceDomain.G3),
            SecurityAction.ModifyExternalAccount => (SecurityCapability.Unknown, LocalAuthorityTier.None, GovernanceDomain.G4),
            SecurityAction.ChangeAuthority => (SecurityCapability.Unknown, LocalAuthorityTier.L2, GovernanceDomain.None),
            SecurityAction.ExecuteSkillScript => (SecurityCapability.Unknown, LocalAuthorityTier.L3, GovernanceDomain.None),
            _ => (SecurityCapability.Unknown, LocalAuthorityTier.L3, GovernanceDomain.None)
        };

    private static PolicyEvaluation Deny(
        SecurityDenialReason reason,
        string ruleCode,
        SecurityCapability capability = SecurityCapability.Unknown,
        LocalAuthorityTier tier = LocalAuthorityTier.None,
        GovernanceDomain domains = GovernanceDomain.None) =>
        new(PolicyDisposition.Deny,
            reason,
            capability,
            tier,
            domains,
            RequiresApproval: false,
            Version.Value,
            ruleCode);
}
