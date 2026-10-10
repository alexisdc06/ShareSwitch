using System.Windows.Forms;
using ShareSwitch.Hooks;
using ShareSwitch.Interop;

namespace ShareSwitch.Selection;

/// <summary>
/// Orchestrates hold-to-select mode:
///
///   hold the configured modifier combo (default Ctrl+Alt)
///     → ShareWindow becomes click-through, the mouse hook is installed, and a
///       highlight frame follows the window under the cursor
///   left-click any window → it becomes the mirrored source and the frame fades
///     out, so nothing lingers on screen
///   release the combo, or press Esc → cancel
///
/// The frame is driven by a UI timer polling the cursor position, <b>not</b> by
/// the mouse hook. Repainting a form from inside a low-level hook callback can
/// exceed the Windows hook timeout, at which point Windows silently unhooks —
/// which used to leave the frame orphaned on screen. The hook now only handles
/// clicks, which are rare and cheap.
///
/// The keyboard hook is always installed; the mouse hook only while selecting.
/// A short activation delay avoids triggering on AltGr (reported as Ctrl+Alt on
/// AZERTY layouts) and on ordinary app shortcuts.
///
/// Everything runs on the UI thread (LL hooks deliver to the installing thread).
/// </summary>
public sealed class SelectionController : IDisposable
{
    private readonly KeyboardHook _keyboardHook = new();
    private readonly MouseHook _mouseHook = new();
    private readonly HighlightOverlay _overlay = new();
    private readonly System.Windows.Forms.Timer _activationTimer = new();
    private readonly System.Windows.Forms.Timer _safetyTimer = new() { Interval = 200 };
    private readonly System.Windows.Forms.Timer _trackTimer = new() { Interval = 30 };
    private readonly Func<ShareWindow> _getShareWindow;

    private bool _comboHeld;
    private bool _selecting;
    private IntPtr _hoveredHwnd = IntPtr.Zero;

    /// <summary>Windows assigned to modifier + 1…9.</summary>
    public WindowSlots Slots { get; } = new();

    /// <summary>
    /// Window list captured when cycling starts, so the order can't shift under
    /// the user while they step through it. Cleared when the combo is released.
    /// </summary>
    private List<WindowEnumerator.WindowInfo>? _cycleSnapshot;
    private int _cycleIndex = -1;

    /// <param name="getShareWindow">Returns the current ShareWindow (creating it
    /// if needed) — the tray context owns its lifetime.</param>
    public SelectionController(Func<ShareWindow> getShareWindow)
    {
        _getShareWindow = getShareWindow;

        _keyboardHook.KeyEvent += OnKeyEvent;
        _mouseHook.LeftButtonDown += OnLeftButtonDown;

        _activationTimer.Tick += (_, _) =>
        {
            _activationTimer.Stop();
            if (_comboHeld && !_selecting)
                EnterSelectionMode();
        };

        // Failsafe: if a key-up is ever missed (focus loss, UAC prompt, lock
        // screen, dropped hook), physical key state still tells the truth.
        _safetyTimer.Tick += (_, _) =>
        {
            if (_selecting && !AreRequiredModifiersPhysicallyHeld())
            {
                _comboHeld = false;
                ExitSelectionMode();
            }
        };

        _trackTimer.Tick += (_, _) => UpdateHoverHighlight();
    }

    /// <summary>Installs the global keyboard hook. Call once at startup.</summary>
    public void Start() => _keyboardHook.Install();

    #region Keyboard

    private static Keys RequiredModifiers
        => AppSettings.Current.SelectionModifiers & (Keys.Control | Keys.Alt | Keys.Shift);

    private void OnKeyEvent(object? sender, KeyboardHookEventArgs e)
    {
        // Esc cancels selection (swallowed, so the focused app never sees it).
        if (_selecting && e.IsDown && e.Key == Keys.Escape)
        {
            e.Handled = true;
            _comboHeld = false;
            ExitSelectionMode();
            return;
        }

        Keys required = RequiredModifiers;
        if (required == Keys.None) return; // misconfigured — never trigger

        Keys held = GetHeldModifiers(e);

        // Slot / cycle shortcuts fire on key-down while the combo is held.
        if (e.IsDown && (held & required) == required && HandleActionKey(e, held, required))
        {
            e.Handled = true; // don't let the shortcut reach the focused app
            return;
        }

        // Exact match arms selection; the one exception is combo + Shift, which
        // arms it too (for assigning numbers) so the user sees what they're
        // pointing at. Other extra modifiers still don't arm anything.
        bool arms = held == required || (CanAssign(required) && held == (required | Keys.Shift));
        if (!_comboHeld && arms)
        {
            _comboHeld = true;
            _activationTimer.Interval = Math.Max(1, AppSettings.Current.SelectionActivationDelayMs);
            _activationTimer.Start();
        }
        else if (_comboHeld && (held & required) != required)
        {
            _comboHeld = false;
            _activationTimer.Stop();
            _cycleSnapshot = null; // fresh window list next time
            _cycleIndex = -1;
            if (_selecting)
                ExitSelectionMode();
        }
    }

    #endregion

    #region Shortcut handling and modifier state

    /// <summary>
    /// Handles the shortcuts available while the combo is held:
    /// <list type="bullet">
    /// <item>modifier + 1…9 — switch to the window assigned to that slot</item>
    /// <item>modifier + Shift + 1…9 — assign the window under the cursor to that slot</item>
    /// <item>modifier + ← / → — step through open windows</item>
    /// </list>
    /// </summary>
    /// <returns>true if the key was consumed.</returns>
    private static bool CanAssign(Keys required)
        => AppSettings.Current.EnableSlotShortcuts && !required.HasFlag(Keys.Shift);

    private bool HandleActionKey(KeyboardHookEventArgs e, Keys held, Keys required)
    {
        Keys key = e.Key;
        int slot = WindowSlots.IndexFromKey(key, e.ScanCode, e.Extended);
        if (slot >= 0)
        {
            if (!AppSettings.Current.EnableSlotShortcuts) return false;

            // Shift is the "modify" modifier — but only when it isn't already
            // part of the user's own combo, which would make assign and recall
            // indistinguishable.
            bool assigning = !required.HasFlag(Keys.Shift) && held.HasFlag(Keys.Shift);
            return assigning ? AssignHoveredToSlot(slot) : RecallSlot(slot);
        }

        if (key is Keys.Left or Keys.Right)
        {
            if (!AppSettings.Current.EnableCycleShortcuts) return false;
            return Cycle(key == Keys.Right ? 1 : -1);
        }

        return false;
    }

    private bool AssignHoveredToSlot(int slot)
    {
        IntPtr target = _selecting && _hoveredHwnd != IntPtr.Zero
            ? _hoveredHwnd
            : WindowEnumerator.GetTopLevelWindowAt(Cursor.Position);

        if (target == IntPtr.Zero || !WindowEnumerator.IsCaptureCandidate(target))
            return false;

        SaveAndShow(target, slot);
        return true;
    }

    private bool RecallSlot(int slot)
    {
        if (!Slots.TryGet(slot, out IntPtr hwnd, out string title)) return false;
        SwitchTo(hwnd, title, $"{slot + 1}  ·  {title}", OverlayMode.Select, (slot + 1).ToString(), holdMs: 450);
        return true;
    }

    /// <summary>
    /// Steps through the open windows. The list is snapshotted on the first
    /// press so it can't reorder mid-sequence; releasing the combo resets it.
    /// </summary>
    private bool Cycle(int direction)
    {
        if (_cycleSnapshot is null)
        {
            _cycleSnapshot = WindowEnumerator.GetCaptureCandidates();
            if (_cycleSnapshot.Count == 0)
            {
                _cycleSnapshot = null;
                return false;
            }

            // Start from whatever is currently mirrored, so the first press
            // moves one step away from it rather than jumping arbitrarily.
            IntPtr current = _getShareWindow().SourceHwnd;
            _cycleIndex = _cycleSnapshot.FindIndex(w => w.Hwnd == current);
        }

        // Skip windows that have closed since the snapshot was taken.
        for (int attempt = 0; attempt < _cycleSnapshot.Count; attempt++)
        {
            _cycleIndex = ((_cycleIndex + direction) % _cycleSnapshot.Count + _cycleSnapshot.Count)
                          % _cycleSnapshot.Count;

            var candidate = _cycleSnapshot[_cycleIndex];
            if (NativeMethods.IsWindow(candidate.Hwnd))
            {
                SwitchTo(candidate.Hwnd, candidate.Title);
                return true;
            }
        }

        _cycleSnapshot = null;
        return false;
    }

    /// <summary>Switches the mirror and flashes the confirmation frame.</summary>
    /// <summary>Switches the mirror and shows a confirmation frame (optionally with a number badge).</summary>
    private void SwitchTo(IntPtr hwnd, string title, string? label = null,
                          OverlayMode mode = OverlayMode.Select, string? badge = null, int holdMs = 0)
    {
        // The user is driving by keyboard now, so stop the cursor-following
        // highlight — otherwise the next tracking tick would immediately pull
        // the frame back onto whatever the mouse happens to be over.
        ExitSelectionMode();

        if (_getShareWindow().SetSource(hwnd, title))
            _overlay.Confirm(hwnd, label ?? title, mode, badge, holdMs);
    }

    /// <summary>
    /// Saves a window to a number, mirrors it, and confirms loudly: fluo-green
    /// frame, "Saved to N" chip and a big N badge held for about a second.
    /// </summary>
    private void SaveAndShow(IntPtr hwnd, int slot)
    {
        string title = NativeMethods.GetWindowTitle(hwnd);
        Slots.Assign(slot, hwnd, title);
        SwitchTo(hwnd, title, $"Saved to {slot + 1}  ·  Ctrl+Alt+{slot + 1} to come back",
                 OverlayMode.Assign, (slot + 1).ToString(), holdMs: 1100);
    }

    /// <summary>Shift held on top of the combo, with number shortcuts enabled.</summary>
    private static bool AssignHintActive()
        => CanAssign(RequiredModifiers) && ReadPhysicalModifiers().HasFlag(Keys.Shift);

    /// <summary>
    /// Modifier state during a low-level hook event. GetAsyncKeyState does not
    /// yet reflect the key that caused this very event, so the event key is
    /// folded in manually.
    /// </summary>
    private static Keys GetHeldModifiers(KeyboardHookEventArgs e)
    {
        Keys held = ReadPhysicalModifiers();

        Keys eventModifier = e.Key switch
        {
            Keys.ControlKey or Keys.LControlKey or Keys.RControlKey => Keys.Control,
            Keys.Menu or Keys.LMenu or Keys.RMenu => Keys.Alt,
            Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey => Keys.Shift,
            _ => Keys.None
        };

        if (eventModifier != Keys.None)
            held = e.IsDown ? held | eventModifier : held & ~eventModifier;

        return held;
    }

    private static Keys ReadPhysicalModifiers()
    {
        Keys held = Keys.None;
        if (NativeMethods.IsKeyDown(NativeMethods.VK_CONTROL)) held |= Keys.Control;
        if (NativeMethods.IsKeyDown(NativeMethods.VK_MENU)) held |= Keys.Alt;
        if (NativeMethods.IsKeyDown(NativeMethods.VK_SHIFT)) held |= Keys.Shift;
        return held;
    }

    private static bool AreRequiredModifiersPhysicallyHeld()
    {
        Keys required = RequiredModifiers;
        return required != Keys.None && (ReadPhysicalModifiers() & required) == required;
    }

    #endregion

    #region Selection mode

    private void EnterSelectionMode()
    {
        try
        {
            _mouseHook.Install();
        }
        catch (Exception)
        {
            // Without the mouse hook selection cannot work — abort quietly.
            return;
        }

        _selecting = true;
        _hoveredHwnd = IntPtr.Zero;
        _safetyTimer.Start();
        _trackTimer.Start();
        _getShareWindow().SetClickThrough(true);
        UpdateHoverHighlight(); // paint immediately, don't wait for the first tick
    }

    /// <param name="fadeOverlay">true when a window was picked (fade as
    /// confirmation); false on cancel, where the frame should just vanish.</param>
    private void ExitSelectionMode(bool fadeOverlay = false)
    {
        if (!_selecting) return;

        _selecting = false;
        _trackTimer.Stop();
        _safetyTimer.Stop();
        _mouseHook.Uninstall();
        _hoveredHwnd = IntPtr.Zero;
        _getShareWindow().SetClickThrough(false);

        if (fadeOverlay) _overlay.FadeOut();
        else _overlay.HideNow();
    }

    /// <summary>Moves the highlight onto whatever valid window is under the cursor.</summary>
    private void UpdateHoverHighlight()
    {
        if (!_selecting) return;

        IntPtr root = WindowEnumerator.GetTopLevelWindowAt(Cursor.Position);

        if (root == IntPtr.Zero || !WindowEnumerator.IsCaptureCandidate(root))
        {
            _hoveredHwnd = IntPtr.Zero;
            _overlay.HideNow();
            return;
        }

        _hoveredHwnd = root;
        string title = NativeMethods.GetWindowTitle(root);
        if (AssignHintActive())
        {
            // Saving mode: fluo green, and say exactly what will happen.
            int existing = Slots.IndexOf(root);
            string where = existing >= 0 ? $"saved as {existing + 1} · " : string.Empty;
            _overlay.ShowOn(root, $"{where}Press 1–9 or click to save  ·  {title}", OverlayMode.Assign);
        }
        else
        {
            _overlay.ShowOn(root, title);
        }
    }

    private void OnLeftButtonDown(object? sender, MouseButtonEventArgs e)
    {
        if (!_selecting) return;

        IntPtr root = WindowEnumerator.GetTopLevelWindowAt(e.Location);
        if (root == IntPtr.Zero || !WindowEnumerator.IsCaptureCandidate(root))
            return; // not a valid target — let the click through untouched

        e.Handled = true; // swallow the click so the target app never sees it

        // Ctrl+Alt+Shift + click: save to a number without touching the number
        // row (keeps its slot if already saved, else the first free one).
        if (AssignHintActive())
        {
            int slot = Slots.IndexOf(root);
            if (slot < 0) slot = Slots.FirstFree();
            if (slot >= 0)
            {
                SaveAndShow(root, slot);
                return;
            }
            ExitSelectionMode();
            if (_getShareWindow().SetSource(root, NativeMethods.GetWindowTitle(root)))
                _overlay.Confirm(root, "All 9 numbers are used — Ctrl+Alt+Shift+number replaces one",
                                 OverlayMode.Assign, null, 1600);
            return;
        }

        string title = NativeMethods.GetWindowTitle(root);

        // The cursor may have moved since the last tick — make sure the frame is
        // on the window actually being clicked before it fades.
        if (root != _hoveredHwnd)
            _overlay.ShowOn(root, title);

        bool ok = _getShareWindow().SetSource(root, title);
        ExitSelectionMode(fadeOverlay: ok);
    }

    #endregion

    public void Dispose()
    {
        _activationTimer.Dispose();
        _safetyTimer.Dispose();
        _trackTimer.Dispose();
        _mouseHook.Dispose();
        _keyboardHook.Dispose();
        _overlay.HideNow();
        _overlay.Dispose();
    }
}
