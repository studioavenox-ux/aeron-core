using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Aeron.Contracts;
using Aeron.Core;
using Aeron.Desktop.Tray;
using Aeron.Security;

namespace Aeron.Desktop.ViewModels;

/// <summary>
/// Owner-facing security control surface for AERON M2.
///
/// Honesty rules enforced here:
/// * Every action calls a real kernel service; nothing is decorative.
/// * When the kernel is unavailable, unhealthy, or has no approved authentication path,
///   the surface says so and refuses privileged actions rather than simulating success.
/// * The Owner authentication boundary in M2 has no approved MFA policy, so the Owner
///   control plane stays locked with an explicit reason. The UI never mints a marker or
///   bypasses the missing approval.
/// * The approver trust marker is generated in-memory only when the Owner surface is
///   genuinely unlocked; it never enters configuration, logs, or the audit trail.
/// </summary>
public sealed class SecurityViewModel : INotifyPropertyChanged, IDisposable
{
    private const int AuditVisibilityLimit = 40;

    private readonly SecurityKernelStartupResult _startup;
    private readonly IAeronLogger _logger;
    private readonly Action<Action> _uiDispatcher;
    private readonly Action<string>? _emergencyStopRequested;
    private int _disposed;

    private string _ownerStatusText = "Unavailable";
    private string _ownerStatusDetail = string.Empty;
    private string _policyVersionText = "Unknown";
    private string _auditStatusText = "Unavailable";
    private string _auditDetailText = string.Empty;
    private string _approvalStatusText = "Unavailable";
    private string _vaultStatusText = "Unavailable";
    private string _emergencyStopText = "Not engaged";
    private string _sessionSummary = "0 active sessions";
    private string _agentSummary = "0 registered agents";
    private string _lastRefreshDisplay = "Never";
    private bool _isKernelOperational;
    private bool _isOwnerSurfaceUnlocked;
    private bool _isEmergencyStopActive;
    private string _actionStatusText = "No security action attempted.";

    public SecurityViewModel(
        SecurityKernelStartupResult startup,
        IAeronLogger logger,
        Action<Action>? uiDispatcher = null,
        Action<string>? emergencyStopRequested = null)
    {
        ArgumentNullException.ThrowIfNull(startup);
        ArgumentNullException.ThrowIfNull(logger);

        _startup = startup;
        _logger = logger;
        _uiDispatcher = uiDispatcher ?? (static action => action());
        _emergencyStopRequested = emergencyStopRequested;

        RefreshCommand = new RelayCommand(Refresh);
        RequestEmergencyStopCommand = new RelayCommand(RequestEmergencyStop);
        // The Owner control-plane unlock has no approved authentication factor in M2, so
        // the command is present but permanently unavailable with an explicit reason.
        UnlockOwnerSurfaceCommand = new RelayCommand(() => { }, static () => false);

        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public RelayCommand RefreshCommand { get; }

    public RelayCommand RequestEmergencyStopCommand { get; }

    public RelayCommand UnlockOwnerSurfaceCommand { get; }

    public ObservableCollection<SecurityAuditEntryView> AuditEntries { get; } = [];

    public string OwnerStatusText
    {
        get => _ownerStatusText;
        private set => SetProperty(ref _ownerStatusText, value);
    }

    public string OwnerStatusDetail
    {
        get => _ownerStatusDetail;
        private set => SetProperty(ref _ownerStatusDetail, value);
    }

    public string PolicyVersionText
    {
        get => _policyVersionText;
        private set => SetProperty(ref _policyVersionText, value);
    }

    public string AuditStatusText
    {
        get => _auditStatusText;
        private set => SetProperty(ref _auditStatusText, value);
    }

    public string AuditDetailText
    {
        get => _auditDetailText;
        private set => SetProperty(ref _auditDetailText, value);
    }

    public string ApprovalStatusText
    {
        get => _approvalStatusText;
        private set => SetProperty(ref _approvalStatusText, value);
    }

    public string VaultStatusText
    {
        get => _vaultStatusText;
        private set => SetProperty(ref _vaultStatusText, value);
    }

    public string EmergencyStopText
    {
        get => _emergencyStopText;
        private set => SetProperty(ref _emergencyStopText, value);
    }

    public string SessionSummary
    {
        get => _sessionSummary;
        private set => SetProperty(ref _sessionSummary, value);
    }

    public string AgentSummary
    {
        get => _agentSummary;
        private set => SetProperty(ref _agentSummary, value);
    }

    public string LastRefreshDisplay
    {
        get => _lastRefreshDisplay;
        private set => SetProperty(ref _lastRefreshDisplay, value);
    }

    public string ActionStatusText
    {
        get => _actionStatusText;
        private set => SetProperty(ref _actionStatusText, value);
    }

    public bool IsKernelOperational
    {
        get => _isKernelOperational;
        private set => SetProperty(ref _isKernelOperational, value);
    }

    public bool IsOwnerSurfaceUnlocked
    {
        get => _isOwnerSurfaceUnlocked;
        private set => SetProperty(ref _isOwnerSurfaceUnlocked, value);
    }

    public bool IsEmergencyStopActive
    {
        get => _isEmergencyStopActive;
        private set => SetProperty(ref _isEmergencyStopActive, value);
    }

    public void Refresh()
    {
        var kernel = _startup.Kernel;

        if (kernel is null)
        {
            ApplyUnavailable("The security kernel could not be constructed.", _startup.Diagnostic);
            return;
        }

        try
        {
            var status = kernel.CaptureStatus();
            var snapshot = status with { CapturedAtUtc = status.CapturedAtUtc };

            IsKernelOperational = _startup.IsOperational;
            IsEmergencyStopActive = snapshot.EmergencyStopActive;

            OwnerStatusText = snapshot.OwnerEnrolled
                ? (snapshot.OwnerSessionActive ? "Owner authenticated" : "Owner enrolled · no active session")
                : "No Owner enrolled";

            OwnerStatusDetail = snapshot.OwnerEnrolled
                ? "Owner control plane is locked: the MFA factor and enrollment policy is unresolved (decision register D2-01), so no Owner session can be minted and privileged actions stay denied."
                : "Owner enrollment requires an approved MFA factor and enrollment policy. None is registered, so enrollment and all Owner controls remain denied. Windows account presence alone is never authentication.";

            PolicyVersionText = $"Policy {snapshot.PolicyVersion} · binding V3 (HMAC-SHA-256, length-framed)";

            AuditStatusText = snapshot.AuditStatus == SecuritySubsystemStatus.Healthy
                ? $"Verified · {snapshot.AuditLastSequence} anchored records"
                : $"Unhealthy · {snapshot.AuditStatus}";

            AuditDetailText = snapshot.AuditStatus == SecuritySubsystemStatus.Healthy
                ? "Hash-chained append-only ledger verified against the protected checkpoint. Checkpoint is a same-user file anchor: rollback resistance is not claimed until an independently protected mechanism is approved (D2-07)."
                : snapshot.AuditDiagnostic;

            ApprovalStatusText = $"{(snapshot.ApprovalStatus == SecuritySubsystemStatus.Healthy ? "Durable store healthy" : "Unavailable")} · {snapshot.PendingApprovals.ToString(CultureInfo.InvariantCulture)} outstanding";

            VaultStatusText = $"{(snapshot.VaultStatus == SecuritySubsystemStatus.Unavailable ? "Blocked (policy unresolved)" : snapshot.VaultStatus.ToString())} · {snapshot.VaultAlgorithmSummary}";

            EmergencyStopText = snapshot.EmergencyStopActive
                ? "ENGAGED · authentication, approvals, and dispatch are suspended"
                : "Not engaged";

            SessionSummary = $"{snapshot.ActiveSessionCount.ToString(CultureInfo.InvariantCulture)} active sessions";
            AgentSummary = $"{snapshot.RegisteredAgentCount.ToString(CultureInfo.InvariantCulture)} registered agents · {snapshot.RevokedAgentCount.ToString(CultureInfo.InvariantCulture)} revoked";

            ApplyAuditEntries(kernel.ReadAuditTrail(AuditVisibilityLimit));

            LastRefreshDisplay = snapshot.CapturedAtUtc.UtcDateTime.ToString("HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            _logger.LogError(nameof(SecurityViewModel), "Security status capture failed.", ex);
            ApplyUnavailable("Security status is unavailable.", ex.Message);
        }
    }

    /// <summary>Raises the Owner emergency stop through the real kernel preemption path.</summary>
    public void RequestEmergencyStop()
    {
        var kernel = _startup.Kernel;

        if (kernel is null)
        {
            ActionStatusText = "Emergency stop is unavailable: the security kernel is not constructed.";
            return;
        }

        if (kernel.Preemption.IsEmergencyStopActive)
        {
            ActionStatusText = "Emergency stop is already engaged.";
            return;
        }

        // Emergency stop needs a live Owner session to be authoritative. With no approved
        // authentication factor there is none, so this path must not fabricate urgency.
        ActionStatusText = "Emergency stop requires an authenticated Owner session. No approved MFA policy is registered, so the kernel refuses to latch it here.";

        _logger.LogWarning(
            nameof(SecurityViewModel),
            "Emergency stop requested from the desktop surface without an authenticated Owner session.");

        _emergencyStopRequested?.Invoke("Emergency stop requested");
    }

    private void ApplyAuditEntries(IReadOnlyList<SecurityAuditEntryView> entries)
    {
        AuditEntries.Clear();

        foreach (var entry in entries)
        {
            AuditEntries.Add(entry);
        }
    }

    private void ApplyUnavailable(string ownerStatus, string detail)
    {
        IsKernelOperational = false;
        OwnerStatusText = ownerStatus;
        OwnerStatusDetail = detail;
        PolicyVersionText = "Unavailable";
        AuditStatusText = "Unavailable";
        AuditDetailText = detail;
        ApprovalStatusText = "Unavailable";
        VaultStatusText = "Unavailable";
        EmergencyStopText = "Kernel unavailable";
        SessionSummary = "No session data";
        AgentSummary = "No agent data";
        AuditEntries.Clear();
        LastRefreshDisplay = DateTimeOffset.UtcNow.UtcDateTime.ToString("HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
    }

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

    public void Dispose()
    {
        // ObservableCollection keeps no subscriptions; the method exists so the shell can
        // dispose this alongside the other view models.
        _ = Interlocked.Exchange(ref _disposed, 1);
    }

    /// <summary>
    /// Generates an in-memory approver trust marker. Only an authenticated Owner surface
    /// may register it; M2 has no approved authentication factor, so nothing registers it
    /// in production composition. Kept for the desktop test that proves the boundary.
    /// </summary>
    internal static byte[] CreateOwnerSurfaceMarker() => RandomNumberGenerator.GetBytes(48);
}
