using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FrameBoost.Models;

namespace FrameBoost.Services;

internal sealed class WindowResolutionService
{
    private static readonly Dictionary<nint, WindowStyleSnapshot> StyleCache = [];
    private static readonly HashSet<string> DashboardProcessDenyList =
    [
        "System",
        "Idle",
        "Registry",
        "fontdrvhost",
        "csrss",
        "dwm",
        "memory compression",
        "services",
        "sihost",
        "smss",
        "spoolsv",
        "svchost",
        "wininit",
        "winlogon",
        "wudfhost"
    ];

    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;

    private const long WsCaption = 0x00C00000L;
    private const long WsThickFrame = 0x00040000L;
    private const long WsMinimizeBox = 0x00020000L;
    private const long WsMaximizeBox = 0x00010000L;
    private const long WsSysMenu = 0x00080000L;
    private const long WsPopup = unchecked((long)0x80000000);
    private const long WsVisible = 0x10000000L;

    private const long WsExDlgModalFrame = 0x00000001L;
    private const long WsExClientEdge = 0x00000200L;
    private const long WsExStaticEdge = 0x00020000L;
    private const long WsExWindowEdge = 0x00000100L;

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;

    private const int SwRestore = 9;

    public IReadOnlyList<RunningProcessEntry> ListWindowedProcesses()
    {
        var processes = new List<RunningProcessEntry>();
        var windowsByProcess = EnumerateVisibleWindows()
            .GroupBy(static window => window.ProcessId)
            .ToList();

        foreach (var group in windowsByProcess)
        {
            try
            {
                using var process = Process.GetProcessById(group.Key);
                if (process.HasExited)
                {
                    continue;
                }

                var representativeWindow = group
                    .OrderByDescending(static window => window.WindowTitle.Length)
                    .First();

                processes.Add(new RunningProcessEntry
                {
                    ProcessId = process.Id,
                    ProcessName = process.ProcessName,
                    WindowTitle = representativeWindow.WindowTitle,
                    ExecutablePath = TryGetExecutablePath(process) ?? string.Empty,
                    HasVisibleWindow = true
                });
            }
            catch
            {
            }
        }

        return processes
            .OrderBy(static entry => entry.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.WindowTitle, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<RunningProcessEntry> ListDashboardProcesses()
    {
        var visibleWindows = EnumerateVisibleWindows()
            .GroupBy(static window => window.ProcessId)
            .ToDictionary(
                static group => group.Key,
                static group => group.OrderByDescending(static window => window.WindowTitle.Length).First(),
                EqualityComparer<int>.Default);

        var currentSessionId = Process.GetCurrentProcess().SessionId;
        var results = new Dictionary<int, RunningProcessEntry>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                using (process)
                {
                    if (process.HasExited || process.Id == Environment.ProcessId)
                    {
                        continue;
                    }

                    if (process.SessionId != currentSessionId)
                    {
                        continue;
                    }

                    var processName = process.ProcessName;
                    if (string.IsNullOrWhiteSpace(processName) || DashboardProcessDenyList.Contains(processName))
                    {
                        continue;
                    }

                    var executablePath = TryGetExecutablePath(process);
                    visibleWindows.TryGetValue(process.Id, out var visibleWindow);
                    var visibleTitle = visibleWindow?.WindowTitle ?? string.Empty;
                    var mainWindowTitle = string.IsNullOrWhiteSpace(visibleTitle)
                        ? process.MainWindowTitle?.Trim() ?? string.Empty
                        : visibleTitle;

                    results[process.Id] = new RunningProcessEntry
                    {
                        ProcessId = process.Id,
                        ProcessName = processName,
                        WindowTitle = mainWindowTitle,
                        ExecutablePath = executablePath ?? string.Empty,
                        HasVisibleWindow = !string.IsNullOrWhiteSpace(mainWindowTitle)
                    };
                }
            }
            catch
            {
            }
        }

        return results.Values
            .OrderByDescending(static entry => entry.HasVisibleWindow)
            .ThenBy(static entry => entry.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.WindowTitle, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<WindowTargetEntry> ListWindowsForProcess(int processId)
    {
        return EnumerateVisibleWindows()
            .Where(window => window.ProcessId == processId)
            .OrderBy(static window => window.WindowTitle, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool TryGetWindowBounds(nint hwnd, out Rectangle bounds, out string? error)
    {
        bounds = Rectangle.Empty;
        error = null;

        if (!IsWindow(hwnd))
        {
            error = "The selected window is no longer available.";
            return false;
        }

        if (!GetWindowRect(hwnd, out var rect))
        {
            error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return false;
        }

        bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        return true;
    }

    public bool TryApplyWindowLayout(nint hwnd, int width, int height, bool borderless, out string? error)
    {
        error = null;

        if (width <= 0 || height <= 0)
        {
            error = "Choose a valid width and height before applying.";
            return false;
        }

        if (!IsWindow(hwnd))
        {
            error = "The selected window is no longer available.";
            return false;
        }

        if (!TrySetBorderStyle(hwnd, borderless, out error))
        {
            return false;
        }

        ShowWindow(hwnd, SwRestore);

        var left = 0;
        var top = 0;
        if (GetWindowRect(hwnd, out var rect))
        {
            left = rect.Left;
            top = rect.Top;
        }

        if (!SetWindowPos(hwnd, IntPtr.Zero, left, top, width, height, SwpNoZOrder | SwpNoActivate | SwpFrameChanged | SwpShowWindow))
        {
            if (!MoveWindow(hwnd, left, top, width, height, true))
            {
                error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                return false;
            }
        }

        return true;
    }

    public bool TryRestoreWindowBorder(nint hwnd, out string? error)
    {
        return TrySetBorderStyle(hwnd, borderless: false, out error);
    }

    public bool HasSavedBorderStyle(nint hwnd)
        => StyleCache.ContainsKey(hwnd);

    private bool TrySetBorderStyle(nint hwnd, bool borderless, out string? error)
    {
        error = null;

        if (!IsWindow(hwnd))
        {
            error = "The selected window is no longer available.";
            return false;
        }

        try
        {
            if (borderless)
            {
                var style = GetWindowLongPtr(hwnd, GwlStyle).ToInt64();
                var exStyle = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
                _ = GetWindowRect(hwnd, out var currentRect);

                if (!StyleCache.ContainsKey(hwnd))
                {
                    StyleCache[hwnd] = new WindowStyleSnapshot(
                        style,
                        exStyle,
                        currentRect.Left,
                        currentRect.Top,
                        Math.Max(0, currentRect.Right - currentRect.Left),
                        Math.Max(0, currentRect.Bottom - currentRect.Top));
                }

                var newStyle = style & ~(WsCaption | WsThickFrame | WsMinimizeBox | WsMaximizeBox | WsSysMenu);
                newStyle |= WsPopup | WsVisible;
                var newExStyle = exStyle & ~(WsExDlgModalFrame | WsExClientEdge | WsExStaticEdge | WsExWindowEdge);

                SetWindowLongPtr(hwnd, GwlStyle, new IntPtr(newStyle));
                SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(newExStyle));
            }
            else if (StyleCache.TryGetValue(hwnd, out var snapshot))
            {
                SetWindowLongPtr(hwnd, GwlStyle, new IntPtr(snapshot.Style));
                SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(snapshot.ExStyle));
                SetWindowPos(
                    hwnd,
                    IntPtr.Zero,
                    snapshot.Left,
                    snapshot.Top,
                    snapshot.Width,
                    snapshot.Height,
                    SwpNoZOrder | SwpNoActivate | SwpFrameChanged | SwpShowWindow);
                StyleCache.Remove(hwnd);
            }
            else
            {
                var style = GetWindowLongPtr(hwnd, GwlStyle).ToInt64();
                var exStyle = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();

                var restoredStyle = style & ~WsPopup;
                restoredStyle |= WsCaption | WsThickFrame | WsMinimizeBox | WsMaximizeBox | WsSysMenu | WsVisible;
                var restoredExStyle = exStyle | WsExWindowEdge | WsExClientEdge;

                SetWindowLongPtr(hwnd, GwlStyle, new IntPtr(restoredStyle));
                SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(restoredExStyle));
            }

            if (!SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoZOrder | SwpNoActivate | SwpFrameChanged | SwpShowWindow | SwpNoMove | SwpNoSize))
            {
                error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static IEnumerable<WindowTargetEntry> EnumerateVisibleWindows()
    {
        var windows = new List<WindowTargetEntry>();

        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || GetWindowTextLength(hwnd) <= 0)
            {
                return true;
            }

            var title = GetWindowTitle(hwnd);
            if (string.IsNullOrWhiteSpace(title))
            {
                return true;
            }

            GetWindowThreadProcessId(hwnd, out var processId);
            if (processId == 0)
            {
                return true;
            }

            try
            {
                using var process = Process.GetProcessById((int)processId);
                if (process.HasExited)
                {
                    return true;
                }

                windows.Add(new WindowTargetEntry
                {
                    Handle = hwnd,
                    ProcessId = process.Id,
                    ProcessName = process.ProcessName,
                    WindowTitle = title,
                    ExecutablePath = TryGetExecutablePath(process) ?? string.Empty
                });
            }
            catch
            {
            }

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static string GetWindowTitle(nint hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new StringBuilder(length + 1);
        _ = GetWindowText(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string? TryGetExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private readonly record struct WindowStyleSnapshot(long Style, long ExStyle, int Left, int Top, int Width, int Height);

    private delegate bool EnumWindowsProc(nint hwnd, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(nint hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(nint hWnd, out Rect lpRect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
