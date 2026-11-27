using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Aeron.Contracts;
using Aeron.Desktop.Tray;

namespace Aeron.Desktop.ViewModels;

/// <summary>
/// Decision produced when the user clicks the main window close button (X).
/// </summary>
public enum WindowCloseAction
{
    MinimizeToTray = 0,
    AllowShutdown = 1
}

/// <summary>
/// MVVM presentation state for AERON's Milestone 1 Foundation window.
/// Contains zero direct SQLite, filesystem, or network dependencies and keeps all
/// close-to-tray, activation, and exit state transitions independently unit-testable.
/// </summary>
public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IAppLifecycle _lifecycle;
    private readonly IFoundationHealthService _healthService;
    private readonly ICoreEventBus _eventBus;
    private readonly ISystemTrayService _trayService;
    private readonly IAeronLogger _logger;
    private readonly Action<Action> _uiDispatcher;
    private readonly List<IDisposable> _subscriptions = [];

    private string _applicationVersion = "0.1.0";
    private string _runtimeDescription = ".NET 10";
    private string _instanceId = string.Empty;
    private string _lifecycleStateText = AppLifecycleState.Uninitialized.ToString();
    private string _startedAtDisplay = "Pending";
    private string _lastCheckedDisplay = "Pending";
    private string _appDataRootPath = string.Empty;
    private string _configStatusText = "Validating";
    private string _databaseStatusText = "Initializing";
    private string _databaseModeText = "WAL / FK=ON";
    private string _schemaVersionText = "v1";
    private string _loggerStatusText = "Active (Redaction Enforced)";
    private string _securityPolicyText = "Locked (Remote-Only · Zero Paid/Local Fallback · No v1 Skill Scripts)";
    private string _osBaselineBadgeText = "Checking";
    private string _osBaselineSummary = string.Empty;
    private string _statusMessage = "Initializing AERON foundation...";
    private bool _meetsOsBaseline = true;
    private bool _isDatabaseHealthy;
    private bool _areSecurityInvariantsLocked;
    private bool _closeToTray = true;
    private bool _isHiddenToTray;
    private bool _isWindowVisible = true;
    private bool _suppressCloseToTraySave;
    private int _exitInitiated;
    private int _disposed;

    public MainWindowViewModel(
        IAppLifecycle lifecycle,
        IFoundationHealthService healthService,
        ICoreEventBus eventBus,
        ISystemTrayService trayService,
        IAeronLogger logger,
        Action<Action>? uiDispatcher = null,
        SecurityViewModel? security = null)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(healthService);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(trayService);
        ArgumentNullException.ThrowIfNull(logger);

        _lifecycle = lifecycle;
        _healthService = healthService;
        _eventBus = eventBus;
        _trayService = trayService;
        _logger = logger;
        _uiDispatcher = uiDispatcher ?? (static action => action());
        Security = security;

        _instanceId = lifecycle.InstanceId;
        _lifecycleStateText = lifecycle.CurrentState.ToString();
        _closeToTray = lifecycle.CurrentConfig.Window.CloseToTray;

        RefreshCommand = new AsyncRelayCommand(() => RefreshHealthAsync(), logger: _logger);
        MinimizeToTrayCommand = new RelayCommand(MinimizeToTray);
        RestoreWindowCommand = new RelayCommand(() => RestoreWindow("UserCommand"));
        ExitApplicationCommand = new AsyncRelayCommand(() => RequestExitAsync("User requested exit"), logger: _logger);

        _trayService.OpenRequested += OnTrayOpenRequested;
        _trayService.MinimizeToTrayRequested += OnTrayMinimizeRequested;
        _trayService.ExitRequested += OnTrayExitRequested;

        _subscriptions.Add(_eventBus.Subscribe<LifecycleStateChangedEvent>(OnLifecycleStateChanged));
        _subscriptions.Add(_eventBus.Subscribe<WindowActivationRequestedEvent>(OnActivationRequested));
        _subscriptions.Add(_eventBus.Subscribe<ConfigurationUpdatedEvent>(OnConfigurationUpdated));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? HideToTrayRequested;
    public event EventHandler? RestoreWindowRequested;
    public event EventHandler? ShutdownApplicationRequested;

    /// <summary>
    /// M2 Owner security surface. Null only in foundation-only test constructions; the
    /// production shell always supplies one so security status is never hidden.
    /// </summary>
    public SecurityViewModel? Security { get; }

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand MinimizeToTrayCommand { get; }
    public RelayCommand RestoreWindowCommand { get; }
    public AsyncRelayCommand ExitApplicationCommand { get; }

    public string ApplicationVersion
    {
        get => _applicationVersion;
        private set => SetProperty(ref _applicationVersion, value);
    }

    public string RuntimeDescription
    {
        get => _runtimeDescription;
        private set => SetProperty(ref _runtimeDescription, value);
    }

    public string InstanceId
    {
        get => _instanceId;
        private set => SetProperty(ref _instanceId, value);
    }

    public string LifecycleStateText
    {
        get => _lifecycleStateText;
        private set => SetProperty(ref _lifecycleStateText, value);
    }

    public string StartedAtDisplay
    {
        get => _startedAtDisplay;
        private set => SetProperty(ref _startedAtDisplay, value);
    }

    public string LastCheckedDisplay
    {
        get => _lastCheckedDisplay;
        private set => SetProperty(ref _lastCheckedDisplay, value);
    }

    public string AppDataRootPath
    {
        get => _appDataRootPath;
        private set => SetProperty(ref _appDataRootPath, value);
    }

    public string ConfigStatusText
    {
        get => _configStatusText;
        private set => SetProperty(ref _configStatusText, value);
    }

    public string DatabaseStatusText
    {
        get => _databaseStatusText;
        private set => SetProperty(ref _databaseStatusText, value);
    }

    public string DatabaseModeText
    {
        get => _databaseModeText;
        private set => SetProperty(ref _databaseModeText, value);
    }

    public string SchemaVersionText
    {
        get => _schemaVersionText;
        private set => SetProperty(ref _schemaVersionText, value);
    }

    public string LoggerStatusText
    {
        get => _loggerStatusText;
        private set => SetProperty(ref _loggerStatusText, value);
    }

    public string SecurityPolicyText
    {
        get => _securityPolicyText;
        private set => SetProperty(ref _securityPolicyText, value);
    }

    public string OsBaselineBadgeText
    {
        get => _osBaselineBadgeText;
        private set => SetProperty(ref _osBaselineBadgeText, value);
    }

    public string OsBaselineSummary
    {
        get => _osBaselineSummary;
        private set => SetProperty(ref _osBaselineSummary, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool MeetsOsBaseline
    {
        get => _meetsOsBaseline;
        private set => SetProperty(ref _meetsOsBaseline, value);
    }

    public bool IsDatabaseHealthy
    {
        get => _isDatabaseHealthy;
        private set => SetProperty(ref _isDatabaseHealthy, value);
    }

    public bool AreSecurityInvariantsLocked
    {
        get => _areSecurityInvariantsLocked;
        private set => SetProperty(ref _areSecurityInvariantsLocked, value);
    }

    public bool CloseToTray
    {
        get => _closeToTray;
        set
        {
            if (!SetProperty(ref _closeToTray, value) || _suppressCloseToTraySave)
            {
                return;
            }

            _ = PersistCloseToTrayPreferenceAsync(value);
        }
    }

    public bool IsHiddenToTray
    {
        get => _isHiddenToTray;
        private set => SetProperty(ref _isHiddenToTray, value);
    }

    public bool IsWindowVisible
    {
        get => _isWindowVisible;
        private set => SetProperty(ref _isWindowVisible, value);
    }

    public bool IsExplicitExitInProgress => Volatile.Read(ref _exitInitiated) != 0;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshHealthAsync(cancellationToken).ConfigureAwait(true);

        if (_lifecycle.CurrentConfig.Window.StartMinimizedToTray)
        {
            MinimizeToTray();
        }
        else
        {
            StatusMessage = "AERON Milestone 1 foundation ready. Closing (X) minimizes to the system tray.";
        }
    }

    public async Task RefreshHealthAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _healthService.CaptureSnapshotAsync(cancellationToken).ConfigureAwait(false);
        _uiDispatcher(() =>
        {
            ApplySnapshot(snapshot);
            Security?.Refresh();
        });
    }

    public async Task<Result> SetCloseToTrayAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var result = await _lifecycle.UpdateCloseToTrayAsync(enabled, cancellationToken).ConfigureAwait(false);
        _uiDispatcher(() =>
        {
            if (result.IsSuccess)
            {
                SetCloseToTraySilently(enabled);
                StatusMessage = enabled
                    ? "Close button (X) set to minimize AERON to the system tray."
                    : "Close button (X) set to exit AERON completely.";
            }
            else
            {
                SetCloseToTraySilently(_lifecycle.CurrentConfig.Window.CloseToTray);
                StatusMessage = $"Could not save window preference ({result.ErrorCode}).";
            }
        });

        return result;
    }

    /// <summary>
    /// Evaluates window close behavior per D-M1-03: hides to system tray when CloseToTray is enabled
    /// and explicit exit has not been requested; allows full shutdown otherwise.
    /// </summary>
    public WindowCloseAction HandleWindowClosing()
    {
        if (IsExplicitExitInProgress || !CloseToTray)
        {
            return WindowCloseAction.AllowShutdown;
        }

        MinimizeToTray();
        return WindowCloseAction.MinimizeToTray;
    }

    public void MinimizeToTray()
    {
        if (IsExplicitExitInProgress)
        {
            return;
        }

        IsHiddenToTray = true;
        IsWindowVisible = false;
        StatusMessage = "Running quietly in the Windows system tray.";
        _trayService.UpdateTooltip("AERON — Running in System Tray");
        _logger.LogInfo(nameof(MainWindowViewModel), "Main window minimized to system tray.");
        HideToTrayRequested?.Invoke(this, EventArgs.Empty);
    }

    public void RestoreWindow(string source)
    {
        if (IsExplicitExitInProgress)
        {
            return;
        }

        IsHiddenToTray = false;
        IsWindowVisible = true;
        StatusMessage = "AERON foundation active.";
        _trayService.UpdateTooltip("AERON — Foundation Running");
        _logger.LogInfo(
            nameof(MainWindowViewModel),
            "Main window restored to foreground.",
            new Dictionary<string, string?> { ["source"] = source });
        RestoreWindowRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task RequestExitAsync(string reason = "Exit requested")
    {
        if (Interlocked.CompareExchange(ref _exitInitiated, 1, 0) != 0)
        {
            return;
        }

        _uiDispatcher(() =>
        {
            StatusMessage = "Shutting down AERON cleanly...";
            IsHiddenToTray = false;
            IsWindowVisible = false;
        });

        // Remove tray icon immediately so zero ghost icons remain in the Windows 11 notification area.
        _trayService.Dispose();

        await _lifecycle.StopAsync(reason).ConfigureAwait(false);

        _uiDispatcher(() =>
        {
            LifecycleStateText = _lifecycle.CurrentState.ToString();
            ShutdownApplicationRequested?.Invoke(this, EventArgs.Empty);
        });
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _trayService.OpenRequested -= OnTrayOpenRequested;
        _trayService.MinimizeToTrayRequested -= OnTrayMinimizeRequested;
        _trayService.ExitRequested -= OnTrayExitRequested;

        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
        _trayService.Dispose();
    }

    private void ApplySnapshot(FoundationHealthSnapshot snapshot)
    {
        ApplicationVersion = $"v{snapshot.ApplicationVersion}";
        RuntimeDescription = snapshot.RuntimeDescription;
        InstanceId = snapshot.InstanceId;
        LifecycleStateText = snapshot.LifecycleState.ToString();
        StartedAtDisplay = snapshot.StartedAtUtc?.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)
                           ?? "Not started";
        LastCheckedDisplay = snapshot.CapturedAtUtc.UtcDateTime.ToString("HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
        AppDataRootPath = snapshot.AppDataRootPath;
        ConfigStatusText = snapshot.ConfigValid
            ? "Validated (User-ACL Isolated · Secret-Free)"
            : "Validation Failed (Fail-Closed)";

        SetCloseToTraySilently(snapshot.CloseToTrayEnabled);

        IsDatabaseHealthy = snapshot.Database.IsHealthy;
        DatabaseStatusText = snapshot.Database.IsHealthy ? "Healthy (Verified)" : "Attention Required";
        DatabaseModeText =
            $"Journal: {snapshot.Database.JournalMode.ToUpperInvariant()} · Foreign Keys: {(snapshot.Database.ForeignKeysEnabled ? "ON" : "OFF")}";
        SchemaVersionText =
            $"Schema v{snapshot.Database.LatestSchemaVersion.ToString(CultureInfo.InvariantCulture)} ({snapshot.Database.AppliedMigrationCount.ToString(CultureInfo.InvariantCulture)} checksum-verified migration)";

        LoggerStatusText = snapshot.RedactingLoggerActive
            ? "Active (JSONL · Mandatory Redaction · Size Rotation)"
            : "Inactive";

        AreSecurityInvariantsLocked = snapshot.SecurityInvariantsLocked;
        SecurityPolicyText = snapshot.SecurityInvariantsLocked
            ? "Enforced (Remote-Only · Zero Paid/Local Fallback · Zero v1 Skill Scripts)"
            : "Policy Violation Detected";

        MeetsOsBaseline = snapshot.OsBaseline.MeetsMinimumBaseline;
        OsBaselineBadgeText = snapshot.OsBaseline.MeetsMinimumBaseline
            ? "OS BASELINE COMPLIANT"
            : "BELOW MINIMUM OS BASELINE";
        OsBaselineSummary = snapshot.OsBaseline.Summary;
    }

    private async Task PersistCloseToTrayPreferenceAsync(bool enabled)
    {
        try
        {
            await SetCloseToTrayAsync(enabled).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(nameof(MainWindowViewModel), "Failed to update CloseToTray preference.", ex);
        }
    }

    private void SetCloseToTraySilently(bool value)
    {
        _suppressCloseToTraySave = true;
        try
        {
            SetProperty(ref _closeToTray, value, nameof(CloseToTray));
        }
        finally
        {
            _suppressCloseToTraySave = false;
        }
    }

    private void OnTrayOpenRequested(object? sender, EventArgs e) =>
        _uiDispatcher(() => RestoreWindow("SystemTray"));

    private void OnTrayMinimizeRequested(object? sender, EventArgs e) =>
        _uiDispatcher(MinimizeToTray);

    private async void OnTrayExitRequested(object? sender, EventArgs e)
    {
        try
        {
            await RequestExitAsync("System tray Exit command").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(nameof(MainWindowViewModel), "Tray exit handler failed.", ex);
        }
    }

    private void OnLifecycleStateChanged(LifecycleStateChangedEvent evt) =>
        _uiDispatcher(() => LifecycleStateText = evt.CurrentState.ToString());

    private void OnActivationRequested(WindowActivationRequestedEvent evt) =>
        _uiDispatcher(() => RestoreWindow(evt.Source));

    private void OnConfigurationUpdated(ConfigurationUpdatedEvent evt) =>
        _uiDispatcher(() => SetCloseToTraySilently(evt.CloseToTray));

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
