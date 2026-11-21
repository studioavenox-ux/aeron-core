namespace Aeron.Contracts;

// Engineering mappings for the independently defined C-10/C-11 and G/L constraints.
// They are not a reconstruction of missing historical option wording.
public enum SecurityAction
{
    Unknown = 0,
    ReadApplicationState = 1,
    ReadLocalData = 2,
    ModifyLocalData = 3,
    RemoteInference = 4,
    DiscloseData = 5,
    SendCommunication = 6,
    ModifyExternalAccount = 7,
    ChangeAuthority = 8,
    ExecuteSkillScript = 9
}

public enum DataSensitivity
{
    Unknown = 0,
    Routine = 1,
    LocalPrivate = 2,
    Secret = 3
}

public enum IdentityKind
{
    Unknown = 0,
    Owner = 1,
    Agent = 2
}

public enum LocalAuthorityTier
{
    None = 0,
    L0 = 1,
    L1 = 2,
    L2 = 3,
    L3 = 4
}

[Flags]
public enum GovernanceDomain
{
    None = 0,
    G1 = 1,
    G2 = 2,
    G3 = 4,
    G4 = 8
}

public enum PolicyDisposition
{
    Deny = 0,
    Allow = 1,
    RequireOwnerApproval = 2
}

public enum PolicyReason
{
    Allowed = 0,
    ApprovalRequired = 1,
    InvalidRequest = 2,
    PolicyVersionMismatch = 3,
    IdentityProviderUnavailable = 4,
    IdentityRejected = 5,
    PermissionStoreUnavailable = 6,
    PermissionDenied = 7,
    ForbiddenAction = 8,
    SecretDisclosureForbidden = 9,
    GatewayPolicyUnavailable = 10,
    OwnerRequired = 11,
    ScopeValidationUnavailable = 12
}

/// <summary>Supplied by a trusted identity provider, not by an agent request.</summary>
public sealed record VerifiedIdentity(string SubjectId, IdentityKind Kind);

/// <summary>
/// Implementations must verify evidence and identity out of band, including required MFA
/// and device trust. No production identity provider is currently configured.
/// </summary>
public interface ITrustedIdentityProvider
{
    bool TryAuthenticate(ReadOnlyMemory<byte> evidence, out VerifiedIdentity? identity);
}

/// <summary>Provisioned by trusted composition; an agent cannot grant itself access.</summary>
public interface IPermissionGrantStore
{
    bool IsGranted(VerifiedIdentity identity, SecurityAction action);
}

/// <summary>
/// Action and sensitivity must be determined by the trusted action boundary, not by
/// untrusted model or agent input. Policy evaluation alone does not execute an action.
/// </summary>
public sealed record SecurityRequest(SecurityAction Action, DataSensitivity Sensitivity, string PolicyVersion);

public sealed record SecurityDecision(
    PolicyDisposition Disposition,
    PolicyReason Reason,
    string PolicyVersion,
    LocalAuthorityTier LocalTier,
    GovernanceDomain Domains);

public interface ISecurityPolicy
{
    SecurityDecision Evaluate(SecurityRequest? request, ReadOnlyMemory<byte> identityEvidence);
}
