using System.Runtime.Versioning;
using System.Security.Principal;
using Aeron.Contracts;

namespace Aeron.Infrastructure;

/// <summary>
/// Trusted Windows account observation through the OS token. Returns the current process
/// identity's SID exactly as Windows reports it; it never accepts caller-supplied text.
/// On non-Windows hosts the observation fails rather than inventing an account, which
/// keeps the security path fail-closed.
/// </summary>
public sealed class WindowsAccountIdentitySource : IWindowsAccountIdentitySource
{
    public bool TryGetCurrentAccount(out WindowsAccountIdentity? account)
    {
        account = null;

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return TryGetWindowsAccount(out account);
    }

    [SupportedOSPlatform("windows")]
    private static bool TryGetWindowsAccount(out WindowsAccountIdentity? account)
    {
        account = null;

        try
        {
            using var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query);

            if (identity.User is null)
            {
                return false;
            }

            var sid = identity.User;

            // Only a user account SID identifies an Owner candidate. A group, logon-session,
            // or service SID must never be treated as an account identity.
            if (sid.IsWellKnown(WellKnownSidType.LocalSystemSid) ||
                sid.IsWellKnown(WellKnownSidType.LocalServiceSid) ||
                sid.IsWellKnown(WellKnownSidType.NetworkServiceSid) ||
                !sid.IsAccountSid())
            {
                return false;
            }

            var value = sid.Value;

            if (string.IsNullOrWhiteSpace(value) || value.Length > 256)
            {
                return false;
            }

            account = new WindowsAccountIdentity(value);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
