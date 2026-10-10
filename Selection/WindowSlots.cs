using ShareSwitch.Interop;

namespace ShareSwitch.Selection;

/// <summary>
/// Nine numbered shortcuts to specific windows, recalled with modifier + 1…9.
///
/// Assignments hold live window handles, so they last for the current Windows
/// session only — a handle means nothing after the target app restarts, and
/// silently switching to "whatever has that handle now" mid-call would be worse
/// than an empty slot.
/// </summary>
public sealed class WindowSlots
{
    public const int Count = 9;

    private readonly IntPtr[] _handles = new IntPtr[Count];
    private readonly string[] _titles = new string[Count];

    public WindowSlots() => Array.Fill(_titles, string.Empty);

    /// <summary>
    /// Maps a key to slot 0–8 (digits 1–9), or -1. Recognised three ways, so it
    /// works on every layout and keyboard:
    /// the virtual key (1–9, NumPad1–9); the physical top-row position (scan
    /// codes 0x02–0x0A, layout-independent); and the numpad position, which
    /// Windows reports as End/Down/PgDn/… when Shift is held with Num Lock on.
    /// </summary>
    public static int IndexFromKey(System.Windows.Forms.Keys key, int scanCode = 0, bool extended = false)
    {
        if (key >= System.Windows.Forms.Keys.D1 && key <= System.Windows.Forms.Keys.D9)
            return key - System.Windows.Forms.Keys.D1;
        if (key >= System.Windows.Forms.Keys.NumPad1 && key <= System.Windows.Forms.Keys.NumPad9)
            return key - System.Windows.Forms.Keys.NumPad1;
        if (extended)
            return -1;   // arrow cluster / navigation block, not digits
        if (scanCode >= 0x02 && scanCode <= 0x0A)
            return scanCode - 0x02;
        return scanCode switch
        {
            0x4F => 0, 0x50 => 1, 0x51 => 2,   // numpad 1 2 3
            0x4B => 3, 0x4C => 4, 0x4D => 5,   // numpad 4 5 6
            0x47 => 6, 0x48 => 7, 0x49 => 8,   // numpad 7 8 9
            _ => -1
        };
    }

    public void Assign(int index, IntPtr hwnd, string title)
    {
        if (!IsValidIndex(index)) return;
        _handles[index] = hwnd;
        _titles[index] = title;
    }

    /// <summary>
    /// Returns the window in a slot, or false if the slot is empty or its
    /// window has since been closed (in which case the slot is cleared).
    /// </summary>
    public bool TryGet(int index, out IntPtr hwnd, out string title)
    {
        hwnd = IntPtr.Zero;
        title = string.Empty;
        if (!IsValidIndex(index)) return false;

        IntPtr candidate = _handles[index];
        if (candidate == IntPtr.Zero) return false;

        if (!NativeMethods.IsWindow(candidate))
        {
            Clear(index); // target has gone away
            return false;
        }

        hwnd = candidate;
        title = _titles[index];
        return true;
    }

    public void Clear(int index)
    {
        if (!IsValidIndex(index)) return;
        _handles[index] = IntPtr.Zero;
        _titles[index] = string.Empty;
    }

    /// <summary>Slot labels for the settings UI: index → title, or null if free.</summary>
    public IReadOnlyList<string?> Describe()
    {
        var result = new string?[Count];
        for (int i = 0; i < Count; i++)
            result[i] = TryGet(i, out _, out string title) ? title : null;
        return result;
    }

    private static bool IsValidIndex(int index) => index >= 0 && index < Count;
}
