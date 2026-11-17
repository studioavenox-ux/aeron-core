using System.Windows;
using System.Windows.Threading;
using Aeron.Contracts;
using Aeron.Core;
using Aeron.Desktop.SingleInstance;
using Aeron.Desktop.Tray;
using Aeron.Desktop.ViewModels;

namespace Aeron.Desktop;

/// <summary>
/// Native WPF application entry host for AERON Milestone 1.
/// Manages single-instance coordination, foundation lifecycle bootstrap,
/// system tray integration, redacted fault handling, and clean resource disposal.
/// </summary>
public partial class App : Application
{
    private const string ProductionInstanceScope = "Aeron.Desktop.v1";

    private SingleInstanceManager? _singleInstance;
    private AeronFoundationHost? _host;
    private Win32SystemTrayService? _trayService;
    private MainWindowViewModel? _mainViewModel;
    private MainWindow? _mainWindow;
    private int _teardownStarted;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _singleInstance = SingleInstanceManager.Acquire(ProductionInstanceScope);
            if (!_singleInstance.IsPrimaryInstance)
            {
                _ = await _singleInstance
                    .TrySignalPrimaryInstanceAsync(TimeSpan.FromSeconds(3))
                    .ConfigureAwait(true);
                await _singleInstance.DisposeAsync().ConfigureAwait(true);
                _singleInstance = null;
                Shutdown(0);
                return;
            }

            _host = AeronFoundationHost.Create();
            AttachGlobalSafetyHandlers();

            if (_singleInstance.RecoveredAbandonedMutex)
            {
                _host.Logger.LogWarning(
                    nameof(App),
                    "Recovered single-instance ownership after a previous abnormal process termination.");
            }

            _singleInstance.StartActivationListener(
                () => _host.EventBus.Publish(
                    new WindowActivationRequestedEvent("SecondaryInstance", DateTimeOffset.UtcNow)),
                _host.Logger);

            await _host.Lifecycle.StartAsync().ConfigureAwait(true);

            _trayService = new Win32SystemTrayService(_host.Logger);
            _ = _trayService.Initialize("AERON — Foundation Running");

            _mainViewModel = new MainWindowViewModel(
                _host.Lifecycle,
                _host.HealthService,
                _host.EventBus,
                _trayService,
                _host.Logger,
                DispatchToUi);
            _mainViewModel.ShutdownApplicationRequested += OnShutdownApplicationRequested;

            _mainWindow = new MainWindow(_mainViewModel);
            MainWindow = _mainWindow;

            await _mainViewModel.InitializeAsync().ConfigureAwait(true);

            if (_host.Lifecycle.CurrentConfig.Window.StartMinimizedToTray)
            {
                _mainWindow.HideToSystemTray();
            }
            else
            {
                _mainWindow.RestoreToForeground();
            }
        }
        catch (Exception ex)
        {
            var safeSummary = _host is not null
                ? _host.RedactionFilter.Redact(ex.Message)
                : "Startup initialization failed before logging became available.";

            _host?.Logger.LogCritical(nameof(App), "Fatal AERON startup failure.", ex);

            MessageBox.Show(
                $"AERON failed a mandatory startup verification check and stopped safely.\n\n{safeSummary}",
                "AERON — Startup Halted",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            await TeardownAsync("Fatal startup rollback").ConfigureAwait(true);
            Shutdown(1);
        }
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        base.OnSessionEnding(e);
        _trayService?.Dispose();
        _host?.Lifecycle.TriggerEmergencyStop($"Windows session ending ({e.ReasonSessionEnding})");
    }

    private void AttachGlobalSafetyHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _host?.Logger.LogCritical(nameof(App), "Unhandled WPF Dispatcher exception.", e.Exception);
        _host?.Logger.Flush();
        _trayService?.Dispose();
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _host?.Logger.LogError(nameof(App), "Unobserved background task exception.", e.Exception);
        e.SetObserved();
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        _host?.Logger.LogCritical(nameof(App), "Unhandled AppDomain exception.", ex);
        _host?.Logger.Flush();
        _trayService?.Dispose();
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        // Guarantee idempotent system tray cleanup even on abrupt process exit.
        _trayService?.Dispose();
        _host?.Logger.Flush();
    }

    private async void OnShutdownApplicationRequested(object? sender, EventArgs e)
    {
        if (_mainWindow is not null)
        {
            _mainWindow.Close();
            _mainWindow = null;
        }

        await TeardownAsync("Clean application exit").ConfigureAwait(true);
        _ = Dispatcher.InvokeAsync(() => Shutdown(0), DispatcherPriority.Normal);
    }

    private async Task TeardownAsync(string reason)
    {
        if (Interlocked.Exchange(ref _teardownStarted, 1) != 0)
        {
            return;
        }

        if (_mainViewModel is not null)
        {
            _mainViewModel.ShutdownApplicationRequested -= OnShutdownApplicationRequested;
            _mainViewModel.Dispose();
            _mainViewModel = null;
        }

        _trayService?.Dispose();
        _trayService = null;

        if (_singleInstance is not null)
        {
            await _singleInstance.DisposeAsync().ConfigureAwait(false);
            _singleInstance = null;
        }

        if (_host is not null)
        {
            await _host.Lifecycle.StopAsync(reason).ConfigureAwait(false);
            await _host.DisposeAsync().ConfigureAwait(false);
            _host = null;
        }
    }

    private void DispatchToUi(Action action)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _ = Dispatcher.InvokeAsync(action, DispatcherPriority.Normal);
    }
}
