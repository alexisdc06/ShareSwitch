using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace ShareSwitch.Interop;

/// <summary>
/// Shared user32 / dwmapi P/Invoke declarations used across the app.
/// Hook-specific and thumbnail-specific imports live next to their owners
/// (KeyboardHook / MouseHook / DwmThumbnail) to keep responsibilities clear.
/// NOTE: the app targets win-x64 only, so the *LongPtr entry points are safe.
/// </summary>
internal static class NativeMethods
{
    #region Constants

    public const int GA_ROOT = 2;

    public const int WM_NCHITTEST = 0x0084;
    public const int HTTRANSPARENT = -1;

    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int GWL_EXSTYLE = -20;

    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    public const int DWMWA_CLOAKED = 14;

    // Dark title bar for app dialogs (Windows 10 20H1+ / 11).
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    // Windows 11 (build 22000+) window chrome tinting.
    public const int DWMWA_BORDER_COLOR = 34;
    public const int DWMWA_CAPTION_COLOR = 35;
    public const int DWMWA_TEXT_COLOR = 36;
    public const uint DWMWA_COLOR_DEFAULT = 0xFFFFFFFF;

    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    // Virtual keys (generic modifiers, used with GetAsyncKeyState)
    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12; // Alt

    #endregion

    #region Structs

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public readonly Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
        public POINT(Point p) { X = p.X; Y = p.Y; }
    }

    #endregion

    #region Imports

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT pt);

    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder buffer, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder buffer, int maxCount);

    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint flags);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute,
        out int value, int size);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute,
        out RECT value, int size);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hWnd, int attribute,
        ref uint value, int size);

    #endregion

    #region Helpers

    /// <summary>Window title, or empty string.</summary>
    public static string GetWindowTitle(IntPtr hWnd)
    {
        int len = GetWindowTextLength(hWnd);
        if (len == 0) return string.Empty;
        var sb = new StringBuilder(len + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>Window class name, or empty string.</summary>
    public static string GetWindowClassName(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>True if the window is DWM-cloaked (e.g. inactive UWP/virtual-desktop windows).</summary>
    public static bool IsCloaked(IntPtr hWnd)
    {
        int hr = DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int));
        return hr == 0 && cloaked != 0;
    }

    /// <summary>
    /// Visible bounds of a window in screen pixels. Prefers DWM extended frame
    /// bounds (excludes the invisible resize borders Win10/11 add), falls back
    /// to GetWindowRect.
    /// </summary>
    public static Rectangle GetVisibleWindowBounds(IntPtr hWnd)
    {
        int hr = DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS,
            out RECT rect, Marshal.SizeOf<RECT>());
        if (hr != 0)
            GetWindowRect(hWnd, out rect);
        return rect.ToRectangle();
    }

    public static bool HasExStyle(IntPtr hWnd, int exStyle)
        => ((long)GetWindowLongPtr(hWnd, GWL_EXSTYLE) & exStyle) != 0;

    public static bool IsKeyDown(int vKey) => (GetAsyncKeyState(vKey) & 0x8000) != 0;

    /// <summary>Windows 11 introduced the chrome-tinting DWM attributes.</summary>
    public static bool SupportsWindowChromeTinting
        => Environment.OSVersion.Version.Build >= 22000;

    /// <summary>
    /// Tints the window's border, title bar and caption text. Non-client area
    /// only, so it is <b>not</b> part of what a screen-share captures.
    /// Pass null to restore the system default.
    /// Returns false on Windows 10 and older, where the attributes don't exist.
    /// </summary>
    public static bool TrySetWindowChrome(IntPtr hWnd, Color? border, Color? caption, Color? text)
    {
        if (!SupportsWindowChromeTinting || hWnd == IntPtr.Zero) return false;

        bool ok = true;
        ok &= Apply(hWnd, DWMWA_BORDER_COLOR, border);
        ok &= Apply(hWnd, DWMWA_CAPTION_COLOR, caption);
        ok &= Apply(hWnd, DWMWA_TEXT_COLOR, text);
        return ok;

        static bool Apply(IntPtr hWnd, int attribute, Color? color)
        {
            // DWM expects 0x00BBGGRR, not ARGB.
            uint value = color is { } c
                ? (uint)(c.R | (c.G << 8) | (c.B << 16))
                : DWMWA_COLOR_DEFAULT;
            return DwmSetWindowAttribute(hWnd, attribute, ref value, sizeof(uint)) == 0;
        }
    }

    #endregion
}
