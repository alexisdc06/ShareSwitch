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

        if (!_comboHeld && held == required)
        {
            // Exact match required: holding extra modifiers (e.g. Ctrl+Alt+Shift
            // for an app shortcut) must not arm selection mode.
            _comboHeld = true;
            _activationTimer.Interval = Math.Max(1, AppSettings.Current.SelectionActivationDelayMs);
            _activationTimer.Start();
        }
        else if (_comboHeld && (held & required) != required)
        {
            _comboHeld = false;
            _activationTimer.Stop();
            if (_selecting)
                ExitSelectionMode();
        }
    }

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
        _overlay.ShowOn(root, NativeMethods.GetWindowTitle(root));
    }

    private void OnLeftButtonDown(object? sender, MouseButtonEventArgs e)
    {
        if (!_selecting) return;

        IntPtr root = WindowEnumerator.GetTopLevelWindowAt(e.Location);
        if (root == IntPtr.Zero || !WindowEnumerator.IsCaptureCandidate(root))
            return; // not a valid target — let the click through untouched

        e.Handled = true; // swallow the click so the target app never sees it

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
