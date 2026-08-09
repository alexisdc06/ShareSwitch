using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ShareSwitch.Hooks;

public sealed class KeyboardHookEventArgs : EventArgs
{
    public Keys Key { get; init; }
    public bool IsDown { get; init; }
    /// <summary>Set to true to swallow the key event (it won't reach other apps).</summary>
    public bool Handled { get; set; }
}

/// <summary>
/// Global low-level keyboard hook (WH_KEYBOARD_LL).
///
/// Must be installed from a thread that pumps messages (the UI thread);
/// events are then raised on that same thread, so handlers may touch UI
/// directly — but must return quickly, or Windows will silently drop the hook.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private readonly HookProc _proc; // field keeps the delegate alive (GC root)
    private IntPtr _hook = IntPtr.Zero;

    public event EventHandler<KeyboardHookEventArgs>? KeyEvent;

    public KeyboardHook() => _proc = Callback;

    public bool IsInstalled => _hook != IntPtr.Zero;

    /// <exception cref="Win32Exception">The hook could not be installed.</exception>
    public void Install()
    {
        if (IsInstalled) return;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install global keyboard hook.");
    }

    public void Uninstall()
    {
        if (!IsInstalled) return;
        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    public void Dispose() => Uninstall();

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                int msg = wParam.ToInt32();
                bool down = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
                bool up = msg is WM_KEYUP or WM_SYSKEYUP;

                if (down || up)
                {
                    var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    var args = new KeyboardHookEventArgs { Key = (Keys)data.vkCode, IsDown = down };
                    KeyEvent?.Invoke(this, args);
                    if (args.Handled)
                        return (IntPtr)1;
                }
            }
            catch
            {
                // Never let an exception escape the hook chain.
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    #region P/Invoke

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
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
