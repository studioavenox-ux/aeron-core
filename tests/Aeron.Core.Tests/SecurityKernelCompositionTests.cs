using Aeron.Contracts;
using Aeron.Core;
using Aeron.Infrastructure;
using Aeron.Security;
using Xunit;

namespace Aeron.Core.Tests;

public sealed class SecurityKernelCompositionTests
{
    [Fact]
    public async Task FoundationHostComposesAVerifiedKernelAndKeepsSecurityPathsDenied()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aeron-kernel-compose-{Guid.NewGuid():N}");

        try
        {
            await using var host = AeronFoundationHost.Create(AppPaths.FromRoot(path));
            await host.Lifecycle.StartAsync();

            var startup = host.SecurityKernel;
            Assert.NotNull(startup);
            Assert.NotNull(startup.Kernel);
            Assert.True(startup.IsOperational, startup.Diagnostic);

            var kernel = startup.Kernel!;

            // No approved MFA policy is registered in production composition, so the Owner
            // cannot enroll or authenticate: the deliberate fail-closed default.
            Assert.False(kernel.IdentityAuthority.IsOwnerEnrolled);
            Assert.False(kernel.IdentityAuthority.EnrollOwner(new OwnerEnrollmentRequest("owner", "S-1-5-21-test")).IsSuccess);

            // A direct dispatch attempt with a fabricated session is denied.
            var dispatch = kernel.Dispatcher.ExecuteDirect(
                Guid.NewGuid(),
                new OperationIdentity(
                    SecurityAction.ReadApplicationState,
                    DataSensitivity.Routine,
                    "aeron.security.status",
                    SecurityPolicyEvaluator.M2PolicyVersion),
                static () => "ok");

            Assert.True(dispatch.IsFailure);
            Assert.Equal("SECURITY_DENIED", dispatch.ErrorCode);

            // The status surface reports the honest, unresolved state.
            var status = kernel.CaptureStatus();
            Assert.False(status.OwnerEnrolled);
            Assert.False(status.OwnerSessionActive);
            Assert.Equal(SecuritySubsystemStatus.Healthy, status.AuditStatus);
            Assert.Equal(SecuritySubsystemStatus.Unavailable, status.VaultStatus);
            Assert.Contains("unresolved", status.VaultAlgorithmSummary, StringComparison.OrdinalIgnoreCase);

            await host.Lifecycle.StopAsync();
        }
        finally
        {
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort temp cleanup.
            }
        }
    }

    [Fact]
    public void CompositionDeniesEveryPrivilegedPathWhenTheAuditLedgerFailsVerification()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aeron-kernel-tamper-{Guid.NewGuid():N}");

        try
        {
            var paths = AppPaths.FromRoot(path);
            paths.EnsureCreatedAndSecured();
            var redaction = new RedactionFilter();
            var clock = new SystemClock();

            var first = SecurityComposition.CreateKernel(paths, clock, redaction);
            Assert.NotNull(first.Kernel);
            Assert.True(first.IsOperational);
            first.Kernel!.Dispose();

            var auditPath = Path.Combine(paths.DataDirectory, "security", SecurityComposition.AuditFileName);
            Assert.True(File.Exists(auditPath));

            var bytes = File.ReadAllBytes(auditPath);
            Assert.True(bytes.Length >= DurableAuditLedger.RecordSize);

            // Flip a byte inside the recorded correlation id of the first record. The
            // record hash no longer matches its content, so verification must fail; the
            // kernel must then refuse every privileged path.
            bytes[20] ^= 0x01;
            File.WriteAllBytes(auditPath, bytes);

            var restarted = SecurityComposition.CreateKernel(paths, clock, redaction);
            Assert.NotNull(restarted.Kernel);
            Assert.False(restarted.IsOperational);
            Assert.Contains("fail-closed", restarted.StatusText, StringComparison.OrdinalIgnoreCase);
            Assert.False(restarted.Kernel!.IsOperational);

            restarted.Kernel.Dispose();
        }
        finally
        {
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort temp cleanup.
            }
        }
    }

    [Fact]
    public async Task FoundationHostSecuritySurvivesRestartWithAnIntactChain()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aeron-kernel-restart-{Guid.NewGuid():N}");

        try
        {
            var paths = AppPaths.FromRoot(path);

            await using (var first = AeronFoundationHost.Create(paths))
            {
                await first.Lifecycle.StartAsync();
                Assert.True(first.SecurityKernel.IsOperational);
                await first.Lifecycle.StopAsync();
            }

            await using (var second = AeronFoundationHost.Create(paths))
            {
                await second.Lifecycle.StartAsync();
                Assert.True(second.SecurityKernel.IsOperational);

                var sequence = second.SecurityKernel.Kernel!.Audit.LastVerifiedSequence;
                Assert.True(sequence >= 1);
                await second.Lifecycle.StopAsync();
            }
        }
        finally
        {
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort temp cleanup.
            }
        }
    }
}
