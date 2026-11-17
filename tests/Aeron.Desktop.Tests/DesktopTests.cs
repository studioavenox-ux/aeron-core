using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Aeron.Contracts;
using Aeron.Core;
using Aeron.Desktop.SingleInstance;
using Aeron.Desktop.Tray;
using Aeron.Desktop.ViewModels;
using Aeron.Infrastructure;
using Xunit;

namespace Aeron.Desktop.Tests;

public sealed class SingleInstanceManagerTests
{
    [Fact]
    public async Task SingleInstanceManager_BlocksSecondaryAndDeliversActivationSignalOverNamedPipe()
    {
        var scope = $"aeron-test-ipc-{Guid.NewGuid():N}";
        var firstActivation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondActivation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var activationCount = 0;

        await using (var primary = SingleInstanceManager.Acquire(scope))
        {
            Assert.True(primary.IsPrimaryInstance);
            Assert.False(primary.RecoveredAbandonedMutex);

            primary.StartActivationListener(() =>
            {
                var count = Interlocked.Increment(ref activationCount);
                if (count == 1)
                {
                    firstActivation.TrySetResult(true);
                }
                else if (count == 2)
                {
                    secondActivation.TrySetResult(true);
                }
            });

            await using (var secondaryOne = SingleInstanceManager.Acquire(scope))
            {
                Assert.False(secondaryOne.IsPrimaryInstance);
                var signaled = await secondaryOne.TrySignalPrimaryInstanceAsync(TimeSpan.FromSeconds(5));
                Assert.True(signaled);
            }

            await firstActivation.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await using (var secondaryTwo = SingleInstanceManager.Acquire(scope))
            {
                Assert.False(secondaryTwo.IsPrimaryInstance);
                var signaled = await secondaryTwo.TrySignalPrimaryInstanceAsync(TimeSpan.FromSeconds(5));
                Assert.True(signaled);
            }

            await secondActivation.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(2, activationCount);
        }

        // After clean disposal of primary, a new launch must cleanly acquire ownership without abandoned recovery.
        await using var successor = SingleInstanceManager.Acquire(scope);
        Assert.True(successor.IsPrimaryInstance);
        Assert.False(successor.RecoveredAbandonedMutex);
    }

    [Fact]
    public async Task SingleInstanceManager_RecoversCleanlyFromAbandonedMutex()
    {
        var scope = $"aeron-test-abandoned-{Guid.NewGuid():N}";
        var (mutexName, _) = SingleInstanceManager.BuildScopedChannelNames(scope);

        Mutex? leakedHandle = null;
        try
        {
            var abandonThread = new Thread(() =>
            {
                leakedHandle = new Mutex(initiallyOwned: false, name: mutexName);
                _ = leakedHandle.WaitOne(0);
                // Intentionally exit thread without calling ReleaseMutex to simulate a crashed prior instance.
            })
            {
                IsBackground = true
            };

            abandonThread.Start();
            abandonThread.Join(TimeSpan.FromSeconds(3));

            await using var recovered = SingleInstanceManager.Acquire(scope);
            Assert.True(recovered.IsPrimaryInstance);
            Assert.True(recovered.RecoveredAbandonedMutex);
        }
        finally
        {
            leakedHandle?.Dispose();
        }
    }

    [Fact]
    public void CreateCurrentUserPipeSecurity_RestrictsAccessStrictlyToCurrentWindowsUserSid()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var currentUserSid = identity.User;
        Assert.NotNull(currentUserSid);

        var security = SingleInstanceManager.CreateCurrentUserPipeSecurity();
        Assert.True(security.AreAccessRulesProtected);

        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier));
        Assert.Single(rules);

        var rule = Assert.IsType<PipeAccessRule>(rules[0]);
        Assert.Equal(currentUserSid, rule.IdentityReference);
        Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        Assert.Equal(PipeAccessRights.FullControl, (rule.PipeAccessRights & PipeAccessRights.FullControl));
    }

    [Fact]
    public async Task SingleInstanceManager_IgnoresUnrecognizedPipeCommand()
    {
        var scope = $"aeron-test-invalid-cmd-{Guid.NewGuid():N}";
        var activationCount = 0;
        var validActivation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var primary = SingleInstanceManager.Acquire(scope);
        primary.StartActivationListener(() =>
        {
            if (Interlocked.Increment(ref activationCount) == 1)
            {
                validActivation.TrySetResult(true);
            }
        });

        // Send an unrecognized command first.
        await using (var rogueClient = new NamedPipeClientStream(
                         ".",
                         primary.PipeName,
                         PipeDirection.Out,
                         PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
        {
            await rogueClient.ConnectAsync(TimeSpan.FromSeconds(5));
            var bogus = Encoding.UTF8.GetBytes("UNRECOGNIZED:COMMAND\n");
            await rogueClient.WriteAsync(bogus);
            await rogueClient.FlushAsync();
        }

        // Send a valid activation command afterward to prove the listener ignored bogus input and stayed healthy.
        await using var secondary = SingleInstanceManager.Acquire(scope);
        var sent = await secondary.TrySignalPrimaryInstanceAsync(TimeSpan.FromSeconds(5));
        Assert.True(sent);

        await validActivation.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, activationCount);
    }
}

public sealed class MainWindowViewModelAndTrayStateTests
{
    private sealed class FakeSystemTrayService : ISystemTrayService
    {
        public bool IsIconRegistered { get; private set; }
        public int DisposeCallCount { get; private set; }
        public string LastTooltip { get; private set; } = string.Empty;

        public event EventHandler? OpenRequested;
        public event EventHandler? MinimizeToTrayRequested;
        public event EventHandler? ExitRequested;

        public bool Initialize(string tooltipText)
        {
            IsIconRegistered = true;
            LastTooltip = tooltipText;
            return true;
        }

        public void UpdateTooltip(string tooltipText)
        {
            if (IsIconRegistered)
            {
                LastTooltip = tooltipText;
            }
        }

        public void RaiseOpen() => OpenRequested?.Invoke(this, EventArgs.Empty);
        public void RaiseMinimize() => MinimizeToTrayRequested?.Invoke(this, EventArgs.Empty);
        public void RaiseExit() => ExitRequested?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
            DisposeCallCount++;
            IsIconRegistered = false;
        }
    }

    private sealed class FixedOsBaselineHealthService : IFoundationHealthService
    {
        private readonly IFoundationHealthService _inner;
        private readonly int _simulatedBuild;

        public FixedOsBaselineHealthService(IFoundationHealthService inner, int simulatedBuild)
        {
            _inner = inner;
            _simulatedBuild = simulatedBuild;
        }

        public async Task<FoundationHealthSnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken = default)
        {
            var real = await _inner.CaptureSnapshotAsync(cancellationToken);
            var simulatedOs = OsBaselineEvaluator.EvaluateHost(
                isWindows: true,
                is64BitOperatingSystem: true,
                detectedBuild: _simulatedBuild);
            return real with { OsBaseline = simulatedOs };
        }
    }

    [Fact]
    public async Task ViewModel_CloseToTrayRestoreSecondaryActivationAndExplicitExit_BehaveDeterministically()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"aeron-vm-tests-{Guid.NewGuid():N}");
        try
        {
            await using var host = AeronFoundationHost.Create(AppPaths.FromRoot(tempRoot));
            await host.Lifecycle.StartAsync();

            var tray = new FakeSystemTrayService();
            Assert.True(tray.Initialize("AERON"));

            var hideCount = 0;
            var restoreCount = 0;
            var shutdownCount = 0;

            using var vm = new MainWindowViewModel(
                host.Lifecycle,
                new FixedOsBaselineHealthService(host.HealthService, simulatedBuild: 26200),
                host.EventBus,
                tray,
                host.Logger);

            vm.HideToTrayRequested += (_, _) => hideCount++;
            vm.RestoreWindowRequested += (_, _) => restoreCount++;
            vm.ShutdownApplicationRequested += (_, _) => shutdownCount++;

            await vm.InitializeAsync();

            Assert.True(vm.CloseToTray);
            Assert.True(vm.MeetsOsBaseline);
            Assert.True(vm.IsDatabaseHealthy);
            Assert.True(vm.AreSecurityInvariantsLocked);
            Assert.Equal("Running", vm.LifecycleStateText);

            // Closing (X) minimizes to tray while keeping lifecycle Running and tray icon alive.
            var closeAction = vm.HandleWindowClosing();
            Assert.Equal(WindowCloseAction.MinimizeToTray, closeAction);
            Assert.True(vm.IsHiddenToTray);
            Assert.False(vm.IsWindowVisible);
            Assert.Equal(1, hideCount);
            Assert.Equal(AppLifecycleState.Running, host.Lifecycle.CurrentState);
            Assert.True(tray.IsIconRegistered);
            Assert.Equal(0, tray.DisposeCallCount);

            // Tray Open restores window state.
            tray.RaiseOpen();
            Assert.False(vm.IsHiddenToTray);
            Assert.True(vm.IsWindowVisible);
            Assert.Equal(1, restoreCount);

            // Tray Minimize hides window again, and secondary-instance event restores it.
            tray.RaiseMinimize();
            Assert.True(vm.IsHiddenToTray);
            Assert.Equal(2, hideCount);

            host.EventBus.Publish(new WindowActivationRequestedEvent("SecondaryInstance", DateTimeOffset.UtcNow));
            Assert.False(vm.IsHiddenToTray);
            Assert.True(vm.IsWindowVisible);
            Assert.Equal(2, restoreCount);

            // Explicit Exit disposes tray icon, stops lifecycle, and allows window close.
            await vm.RequestExitAsync("Unit test exit");
            await vm.RequestExitAsync("Idempotent second exit call");

            Assert.True(vm.IsExplicitExitInProgress);
            Assert.False(tray.IsIconRegistered);
            Assert.Equal(1, tray.DisposeCallCount);
            Assert.Equal(AppLifecycleState.Stopped, host.Lifecycle.CurrentState);
            Assert.Equal(1, shutdownCount);
            Assert.Equal(WindowCloseAction.AllowShutdown, vm.HandleWindowClosing());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ViewModel_ConfigurableCloseToTrayAndBelowBaselineDetection_WorkCorrectly()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"aeron-vm-cfg-{Guid.NewGuid():N}");
        try
        {
            await using var host = AeronFoundationHost.Create(AppPaths.FromRoot(tempRoot));
            await host.Lifecycle.StartAsync();

            var tray = new FakeSystemTrayService();
            tray.Initialize("AERON");

            using var vm = new MainWindowViewModel(
                host.Lifecycle,
                new FixedOsBaselineHealthService(host.HealthService, simulatedBuild: 22621),
                host.EventBus,
                tray,
                host.Logger);

            await vm.InitializeAsync();

            // Windows 11 22H2 (Build 22621) must be flagged below the Windows 11 25H2 (26200) baseline.
            Assert.False(vm.MeetsOsBaseline);
            Assert.Equal("BELOW MINIMUM OS BASELINE", vm.OsBaselineBadgeText);

            // Disabling CloseToTray persists to config and returns AllowShutdown on window close.
            var saveResult = await vm.SetCloseToTrayAsync(false);
            Assert.True(saveResult.IsSuccess);
            Assert.False(vm.CloseToTray);
            Assert.Equal(WindowCloseAction.AllowShutdown, vm.HandleWindowClosing());

            var persisted = host.ConfigStore.LoadOrCreateDefault();
            Assert.True(persisted.IsSuccess);
            Assert.False(persisted.Value.Window.CloseToTray);

            // Tray command exit path works cleanly.
            var shutdownTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            vm.ShutdownApplicationRequested += (_, _) => shutdownTcs.TrySetResult(true);
            tray.RaiseExit();
            await shutdownTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(AppLifecycleState.Stopped, host.Lifecycle.CurrentState);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
