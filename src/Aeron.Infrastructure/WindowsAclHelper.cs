using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Aeron.Infrastructure;

/// <summary>
/// Applies and inspects protected current-user-only Windows DACLs on AERON directories and files.
/// </summary>
public static class WindowsAclHelper
{
    [SupportedOSPlatform("windows")]
    public static void SecureDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        var directoryInfo = new DirectoryInfo(directoryPath);
        if (!directoryInfo.Exists)
        {
            directoryInfo.Create();
        }

        using var identity = WindowsIdentity.GetCurrent();
        var currentUserSid = identity.User
            ?? throw new InvalidOperationException("Unable to resolve current Windows user SID for directory ACL.");
        var localSystemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        const InheritanceFlags DirectoryInheritance =
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

        security.AddAccessRule(new FileSystemAccessRule(
            currentUserSid,
            FileSystemRights.FullControl,
            DirectoryInheritance,
            PropagationFlags.None,
            AccessControlType.Allow));

        security.AddAccessRule(new FileSystemAccessRule(
            localSystemSid,
            FileSystemRights.FullControl,
            DirectoryInheritance,
            PropagationFlags.None,
            AccessControlType.Allow));

        directoryInfo.SetAccessControl(security);
    }

    [SupportedOSPlatform("windows")]
    public static void SecureFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("Cannot apply Windows ACL to a nonexistent file.", filePath);
        }

        using var identity = WindowsIdentity.GetCurrent();
        var currentUserSid = identity.User
            ?? throw new InvalidOperationException("Unable to resolve current Windows user SID for file ACL.");
        var localSystemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        security.AddAccessRule(new FileSystemAccessRule(
            currentUserSid,
            FileSystemRights.FullControl,
            InheritanceFlags.None,
            PropagationFlags.None,
            AccessControlType.Allow));

        security.AddAccessRule(new FileSystemAccessRule(
            localSystemSid,
            FileSystemRights.FullControl,
            InheritanceFlags.None,
            PropagationFlags.None,
            AccessControlType.Allow));

        fileInfo.SetAccessControl(security);
    }

    [SupportedOSPlatform("windows")]
    public static bool IsProtectedCurrentUserOnlyAcl(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        FileSystemSecurity security = Directory.Exists(path)
            ? new DirectoryInfo(path).GetAccessControl(AccessControlSections.Access)
            : File.Exists(path)
                ? new FileInfo(path).GetAccessControl(AccessControlSections.Access)
                : throw new FileNotFoundException("Cannot inspect ACL of a nonexistent path.", path);

        if (!security.AreAccessRulesProtected)
        {
            return false;
        }

        using var identity = WindowsIdentity.GetCurrent();
        var currentUserSid = identity.User;
        if (currentUserSid is null)
        {
            return false;
        }

        var localSystemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier));

        var hasCurrentUserFullControl = false;

        foreach (FileSystemAccessRule rule in rules)
        {
            if (rule.IdentityReference is not SecurityIdentifier sid)
            {
                return false;
            }

            if (!sid.Equals(currentUserSid) && !sid.Equals(localSystemSid))
            {
                return false;
            }

            if (sid.Equals(currentUserSid) &&
                rule.AccessControlType == AccessControlType.Allow &&
                (rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl)
            {
                hasCurrentUserFullControl = true;
            }
        }

        return hasCurrentUserFullControl;
    }
}
