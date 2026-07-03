using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;

namespace ReadableHttp.App.Maui;

public sealed class WindowsTrayIconService : IDisposable
{
    private const uint TrayIconId = 1;
    private const uint TrayMessage = 0x8000 + 42;
    private const uint NimAdd = 0x00000000;
    private const uint NimModify = 0x00000001;
    private const uint NimDelete = 0x00000002;
    private const uint NimSetVersion = 0x00000004;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint NotifyIconVersion4 = 4;
    private const uint NinSelect = 0x0400;
    private const uint NinKeySelect = 0x0401;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmContextMenu = 0x007B;
    private const uint WmNull = 0x0000;
    private const uint MfString = 0x00000000;
    private const uint MfSeparator = 0x00000800;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCmd = 0x0100;
    private const int SwHide = 0;
    private const int SwRestore = 9;
    private const int ImageIcon = 1;
    private const int LrLoadFromFile = 0x00000010;
    private const int LrDefaultSize = 0x00000040;
    private const int IdiApplication = 32512;

    private readonly AppCommandService _commands;
    private Microsoft.UI.Xaml.Window? _nativeWindow;
    private Window? _window;
    private AppWindow? _appWindow;
    private IntPtr _hwnd;
    private IntPtr _icon;
    private bool _ownsIcon;
    private bool _isIconAdded;
    private bool _isExitRequested;
    private SubclassProc? _subclassProc;

    public WindowsTrayIconService(AppCommandService commands)
    {
        _commands = commands;
    }

    public void Initialize(Window window)
    {
        if (_isIconAdded || window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativeWindow)
        {
            return;
        }

        _window = window;
        _nativeWindow = nativeWindow;
        _hwnd = WindowNative.GetWindowHandle(nativeWindow);
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        _subclassProc = WindowSubclassProc;
        SetWindowSubclass(_hwnd, _subclassProc, 1, UIntPtr.Zero);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow.Closing += HandleAppWindowClosing;
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeChanged += HandleRequestedThemeChanged;
        }

        ApplyNativeTitleBarTheme();
        ApplyWindowIcon();
        AddTrayIcon();
    }

    public void Dispose()
    {
        RemoveTrayIcon();
        if (_appWindow is not null)
        {
            _appWindow.Closing -= HandleAppWindowClosing;
            _appWindow = null;
        }

        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeChanged -= HandleRequestedThemeChanged;
        }

        if (_hwnd != IntPtr.Zero)
        {
            RemoveWindowSubclass(_hwnd, _subclassProc, 1);
            _hwnd = IntPtr.Zero;
        }

        if (_ownsIcon && _icon != IntPtr.Zero)
        {
            DestroyIcon(_icon);
            _icon = IntPtr.Zero;
        }
    }

    private IntPtr WindowSubclassProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam, UIntPtr subclassId, UIntPtr refData)
    {
        if (message == TrayMessage)
        {
            var trayEvent = GetTrayEvent(lParam);
            if (trayEvent is WmLButtonUp or NinSelect or NinKeySelect)
            {
                ShowAndFocusWindow();
                return IntPtr.Zero;
            }

            if (trayEvent is WmRButtonUp or WmContextMenu)
            {
                ShowContextMenu();
                return IntPtr.Zero;
            }
        }

        return DefSubclassProc(hWnd, message, wParam, lParam);
    }

    private void AddTrayIcon()
    {
        _icon = LoadApplicationIcon();
        var data = CreateNotifyIconData(NifMessage | NifIcon | NifTip);
        data.hIcon = _icon;
        data.szTip = "ReadableHttp";
        data.uCallbackMessage = TrayMessage;

        if (Shell_NotifyIcon(NimAdd, ref data))
        {
            data.uVersion = NotifyIconVersion4;
            Shell_NotifyIcon(NimSetVersion, ref data);
            _isIconAdded = true;
        }
    }

    private void RemoveTrayIcon()
    {
        if (!_isIconAdded)
        {
            return;
        }

        var data = CreateNotifyIconData(0);
        Shell_NotifyIcon(NimDelete, ref data);
        _isIconAdded = false;
    }

    private void ShowContextMenu()
    {
        if (_hwnd == IntPtr.Zero || !GetCursorPos(out var point))
        {
            return;
        }

        SetForegroundWindow(_hwnd);
        var menu = CreatePopupMenu();
        try
        {
            AppendMenu(menu, MfString, 100, "显示窗口");
            AppendMenu(menu, MfString, 101, "新建请求");
            AppendMenu(menu, MfString, 102, "粘贴导入");
            AppendMenu(menu, MfString, 103, "重新加载 Workspace");
            AppendMenu(menu, MfString, 104, "设置");
            AppendMenu(menu, MfSeparator, 0, null);
            AppendMenu(menu, MfString, 199, "退出");

            var command = TrackPopupMenu(menu, TpmRightButton | TpmReturnCmd, point.X, point.Y, 0, _hwnd, IntPtr.Zero);
            HandleMenuCommand(command);
            PostMessage(_hwnd, WmNull, IntPtr.Zero, IntPtr.Zero);
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private void HandleMenuCommand(uint command)
    {
        switch (command)
        {
            case 100:
                ShowAndFocusWindow();
                break;
            case 101:
                ShowAndFocusWindow();
                _ = _commands.RequestAsync(AppCommand.NewRequest);
                break;
            case 102:
                ShowAndFocusWindow();
                _ = _commands.RequestAsync(AppCommand.PasteImport);
                break;
            case 103:
                ShowAndFocusWindow();
                _ = _commands.RequestAsync(AppCommand.ReloadWorkspace);
                break;
            case 104:
                ShowAndFocusWindow();
                _ = _commands.RequestAsync(AppCommand.OpenSettings);
                break;
            case 199:
                _isExitRequested = true;
                RemoveTrayIcon();
                Application.Current?.Quit();
                break;
        }
    }

    private void HandleAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExitRequested)
        {
            return;
        }

        args.Cancel = true;
        HideWindow();
    }

    private void HandleRequestedThemeChanged(object? sender, AppThemeChangedEventArgs args)
    {
        ApplyNativeTitleBarTheme();
    }

    private void HideWindow()
    {
        if (_hwnd != IntPtr.Zero)
        {
            ShowWindow(_hwnd, SwHide);
        }
    }

    private void ShowAndFocusWindow()
    {
        if (_hwnd != IntPtr.Zero)
        {
            ShowWindow(_hwnd, SwRestore);
            SetForegroundWindow(_hwnd);
        }

        _nativeWindow?.Activate();
    }

    private void ApplyWindowIcon()
    {
        var iconPath = FindIconPath();
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return;
        }

        try
        {
            var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
            AppWindow.GetFromWindowId(windowId).SetIcon(iconPath);
        }
        catch (COMException)
        {
        }
    }

    private void ApplyNativeTitleBarTheme()
    {
        if (_appWindow?.TitleBar is not { } titleBar)
        {
            return;
        }

        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var background = isDark
            ? Windows.UI.Color.FromArgb(255, 17, 24, 39)
            : Windows.UI.Color.FromArgb(255, 248, 250, 252);
        var foreground = isDark
            ? Windows.UI.Color.FromArgb(255, 229, 231, 235)
            : Windows.UI.Color.FromArgb(255, 15, 23, 42);
        var hoverBackground = isDark
            ? Windows.UI.Color.FromArgb(255, 31, 37, 49)
            : Windows.UI.Color.FromArgb(255, 245, 247, 251);

        titleBar.BackgroundColor = background;
        titleBar.ForegroundColor = foreground;
        titleBar.ButtonBackgroundColor = background;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonHoverBackgroundColor = hoverBackground;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedBackgroundColor = hoverBackground;
        titleBar.ButtonPressedForegroundColor = foreground;
        titleBar.InactiveBackgroundColor = background;
        titleBar.InactiveForegroundColor = foreground;
        titleBar.ButtonInactiveBackgroundColor = background;
        titleBar.ButtonInactiveForegroundColor = foreground;
    }

    private IntPtr LoadApplicationIcon()
    {
        var iconPath = FindIconPath();
        if (!string.IsNullOrWhiteSpace(iconPath))
        {
            var icon = LoadImage(IntPtr.Zero, iconPath, ImageIcon, 0, 0, LrLoadFromFile | LrDefaultSize);
            if (icon != IntPtr.Zero)
            {
                _ownsIcon = true;
                return icon;
            }
        }

        return LoadIcon(IntPtr.Zero, new IntPtr(IdiApplication));
    }

    private static string? FindIconPath()
    {
        var baseDirectory = AppContext.BaseDirectory;
        return Directory.EnumerateFiles(baseDirectory, "appicon*.ico", SearchOption.AllDirectories)
            .OrderBy(path => path.Length)
            .FirstOrDefault();
    }

    private NotifyIconData CreateNotifyIconData(uint flags)
    {
        return new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _hwnd,
            uID = TrayIconId,
            uFlags = flags
        };
    }

    private static uint GetTrayEvent(IntPtr lParam)
    {
        return unchecked((uint)lParam.ToInt64()) & 0xFFFF;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    private delegate IntPtr SubclassProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam, UIntPtr subclassId, UIntPtr refData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc? subclassProc, UIntPtr subclassId, UIntPtr refData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc? subclassProc, UIntPtr subclassId);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr instance, string name, int type, int cx, int cy, int load);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr LoadIcon(IntPtr instance, IntPtr iconName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int commandShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, uint idNewItem, string? newItem);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hWnd, IntPtr rect);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr menu);
}
