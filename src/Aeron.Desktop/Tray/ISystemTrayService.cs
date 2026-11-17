namespace Aeron.Desktop.Tray;

/// <summary>
/// Abstraction for AERON's native Windows system tray notification icon.
/// Keeps shell state logic headless-testable while interactive icon rendering
/// is verified empirically on APEX (Windows 11 25H2+).
/// </summary>
public interface ISystemTrayService : IDisposable
{
    bool IsIconRegistered { get; }

    event EventHandler? OpenRequested;
    event EventHandler? MinimizeToTrayRequested;
    event EventHandler? ExitRequested;

    bool Initialize(string tooltipText);
    void UpdateTooltip(string tooltipText);
}
