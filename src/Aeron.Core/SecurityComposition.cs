using System.Runtime.Versioning;
using Aeron.Contracts;
using Aeron.Infrastructure;
using Aeron.Security;

namespace Aeron.Core;

/// <summary>
/// Result of security kernel startup. <see cref="Kernel"/> is null only when the kernel
/// could not be constructed at all; a constructed-but-unhealthy kernel is returned with
/// <see cref="IsOperational"/> = false so the UI can report honest status and deny access.
/// </summary>
public sealed record SecurityKernelStartupResult(
    SecurityKernel? Kernel,
    string StatusText,
    string Diagnostic)
{
    /// <summary>True only when a kernel exists and its startup verification plus the live
    /// ledger check both pass. A constructed-but-failed kernel is never "operational".</summary>
    public bool IsOperational => Kernel is { IsOperational: true };

    /// <summary>True when a kernel object exists, even if it is failing closed.</summary>
    public bool IsConstructed => Kernel is not null;
}

/// <summary>
/// Production composition for the M2 security kernel.
///
/// Wiring decisions, stated explicitly so they can be reviewed:
/// * The account source is the OS token (<see cref="WindowsAccountIdentitySource"/>); on
///   non-Windows hosts observation fails and every Owner/agent path stays denied.
/// * The Owner factor policy is <see cref="UnapprovedOwnerFactorPolicy"/>. The Owner has
///   not yet approved MFA factors, enrollment, or recovery (U-8R.3 unresolved; register
///   entry D2-01), so no Owner session can be minted. This is deliberate fail-closed
///   behavior, not a missing wire.
/// * The vault encryption policy is <see cref="UnapprovedVaultEncryptionPolicy"/> for the
///   same reason (register D2-05/D2-06); the vault still refuses storage.
/// * The operation-binding key uses DPAPI-protected local storage when available on
///   Windows; otherwise the kernel falls back to a volatile key and every approval becomes
///   unverifiable on restart (fail closed).
/// * The audit checkpoint is a same-user file anchor and is reported honestly as weak
///   until an independently protected mechanism is chosen (register D2-07).
/// </summary>
public static class SecurityComposition
{
    public const string AuditFileName = "aeron_security_audit.auditlog";
    public const string AuditCheckpointFileName = "aeron_security_audit.head";
    public const string ApprovalDirectoryName = "approvals";
    public const string VaultDirectoryName = "vault";
    public const string BindingKeyFileName = "operation-binding.key";

    public static SecurityKernelStartupResult CreateKernel(IAppPaths paths, IClock clock, IRedactionFilter redactionFilter)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(redactionFilter);

        try
        {
            paths.EnsureCreatedAndSecured();

            var securityDirectory = Path.Combine(paths.DataDirectory, "security");
            var approvalDirectory = Path.Combine(securityDirectory, ApprovalDirectoryName);
            var vaultDirectory = Path.Combine(securityDirectory, VaultDirectoryName);

            Directory.CreateDirectory(securityDirectory);
            Directory.CreateDirectory(approvalDirectory);
            Directory.CreateDirectory(vaultDirectory);

            var checkpoint = new FileAuditCheckpointStore(Path.Combine(securityDirectory, AuditCheckpointFileName));
            var bindingKey = TryLoadOrCreateBindingKey(vaultDirectory, redactionFilter);

            var kernel = SecurityKernel.Create(new SecurityKernelOptions
            {
                AuditFilePath = Path.Combine(securityDirectory, AuditFileName),
                ApprovalDirectory = approvalDirectory,
                CheckpointStore = checkpoint,
                AccountSource = new WindowsAccountIdentitySource(),
                FactorPolicy = new UnapprovedOwnerFactorPolicy(),
                SignatureVerifier = new EcdsaAgentSignatureVerifier(),
                NonceStore = new InMemoryNonceStore(),
                Clock = clock,
                SessionPolicy = SessionPolicy.Default,
                BindingKey = bindingKey
            });

            var verification = kernel.StartupVerification;

            if (!verification.IsHealthy)
            {
                return new SecurityKernelStartupResult(
                    kernel,
                    "Security kernel started in fail-closed mode.",
                    verification.Diagnostic);
            }

            return new SecurityKernelStartupResult(
                kernel,
                "Security kernel operational. Owner authentication is unavailable until an approved MFA policy is registered.",
                verification.Diagnostic);
        }
        catch (Exception ex)
        {
            return new SecurityKernelStartupResult(
                null,
                "Security kernel is unavailable; all security-dependent operations are denied.",
                redactionFilter.Redact(ex.Message));
        }
    }

    /// <summary>
    /// Loads the operation-binding key from DPAPI-protected storage, creating it on first
    /// run. Returns null when protection is unavailable, in which case the kernel uses a
    /// volatile key and approvals cannot survive a restart.
    /// </summary>
    private static byte[]? TryLoadOrCreateBindingKey(string vaultDirectory, IRedactionFilter redactionFilter)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        return TryLoadOrCreateBindingKeyWindows(vaultDirectory, redactionFilter);
    }

    [SupportedOSPlatform("windows")]
    private static byte[]? TryLoadOrCreateBindingKeyWindows(string vaultDirectory, IRedactionFilter redactionFilter)
    {
        try
        {
            var protector = new DpapiDataProtector();
            var path = Path.Combine(vaultDirectory, BindingKeyFileName);

            if (File.Exists(path))
            {
                var protectedBytes = File.ReadAllBytes(path);

                if (protector.TryUnprotect(protectedBytes, out var existing) && existing.Length == OperationBinding.KeyLength)
                {
                    return existing;
                }

                return null;
            }

            var fresh = System.Security.Cryptography.RandomNumberGenerator.GetBytes(OperationBinding.KeyLength);

            if (!protector.TryProtect(fresh, out var protectedFresh))
            {
                return null;
            }

            var temp = path + ".tmp";
            File.WriteAllBytes(temp, protectedFresh);
            File.Move(temp, path, overwrite: false);
            return fresh;
        }
        catch (Exception ex)
        {
            _ = redactionFilter.Redact(ex.Message);
            return null;
        }
    }
}
