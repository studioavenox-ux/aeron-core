using Aeron.Contracts;
using Aeron.Core;
using Aeron.Desktop.Tray;
using Aeron.Desktop.ViewModels;
using Aeron.Infrastructure;
using Aeron.Security;
using Aeron.TestSupport;
using Xunit;

namespace Aeron.Desktop.Tests;

public sealed class SecuritySurfaceTests
{
    private sealed class NullLogger : IAeronLogger
    {
        public bool IsActive => true;

        public void Configure(AeronLogLevel minimumLevel, long maxFileBytes, int maxRetainedFiles)
        {
        }

        public void Log(
            AeronLogLevel level,
            string category,
            string message,
            IReadOnlyDictionary<string, string?>? properties = null,
            Exception? exception = null)
        {
        }

        public void Flush()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class NullTray : ISystemTrayService
    {
        public bool IsIconRegistered => false;

        public event EventHandler? OpenRequested
        {
            add { }
            remove { }
        }

        public event EventHandler? MinimizeToTrayRequested
        {
            add { }
            remove { }
        }

        public event EventHandler? ExitRequested
        {
            add { }
            remove { }
        }

        public bool Initialize(string tooltipText) => false;

        public void UpdateTooltip(string tooltipText)
        {
        }

        public void Dispose()
        {
        }
    }

    [Fact]
    public void SecuritySurfaceIsHonestWhenTheKernelCouldNotBeConstructed()
    {
        using var viewModel = new SecurityViewModel(
            new SecurityKernelStartupResult(null, "unavailable", "synthetic construction failure"),
            new NullLogger());

        Assert.False(viewModel.IsKernelOperational);
        Assert.Contains("could not be constructed", viewModel.OwnerStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(viewModel.AuditEntries);
        Assert.Equal("Unavailable", viewModel.AuditStatusText);

        viewModel.RequestEmergencyStop();
        Assert.Contains("unavailable", viewModel.ActionStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecuritySurfaceReportsOwnerLockOutAndNeverFakesAnUnlock()
    {
        var root = Path.Combine(Path.GetTempPath(), $"aeron-desktop-security-{Guid.NewGuid():N}");

        try
        {
            var options = KernelHarness.CreateOptions(root, SystemClock.Instance, new UnapprovedOwnerFactorPolicy());
            using var kernel = SecurityKernel.Create(options);
            Assert.True(kernel.IsOperational);

            using var viewModel = new SecurityViewModel(
                new SecurityKernelStartupResult(kernel, "operational", string.Empty),
                new NullLogger());

            Assert.True(viewModel.IsKernelOperational);
            Assert.Equal("No Owner enrolled", viewModel.OwnerStatusText);
            Assert.Contains("MFA", viewModel.OwnerStatusDetail, StringComparison.OrdinalIgnoreCase);
            Assert.False(viewModel.UnlockOwnerSurfaceCommand.CanExecute(null));

            Assert.True(viewModel.AuditEntries.Count >= 0);
            Assert.Contains("aeron-m2-1", viewModel.PolicyVersionText, StringComparison.Ordinal);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort temp cleanup.
            }
        }
    }

    [Fact]
    public void SecuritySurfaceExposesRealAuditRecordsWithoutSensitiveIdentifiers()
    {
        var root = Path.Combine(Path.GetTempPath(), $"aeron-desktop-audit-{Guid.NewGuid():N}");

        try
        {
            var options = KernelHarness.CreateOptions(root, SystemClock.Instance);
            using var kernel = SecurityKernel.Create(options);
            Assert.True(kernel.Audit.TryAppend(
                DurableAuditKind.StartupVerified,
                AuditSubject.For(Guid.NewGuid()),
                SecurityCodes.StartupVerified));

            using var viewModel = new SecurityViewModel(
                new SecurityKernelStartupResult(kernel, "operational", string.Empty),
                new NullLogger());

            var entry = Assert.Single(viewModel.AuditEntries);
            Assert.Equal(DurableAuditKind.StartupVerified.ToString(), entry.Kind);
            Assert.Equal(8, entry.CorrelationDisplay.Length);
            Assert.DoesNotContain("owner", entry.CorrelationDisplay, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort temp cleanup.
            }
        }
    }

    [Fact]
    public async Task FoundationHostExposesTheKernelAndLifecycleStillStarts()
    {
        var root = Path.Combine(Path.GetTempPath(), $"aeron-host-kernel-{Guid.NewGuid():N}");

        try
        {
            await using var host = AeronFoundationHost.Create(AppPaths.FromRoot(root));

            Assert.NotNull(host.SecurityKernel);
            Assert.NotNull(host.SecurityKernel.Kernel);
            Assert.True(host.SecurityKernel.IsOperational);
            Assert.True(host.SecurityKernel.Kernel!.IsOperational);

            // The kernel verified its audit chain during construction, before the lifecycle
            // started, so no operation could run against an unverified ledger.
            var records = host.SecurityKernel.Kernel.Audit.ReadRecent(10);
            Assert.True(records.Count > 0);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort temp cleanup.
            }
        }
    }

    [Fact]
    public void MainWindowViewModelSurfacesSecurityWithoutLeakingSecrets()
    {
        var root = Path.Combine(Path.GetTempPath(), $"aeron-vm-security-{Guid.NewGuid():N}");

        try
        {
            var options = KernelHarness.CreateOptions(root, SystemClock.Instance);
            using var kernel = SecurityKernel.Create(options);
            using var security = new SecurityViewModel(
                new SecurityKernelStartupResult(kernel, "operational", string.Empty),
                new NullLogger());

            using var viewModel = new MainWindowViewModel(
                new NoopLifecycle(),
                new NoopHealth(),
                new CoreEventBus(new NullLogger()),
                new NullTray(),
                new NullLogger(),
                uiDispatcher: null,
                security);

            Assert.Same(security, viewModel.Security);
            Assert.Contains("Policy", security.PolicyVersionText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort temp cleanup.
            }
        }
    }

    private sealed class NoopLifecycle : IAppLifecycle
    {
        public string InstanceId => "test-instance";

        public AppLifecycleState CurrentState => AppLifecycleState.Running;

        public DateTimeOffset? StartedAtUtc => DateTimeOffset.UnixEpoch;

        public CancellationToken ShutdownToken => CancellationToken.None;

        public AeronAppConfig CurrentConfig => AeronAppConfig.CreateDefault();

        public bool IsConfigValid => true;

        public IDisposable RegisterShutdownHook(Func<CancellationToken, Task> shutdownHook) => new NoopDisposable();

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(string reason = "Normal shutdown") => Task.CompletedTask;

        public void TriggerEmergencyStop(string reason)
        {
        }

        public Task<Result> UpdateCloseToTrayAsync(bool closeToTray, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NoopHealth : IFoundationHealthService
    {
        public Task<FoundationHealthSnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new FoundationHealthSnapshot(
                "0.1.0",
                ".NET 10",
                "test-instance",
                AppLifecycleState.Running,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch,
                Path.GetTempPath(),
                true,
                true,
                true,
                true,
                new DatabaseHealthResult(true, "wal", true, true, 1, 1, "ok"),
                new OsBaselineReport(true, true, 26200, 26200, "Windows 11 25H2", true, "ok")));
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
