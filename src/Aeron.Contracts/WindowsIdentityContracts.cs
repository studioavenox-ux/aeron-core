namespace Aeron.Contracts;

/// <summary>OS account observation only; not proof of Owner identity or MFA.</summary>
public sealed record WindowsAccountIdentity(string Sid);

/// <summary>Trusted Windows integration must obtain the SID from the OS, not caller text.</summary>
public interface IWindowsAccountIdentitySource
{
    bool TryGetCurrentAccount(out WindowsAccountIdentity? account);
}

/// <summary>
/// Independent protected Owner verification, including the eventual Owner-approved MFA
/// and device checks. Enrollment, recovery and factors are not defined by this contract.
/// </summary>
public interface IWindowsOwnerVerifier
{
    bool TryVerifyOwner(WindowsAccountIdentity account, ReadOnlyMemory<byte> evidence, out string? ownerSubjectId);
}
