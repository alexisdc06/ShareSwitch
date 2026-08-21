using System.Windows.Forms;
using ShareSwitch.Hooks;
using ShareSwitch.Interop;

namespace ShareSwitch.Selection;

/// <summary>
/// Orchestrates hold-to-select mode:
///
///   hold the configured modifier combo (default Ctrl+Alt)
///     → ShareWindow becomes click-through and the mouse hook is installed
///   left-click any window → it becomes the mirrored source; a confirmation
///     frame flashes once over it and fades out
///   release the combo, or press Esc → cancel
///
/// Deliberately does <b>no work on mouse movement</b>. An earlier version drew a
/// highlight frame that followed the cursor, which meant repainting a form from
/// inside the low-level mouse hook callback — visually noisy, and slow enough
/// that Windows could silently drop the hook (leaving the frame stuck on screen).
/// Now the hook only reacts to a click, and the frame is self-expiring.
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
    private readonly Func<ShareWindow> _getShareWindow;

    private bool _comboHeld;
    private bool _selecting;

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
        _safetyTimer.Start();
        _getShareWindow().SetClickThrough(true);
    }

    private void ExitSelectionMode()
    {
        if (!_selecting) return;

        _selecting = false;
        _safetyTimer.Stop();
        _mouseHook.Uninstall();
        _getShareWindow().SetClickThrough(false);
    }

    private void OnLeftButtonDown(object? sender, MouseButtonEventArgs e)
    {
        if (!_selecting) return;

        IntPtr root = WindowEnumerator.GetTopLevelWindowAt(e.Location);
        if (root == IntPtr.Zero || !WindowEnumerator.IsCaptureCandidate(root))
            return; // not a valid target — let the click through untouched

        e.Handled = true; // swallow the click so the target app never sees it

        string title = NativeMethods.GetWindowTitle(root);
        ExitSelectionMode();

        if (_getShareWindow().SetSource(root, title))
            _overlay.Flash(root, title); // confirmation only; fades out by itself
    }

    #endregion

    public void Dispose()
    {
        _activationTimer.Dispose();
        _safetyTimer.Dispose();
        _mouseHook.Dispose();
        _keyboardHook.Dispose();
        _overlay.HideNow();
        _overlay.Dispose();
    }
}
