using System.Drawing;
using System.Windows.Forms;
using ShareSwitch.Hooks;
using ShareSwitch.Interop;

namespace ShareSwitch.Selection;

/// <summary>
/// Orchestrates hold-to-select mode:
///
///   hold the configured modifier combo (default Ctrl+Alt)
///     → ShareWindow becomes click-through, mouse hook is installed,
///       the window under the cursor gets a live highlight frame
///   left-click → ShareWindow switches to that window (click is swallowed)
///   release the combo, or press Esc → cancel
///
/// The keyboard hook is always installed; the mouse hook only while selecting.
/// A short activation delay avoids overlay flicker from AltGr typing on AZERTY
/// layouts (AltGr is reported as Ctrl+Alt) and from ordinary app shortcuts.
///
/// Everything runs on the UI thread (LL hooks deliver to the installing thread).
/// </summary>
public sealed class SelectionController : IDisposable
{
    private readonly KeyboardHook _keyboardHook = new();
    private readonly MouseHook _mouseHook = new();
    private readonly HighlightOverlay _overlay = new();
    private readonly System.Windows.Forms.Timer _activationTimer = new();
    private readonly Func<ShareWindow> _getShareWindow;

    private bool _comboHeld;
    private bool _selecting;
    private IntPtr _targetHwnd = IntPtr.Zero;
    private string _targetTitle = string.Empty;

    /// <param name="getShareWindow">Returns the current ShareWindow (creating it
    /// if needed) — the tray context owns its lifetime.</param>
    public SelectionController(Func<ShareWindow> getShareWindow)
    {
        _getShareWindow = getShareWindow;

        _keyboardHook.KeyEvent += OnKeyEvent;
        _mouseHook.MouseMoved += OnMouseMoved;
        _mouseHook.LeftButtonDown += OnLeftButtonDown;

        _activationTimer.Tick += (_, _) =>
        {
            _activationTimer.Stop();
            if (_comboHeld && !_selecting)
                EnterSelectionMode();
        };
    }

    /// <summary>Installs the global keyboard hook. Call once at startup.</summary>
    public void Start() => _keyboardHook.Install();

    #region Keyboard

    private void OnKeyEvent(object? sender, KeyboardHookEventArgs e)
    {
        // Esc cancels selection mode (and is swallowed so the focused app never sees it).
        if (_selecting && e.IsDown && e.Key == Keys.Escape)
        {
            e.Handled = true;
            ExitSelectionMode();
            return;
        }

        Keys required = AppSettings.Current.SelectionModifiers & (Keys.Control | Keys.Alt | Keys.Shift);
        if (required == Keys.None) return; // misconfigured — never trigger

        Keys held = GetHeldModifiers(e);

        if (!_comboHeld && held == required)
        {
            // Exact match required to engage: holding extra modifiers
            // (e.g. Ctrl+Alt+Shift for an app shortcut) must not trigger us.
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
        Keys held = Keys.None;
        if (NativeMethods.IsKeyDown(NativeMethods.VK_CONTROL)) held |= Keys.Control;
        if (NativeMethods.IsKeyDown(NativeMethods.VK_MENU)) held |= Keys.Alt;
        if (NativeMethods.IsKeyDown(NativeMethods.VK_SHIFT)) held |= Keys.Shift;

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
            // Without the mouse hook selection mode cannot work — abort quietly.
            return;
        }

        _selecting = true;
        _getShareWindow().SetClickThrough(true);
        UpdateTarget(Cursor.Position);
    }

    private void ExitSelectionMode()
    {
        _selecting = false;
        _mouseHook.Uninstall();
        _overlay.HideHighlight();
        _targetHwnd = IntPtr.Zero;
        _targetTitle = string.Empty;
        _getShareWindow().SetClickThrough(false);
    }

    private void OnMouseMoved(Point screenPoint)
    {
        if (_selecting)
            UpdateTarget(screenPoint);
    }

    private void OnLeftButtonDown(object? sender, MouseButtonEventArgs e)
    {
        if (!_selecting) return;

        UpdateTarget(e.Location); // make sure we commit what's actually under the click

        if (_targetHwnd == IntPtr.Zero) return; // nothing valid → let the click through

        e.Handled = true; // swallow the click so the target app doesn't receive it

        IntPtr hwnd = _targetHwnd;
        string title = _targetTitle;
        ExitSelectionMode();
        _getShareWindow().SetSource(hwnd, title);
    }

    private void UpdateTarget(Point screenPoint)
    {
        IntPtr root = WindowEnumerator.GetTopLevelWindowAt(screenPoint);

        if (root == _targetHwnd) return; // unchanged — keep hook callback cheap

        if (root != IntPtr.Zero && WindowEnumerator.IsCaptureCandidate(root))
        {
            _targetHwnd = root;
            _targetTitle = NativeMethods.GetWindowTitle(root);
            _overlay.HighlightWindow(root, _targetTitle);
        }
        else
        {
            _targetHwnd = IntPtr.Zero;
            _targetTitle = string.Empty;
            _overlay.HideHighlight();
        }
    }

    #endregion

    public void Dispose()
    {
        _activationTimer.Dispose();
        _mouseHook.Dispose();
        _keyboardHook.Dispose();
        _overlay.Dispose();
    }
}
