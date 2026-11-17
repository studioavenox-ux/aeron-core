using System.ComponentModel;
using System.Windows;
using Aeron.Desktop.Tray;
using Aeron.Desktop.ViewModels;

namespace Aeron.Desktop;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;

        InitializeComponent();

        DataContext = _viewModel;
        Icon = BrandIconFactory.CreateWindowIcon();

        _viewModel.HideToTrayRequested += OnHideToTrayRequested;
        _viewModel.RestoreWindowRequested += OnRestoreWindowRequested;
        Closed += OnWindowClosed;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        var action = _viewModel.HandleWindowClosing();
        if (action == WindowCloseAction.MinimizeToTray)
        {
            e.Cancel = true;
            return;
        }

        if (!_viewModel.IsExplicitExitInProgress)
        {
            // CloseToTray is disabled: cancel immediate raw close so lifecycle, SQLite,
            // logger, and system tray shut down deterministically before exiting.
            e.Cancel = true;
            _ = _viewModel.RequestExitAsync("Main window closed with CloseToTray disabled");
            return;
        }

        base.OnClosing(e);
    }

    public void RestoreToForeground()
    {
        ShowInTaskbar = true;

        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        if (!Activate())
        {
            Topmost = true;
            Topmost = false;
            _ = Activate();
        }

        _ = Focus();
    }

    public void HideToSystemTray()
    {
        ShowInTaskbar = false;
        Hide();
    }

    private void OnHideToTrayRequested(object? sender, EventArgs e) => HideToSystemTray();

    private void OnRestoreWindowRequested(object? sender, EventArgs e) => RestoreToForeground();

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _viewModel.HideToTrayRequested -= OnHideToTrayRequested;
        _viewModel.RestoreWindowRequested -= OnRestoreWindowRequested;
        Closed -= OnWindowClosed;
    }
}
