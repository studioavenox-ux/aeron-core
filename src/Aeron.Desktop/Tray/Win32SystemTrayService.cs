using System.Runtime.InteropServices;
using System.Windows.Interop;
using Aeron.Contracts;

namespace Aeron.Desktop.Tray;

internal static class NativeTrayMethods
{
    public const uint NIM_ADD = 0x00000000;
    public const uint NIM_MODIFY = 0x00000001;
    public const uint NIM_DELETE = 0x00000002;

    public const uint NIF_MESSAGE = 0x00000001;
    public const uint NIF_ICON = 0x00000002;
    public const uint NIF_TIP = 0x00000004;

    public const int WM_NULL = 0x0000;
    public const int WM_CONTEXTMENU = 0x007B;
    public const int WM_LBUTTONDBLCLK = 0x0203;
    public const int WM_RBUTTONUP = 0x0205;
    public const int WM_TRAYCALLBACK = 0x8000 + 42; // WM_APP + 42

    public const int WS_POPUP = unchecked((int)0x80000000);
    public const int WS_EX_TOOLWINDOW = 0x00000080;

    public const uint MF_STRING = 0x00000000;
    public const uint MF_SEPARATOR = 0x00000800;

    public const uint TPM_RIGHTBUTTON = 0x0002;
    public const uint TPM_NONOTIFY = 0x0080;
    public const uint TPM_RETURNCMD = 0x0100;
    public const uint TPM_BOTTOMALIGN = 0x0020;

    public const uint BI_BITFIELDS = 3;
    public const uint DIB_RGB_COLORS = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public uint uTimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ICONINFO
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool fIcon;

        public uint xHotspot;
        public uint yHotspot;
        public nint hbmMask;
        public nint hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPV5HEADER
    {
        public uint bV5Size;
        public int bV5Width;
        public int bV5Height;
        public ushort bV5Planes;
        public ushort bV5BitCount;
        public uint bV5Compression;
        public uint bV5SizeImage;
        public int bV5XPelsPerMeter;
        public int bV5YPelsPerMeter;
        public uint bV5ClrUsed;
        public uint bV5ClrImportant;
        public uint bV5RedMask;
        public uint bV5GreenMask;
        public uint bV5BlueMask;
        public uint bV5AlphaMask;
        public uint bV5CSType;

        // CIEXYZTRIPLE (3 * 3 * 4 = 36 bytes)
        public int ciexyzRedX;
        public int ciexyzRedY;
        public int ciexyzRedZ;
        public int ciexyzGreenX;
        public int ciexyzGreenY;
        public int ciexyzGreenZ;
        public int ciexyzBlueX;
        public int ciexyzBlueY;
        public int ciexyzBlueZ;

        public uint bV5GammaRed;
        public uint bV5GammaGreen;
        public uint bV5GammaBlue;
        public uint bV5Intent;
        public uint bV5ProfileData;
        public uint bV5ProfileSize;
        public uint bV5Reserved;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    public static extern uint RegisterWindowMessageW(string lpString);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    public static extern nint CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AppendMenuW(nint hMenu, uint uFlags, nuint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetMenuDefaultItem(nint hMenu, uint uItem, uint fByPos);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyMenu(nint hMenu);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    public static extern uint TrackPopupMenuEx(
        nint hMenu,
        uint fuFlags,
        int x,
        int y,
        nint hWnd,
        nint lptpm);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessageW(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    public static extern nint CreateIconIndirect(ref ICONINFO piconinfo);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(nint hIcon);

    [DllImport("user32.dll", ExactSpelling = true)]
    public static extern nint GetDC(nint hWnd);

    [DllImport("user32.dll", ExactSpelling = true)]
    public static extern int ReleaseDC(nint hWnd, nint hDC);

    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
    public static extern nint CreateDIBSection(
        nint hdc,
        ref BITMAPV5HEADER pbmi,
        uint usage,
        out nint ppvBits,
        nint hSection,
        uint offset);

    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
    public static extern nint CreateBitmap(
        int nWidth,
        int nHeight,
        uint nPlanes,
        uint nBitCount,
        byte[] lpBits);

    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(nint ho);
}

/// <summary>
/// Pure Win32 Shell_NotifyIconW system tray host for Windows 11.
/// Supports double-click restoration, native context menu (Open AERON / Minimize to Tray / Exit AERON),
/// automatic re-registration on Explorer restarts, and idempotent icon removal on shutdown.
/// </summary>
public sealed class Win32SystemTrayService : ISystemTrayService
{
    private const uint TrayIconId = 1;
    private const uint CommandOpen = 1001;
    private const uint CommandMinimizeToTray = 1002;
    private const uint CommandExit = 1003;

    private readonly IAeronLogger? _logger;
    private readonly uint _taskbarCreatedMessage;

    private HwndSource? _messageWindow;
    private nint _iconHandle;
    private string _tooltip = "AERON";
    private bool _iconRegistered;
    private int _disposed;

    public Win32SystemTrayService(IAeronLogger? logger = null)
    {
        _logger = logger;
        _taskbarCreatedMessage = NativeTrayMethods.RegisterWindowMessageW("TaskbarCreated");
    }

    public bool IsIconRegistered => Volatile.Read(ref _iconRegistered);

    public event EventHandler? OpenRequested;
    public event EventHandler? MinimizeToTrayRequested;
    public event EventHandler? ExitRequested;

    public bool Initialize(string tooltipText)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return false;
        }

        _tooltip = TruncateTooltip(tooltipText);

        if (_messageWindow is null)
        {
            var parameters = new HwndSourceParameters("AeronTrayMessageHost")
            {
                Width = 0,
                Height = 0,
                PositionX = 0,
                PositionY = 0,
                WindowStyle = NativeTrayMethods.WS_POPUP,
                ExtendedWindowStyle = NativeTrayMethods.WS_EX_TOOLWINDOW
            };

            _messageWindow = new HwndSource(parameters);
            _messageWindow.AddHook(WndProc);
        }

        if (_iconHandle == nint.Zero)
        {
            _iconHandle = BrandIconFactory.CreateTrayHIcon();
        }

        return RegisterOrRefreshIcon(isModify: false);
    }

    public void UpdateTooltip(string tooltipText)
    {
        if (Volatile.Read(ref _disposed) != 0 || !_iconRegistered || _messageWindow is null)
        {
            return;
        }

        _tooltip = TruncateTooltip(tooltipText);
        _ = RegisterOrRefreshIcon(isModify: true);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        RemoveIconSafe();

        if (_iconHandle != nint.Zero)
        {
            _ = NativeTrayMethods.DestroyIcon(_iconHandle);
            _iconHandle = nint.Zero;
        }

        if (_messageWindow is not null)
        {
            _messageWindow.RemoveHook(WndProc);
            _messageWindow.Dispose();
            _messageWindow = null;
        }
    }

    private bool RegisterOrRefreshIcon(bool isModify)
    {
        if (_messageWindow is null || _messageWindow.Handle == nint.Zero)
        {
            return false;
        }

        var data = CreateNotifyIconData(_messageWindow.Handle);
        var command = isModify ? NativeTrayMethods.NIM_MODIFY : NativeTrayMethods.NIM_ADD;
        var succeeded = NativeTrayMethods.Shell_NotifyIconW(command, ref data);

        if (!isModify)
        {
            Volatile.Write(ref _iconRegistered, succeeded);
            if (succeeded)
            {
                _logger?.LogInfo(nameof(Win32SystemTrayService), "Registered AERON system tray notification icon.");
            }
            else
            {
                _logger?.LogWarning(
                    nameof(Win32SystemTrayService),
                    "System tray notification area was unavailable during icon registration.");
            }
        }

        return succeeded;
    }

    private void RemoveIconSafe()
    {
        if (!Volatile.Read(ref _iconRegistered) || _messageWindow is null || _messageWindow.Handle == nint.Zero)
        {
            Volatile.Write(ref _iconRegistered, false);
            return;
        }

        var data = new NativeTrayMethods.NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NativeTrayMethods.NOTIFYICONDATAW>(),
            hWnd = _messageWindow.Handle,
            uID = TrayIconId,
            szTip = string.Empty,
            szInfo = string.Empty,
            szInfoTitle = string.Empty
        };

        _ = NativeTrayMethods.Shell_NotifyIconW(NativeTrayMethods.NIM_DELETE, ref data);
        Volatile.Write(ref _iconRegistered, false);
        _logger?.LogInfo(nameof(Win32SystemTrayService), "Removed AERON system tray notification icon.");
    }

    private NativeTrayMethods.NOTIFYICONDATAW CreateNotifyIconData(nint hwnd) =>
        new()
        {
            cbSize = (uint)Marshal.SizeOf<NativeTrayMethods.NOTIFYICONDATAW>(),
            hWnd = hwnd,
            uID = TrayIconId,
            uFlags = NativeTrayMethods.NIF_MESSAGE | NativeTrayMethods.NIF_ICON | NativeTrayMethods.NIF_TIP,
            uCallbackMessage = NativeTrayMethods.WM_TRAYCALLBACK,
            hIcon = _iconHandle,
            szTip = _tooltip,
            szInfo = string.Empty,
            szInfoTitle = string.Empty
        };

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeTrayMethods.WM_TRAYCALLBACK)
        {
            var mouseEvent = unchecked((int)(lParam.ToInt64() & 0xFFFF));
            switch (mouseEvent)
            {
                case NativeTrayMethods.WM_LBUTTONDBLCLK:
                    handled = true;
                    OpenRequested?.Invoke(this, EventArgs.Empty);
                    break;

                case NativeTrayMethods.WM_RBUTTONUP:
                case NativeTrayMethods.WM_CONTEXTMENU:
                    handled = true;
                    ShowContextMenu(hwnd);
                    break;
            }
        }
        else if (_taskbarCreatedMessage != 0 && (uint)msg == _taskbarCreatedMessage && Volatile.Read(ref _disposed) == 0)
        {
            // Windows Explorer restarted; re-register notification icon cleanly.
            _ = RegisterOrRefreshIcon(isModify: false);
        }

        return nint.Zero;
    }

    private void ShowContextMenu(nint hwnd)
    {
        var menu = NativeTrayMethods.CreatePopupMenu();
        if (menu == nint.Zero)
        {
            return;
        }

        try
        {
            _ = NativeTrayMethods.AppendMenuW(menu, NativeTrayMethods.MF_STRING, CommandOpen, "Open AERON");
            _ = NativeTrayMethods.AppendMenuW(menu, NativeTrayMethods.MF_STRING, CommandMinimizeToTray, "Minimize to Tray");
            _ = NativeTrayMethods.AppendMenuW(menu, NativeTrayMethods.MF_SEPARATOR, 0, null);
            _ = NativeTrayMethods.AppendMenuW(menu, NativeTrayMethods.MF_STRING, CommandExit, "Exit AERON");
            _ = NativeTrayMethods.SetMenuDefaultItem(menu, CommandOpen, fByPos: 0);

            if (!NativeTrayMethods.GetCursorPos(out var cursor))
            {
                return;
            }

            // Standard Win32 shell sequence so clicking outside dismisses the tray menu cleanly.
            _ = NativeTrayMethods.SetForegroundWindow(hwnd);
            var selected = NativeTrayMethods.TrackPopupMenuEx(
                menu,
                NativeTrayMethods.TPM_RIGHTBUTTON |
                NativeTrayMethods.TPM_BOTTOMALIGN |
                NativeTrayMethods.TPM_NONOTIFY |
                NativeTrayMethods.TPM_RETURNCMD,
                cursor.X,
                cursor.Y,
                hwnd,
                nint.Zero);
            _ = NativeTrayMethods.PostMessageW(hwnd, NativeTrayMethods.WM_NULL, nint.Zero, nint.Zero);

            switch (selected)
            {
                case CommandOpen:
                    OpenRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case CommandMinimizeToTray:
                    MinimizeToTrayRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case CommandExit:
                    ExitRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
        finally
        {
            _ = NativeTrayMethods.DestroyMenu(menu);
        }
    }

    private static string TruncateTooltip(string? tooltip)
    {
        if (string.IsNullOrWhiteSpace(tooltip))
        {
            return "AERON";
        }

        const int MaxTooltipChars = 120;
        var clean = tooltip.Trim();
        return clean.Length <= MaxTooltipChars ? clean : clean[..MaxTooltipChars];
    }
}
