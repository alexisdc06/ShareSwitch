using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ShareSwitch.Hooks;

public sealed class MouseButtonEventArgs : EventArgs
{
    public Point Location { get; init; }
    /// <summary>Set to true to swallow the click (the matching button-up is swallowed automatically).</summary>
    public bool Handled { get; set; }
}

/// <summary>
/// Global low-level mouse hook (WH_MOUSE_LL).
///
/// Only installed while selection mode is active to keep system-wide overhead
/// at zero the rest of the time. Same threading rules as <see cref="KeyboardHook"/>:
/// install from the UI thread, keep handlers fast.
/// </summary>
public sealed class MouseHook : IDisposable
{
    private readonly HookProc _proc; // field keeps the delegate alive (GC root)
    private IntPtr _hook = IntPtr.Zero;
    private bool _swallowNextLeftUp;

    /// <summary>Raised on every mouse move with the screen position.</summary>
    public event Action<Point>? MouseMoved;

    /// <summary>Raised on left button down. Set Handled to swallow the click.</summary>
    public event EventHandler<MouseButtonEventArgs>? LeftButtonDown;

    public MouseHook() => _proc = Callback;

    public bool IsInstalled => _hook != IntPtr.Zero;

    /// <exception cref="Win32Exception">The hook could not be installed.</exception>
    public void Install()
    {
        if (IsInstalled) return;
        _swallowNextLeftUp = false;
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install global mouse hook.");
    }

    public void Uninstall()
    {
        if (!IsInstalled) return;
        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
        _swallowNextLeftUp = false;
    }

    public void Dispose() => Uninstall();

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                int msg = wParam.ToInt32();

                // Mouse moves arrive in floods. Skip all work (including the
                // struct marshal) when nobody is listening — keeps the hook
                // callback fast enough that Windows never drops it.
                if (msg == WM_MOUSEMOVE && MouseMoved is null)
                    return CallNextHookEx(_hook, nCode, wParam, lParam);

                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var point = new Point(data.pt.X, data.pt.Y);

                switch (msg)
                {
                    case WM_MOUSEMOVE:
                        MouseMoved?.Invoke(point);
                        break;

                    case WM_LBUTTONDOWN:
                        var args = new MouseButtonEventArgs { Location = point };
                        LeftButtonDown?.Invoke(this, args);
                        if (args.Handled)
                        {
                            _swallowNextLeftUp = true; // keep down/up pair consistent
                            return (IntPtr)1;
                        }
                        break;

                    case WM_LBUTTONUP when _swallowNextLeftUp:
                        _swallowNextLeftUp = false;
                        return (IntPtr)1;
                }
            }
            catch (Exception ex)
            {
                // Never let an exception escape the hook chain — but leave a trace.
                Diagnostics.LogError("mouse hook", ex);
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    #region P/Invoke

    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLPOINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public MSLLPOINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    #endregion
}
