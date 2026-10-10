using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ShareSwitch.Interop;
using ShareSwitch.Selection;

namespace ShareSwitch.Hooks;

public sealed class KeyboardHookEventArgs : EventArgs
{
    public Keys Key { get; init; }
    public bool IsDown { get; init; }
    /// <summary>Hardware scan code — the key's physical position, independent of layout.</summary>
    public int ScanCode { get; init; }
    /// <summary>True for extended keys (e.g. the arrow cluster, as opposed to the numpad).</summary>
    public bool Extended { get; init; }
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
                    var key = (Keys)data.vkCode;
                    int scan = (int)data.scanCode;
                    bool extended = (data.flags & LLKHF_EXTENDED) != 0;

                    // PRIVACY BOUNDARY — do not weaken.
                    // A low-level keyboard hook technically observes every keystroke.
                    // We discard anything that is not a modifier or Escape *here*,
                    // before the value is passed anywhere else, so no other part of
                    // the program can ever see the keys you type. Nothing is stored,
                    // buffered or transmitted; the app makes no network calls at all.
                    if (IsRelevantKey(key, scan, extended))
                    {
                        var args = new KeyboardHookEventArgs { Key = key, IsDown = down, ScanCode = scan, Extended = extended };
                        KeyEvent?.Invoke(this, args);
                        if (args.Handled)
                            return (IntPtr)1;
                    }
                }
            }
            catch (Exception ex)
            {
                // Never let an exception escape the hook chain — but leave a trace.
                Diagnostics.LogError("keyboard hook", ex);
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <summary>
    /// The keys ShareSwitch is allowed to observe. Everything else is dropped
    /// inside the hook callback and never leaves this class.
    ///
    /// Modifiers and Escape always pass, because they drive selection mode.
    /// Action keys (digits and arrows, used for slot and cycle shortcuts) pass
    /// <b>only while a modifier is physically held</b> — so ordinary typing is
    /// still discarded immediately, before any other code can see it.
    /// </summary>
    private static bool IsRelevantKey(Keys key, int scan, bool extended)
    {
        switch (key)
        {
            case Keys.ControlKey or Keys.LControlKey or Keys.RControlKey:
            case Keys.Menu or Keys.LMenu or Keys.RMenu:
            case Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey:
            case Keys.Escape:
                return true;
        }

        // Digits are recognised by physical position too (any keyboard layout,
        // and the numpad even when Shift turns its digits into End/Down/…).
        bool action = key is Keys.Left or Keys.Right || WindowSlots.IndexFromKey(key, scan, extended) >= 0;
        if (!action) return false;

        // Gate: no modifier held → this is ordinary typing, discard it.
        return NativeMethods.IsKeyDown(NativeMethods.VK_CONTROL)
            || NativeMethods.IsKeyDown(NativeMethods.VK_MENU)
            || NativeMethods.IsKeyDown(NativeMethods.VK_SHIFT);
    }

    #region P/Invoke

    private const int WH_KEYBOARD_LL = 13;
    private const uint LLKHF_EXTENDED = 0x01;
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
