using System.Drawing;
using System.Runtime.InteropServices;
using ShareSwitch.Interop;

namespace ShareSwitch;

/// <summary>
/// Enumerates and validates top-level windows that make sense as capture sources.
/// Filtering rules are shared between the Window Picker and hold-to-select mode
/// so both paths offer exactly the same set of windows.
/// </summary>
public static class WindowEnumerator
{
    public sealed record WindowInfo(IntPtr Hwnd, string Title, string ProcessName);

    /// <summary>Shell/system window classes that are never valid capture sources.</summary>
    private static readonly HashSet<string> ExcludedClasses = new(StringComparer.Ordinal)
    {
        "Progman",                  // desktop
        "WorkerW",                  // desktop (wallpaper host)
        "Shell_TrayWnd",            // taskbar
        "Shell_SecondaryTrayWnd",   // taskbar on other monitors
        "NotifyIconOverflowWindow", // tray overflow
        "TopLevelWindowForOverflowXamlIsland", // Win11 tray overflow
        "Windows.UI.Core.CoreWindow", // cloaked system UI hosts
    };

    /// <summary>All windows that can currently be offered as capture sources.</summary>
    public static List<WindowInfo> GetCaptureCandidates()
    {
        var list = new List<WindowInfo>();

        EnumWindows((hWnd, _) =>
        {
            if (IsCaptureCandidate(hWnd))
                list.Add(new WindowInfo(hWnd, NativeMethods.GetWindowTitle(hWnd), GetProcessName(hWnd)));
            return true; // continue enumeration
        }, IntPtr.Zero);

        return list;
    }

    /// <summary>
    /// True if <paramref name="hWnd"/> is a top-level window we allow as a source:
    /// visible, titled, not cloaked, not a tool window, not a shell window,
    /// and not owned by this process.
    /// </summary>
    public static bool IsCaptureCandidate(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd)) return false;
        if (!NativeMethods.IsWindowVisible(hWnd)) return false;
        if (NativeMethods.GetWindowTextLength(hWnd) == 0) return false;
        if (IsOwnWindow(hWnd)) return false;
        if (NativeMethods.HasExStyle(hWnd, NativeMethods.WS_EX_TOOLWINDOW)) return false;
        if (NativeMethods.IsCloaked(hWnd)) return false;
        if (ExcludedClasses.Contains(NativeMethods.GetWindowClassName(hWnd))) return false;
        return true;
    }

    /// <summary>
    /// Top-level window under the given screen point, or IntPtr.Zero.
    /// Note: windows of this process that report HTTRANSPARENT (ShareWindow in
    /// click-through mode, the highlight overlay) are skipped by WindowFromPoint.
    /// </summary>
    public static IntPtr GetTopLevelWindowAt(Point screenPoint)
    {
        IntPtr hWnd = NativeMethods.WindowFromPoint(new NativeMethods.POINT(screenPoint));
        if (hWnd == IntPtr.Zero) return IntPtr.Zero;
        return NativeMethods.GetAncestor(hWnd, NativeMethods.GA_ROOT);
    }

    public static bool IsOwnWindow(IntPtr hWnd)
    {
        NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
        return pid == (uint)Environment.ProcessId;
    }

    private static string GetProcessName(IntPtr hWnd)
    {
        try
        {
            NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
            using var p = System.Diagnostics.Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch
        {
            return string.Empty; // process exited or access denied
        }
    }

    #region P/Invoke

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    #endregion
}
