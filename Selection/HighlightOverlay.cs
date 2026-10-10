using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShareSwitch.Interop;

namespace ShareSwitch.Selection;

/// <summary>What the highlight frame is communicating.</summary>
public enum OverlayMode
{
    /// <summary>Violet: "click to mirror this window".</summary>
    Select,
    /// <summary>Fluo green: "this window is about to be / was saved to a number".</summary>
    Assign
}

/// <summary>
/// Borderless, topmost, click-through frame drawn around the window the user is
/// about to pick. It follows the cursor during selection and fades out once a
/// window has been chosen, so it never lingers on screen.
///
/// Two looks: violet for selecting, fluo green (double border) while saving a
/// window to a number. Confirmations can carry a large centred number badge and
/// hold briefly before fading, so a save is unmistakable.
///
/// Safety: whenever it is visible a max-lifetime watchdog runs, so the frame
/// cannot stay stuck even if selection mode ends abnormally.
///
/// WS_EX_TRANSPARENT keeps it invisible to WindowFromPoint (never its own
/// target); WS_EX_NOACTIVATE means it never steals focus from the call.
/// Shapes are drawn without anti-aliasing on purpose: the form keys out pure
/// black, so soft edges would leave dark fringes.
/// </summary>
public sealed class HighlightOverlay : Form
{
    private const int FadeTickMs = 25;
    private const int FadeTicks = 10;          // ~250 ms fade-out
    private const int MaxLifetimeMs = 30_000;  // failsafe, never normally reached

    private readonly System.Windows.Forms.Timer _fadeTimer;
    private readonly System.Windows.Forms.Timer _lifetimeGuard;
    private int _fadeStep;
    private string _title = string.Empty;
    private OverlayMode _mode = OverlayMode.Select;
    private string? _badge;

    public HighlightOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;

        // Everything painted in BackColor is fully transparent; only the frame,
        // chip and badge remain visible.
        BackColor = Color.Black;
        TransparencyKey = Color.Black;

        SetStyle(ControlStyles.AllPaintingInWmPaint
               | ControlStyles.UserPaint
               | ControlStyles.OptimizedDoubleBuffer, true);

        _fadeTimer = new System.Windows.Forms.Timer { Interval = FadeTickMs };
        _fadeTimer.Tick += OnFadeTick;

        _lifetimeGuard = new System.Windows.Forms.Timer { Interval = MaxLifetimeMs };
        _lifetimeGuard.Tick += (_, _) => HideNow();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_TRANSPARENT
                        | NativeMethods.WS_EX_TOOLWINDOW
                        | NativeMethods.WS_EX_NOACTIVATE;
            return cp;
        }
    }

    /// <summary>
    /// Moves the frame onto <paramref name="hWnd"/> at full opacity.
    /// Called repeatedly while the cursor moves between windows.
    /// </summary>
    public void ShowOn(IntPtr hWnd, string title, OverlayMode mode = OverlayMode.Select, string? badge = null)
    {
        var bounds = NativeMethods.GetVisibleWindowBounds(hWnd);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            HideNow();
            return;
        }

        _fadeTimer.Stop();
        _fadeStep = 0;

        bool changed = _title != title || Bounds != bounds || _mode != mode || _badge != badge;
        _title = title;
        _mode = mode;
        _badge = badge;
        Bounds = bounds;
        Opacity = 1.0;

        if (!Visible)
        {
            // Must go through Show(), not SetWindowPos(SWP_SHOWWINDOW): showing
            // the window behind WinForms' back leaves Visible == false, and then
            // Hide() silently does nothing.
            Show();

            _lifetimeGuard.Stop();
            _lifetimeGuard.Start(); // failsafe: never visible longer than this
        }

        if (changed) Invalidate();
    }

    /// <summary>
    /// Confirmation: show the frame (optionally with a big number badge), hold it
    /// for <paramref name="holdMs"/>, then fade out.
    /// </summary>
    public void Confirm(IntPtr hWnd, string title, OverlayMode mode, string? badge, int holdMs)
    {
        ShowOn(hWnd, title, mode, badge);
        if (!Visible) return;
        _fadeStep = -Math.Max(0, holdMs / FadeTickMs);   // negative = still holding
        _fadeTimer.Start();
    }

    /// <summary>Fades the frame out — the confirmation that a window was picked.</summary>
    public void FadeOut()
    {
        if (!Visible) return;
        _fadeStep = 0;
        _fadeTimer.Start();
    }

    /// <summary>Hides immediately (cancel, shutdown, invalid target).</summary>
    public void HideNow()
    {
        _fadeTimer.Stop();
        _lifetimeGuard.Stop();
        _fadeStep = 0;
        _title = string.Empty;
        _badge = null;
        _mode = OverlayMode.Select;

        // Unconditional: never trust a cached visibility flag to decide whether
        // the frame needs hiding.
        if (IsHandleCreated) Hide();
        Opacity = 1.0; // reset for the next show
    }

    private void OnFadeTick(object? sender, EventArgs e)
    {
        _fadeStep++;
        if (_fadeStep <= 0) return;           // still in the hold phase
        if (_fadeStep >= FadeTicks)
        {
            HideNow();
            return;
        }
        Opacity = 1.0 - (double)_fadeStep / FadeTicks;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;
        bool assign = _mode == OverlayMode.Assign;
        Color accent = assign ? Theme.Fluo : Theme.Violet;
        Color ink = assign ? Theme.Navy : Color.White;
        int w = ClientSize.Width, h = ClientSize.Height;

        // Frame: violet single line, or a heavier fluo double line when saving.
        int outer = LogicalToDeviceUnits(assign ? 6 : 4);
        using (var pen = new Pen(accent, outer) { Alignment = PenAlignment.Inset })
            g.DrawRectangle(pen, 0, 0, w - 1, h - 1);
        if (assign)
        {
            int gap = outer + LogicalToDeviceUnits(5);
            using var inner = new Pen(accent, LogicalToDeviceUnits(2)) { Alignment = PenAlignment.Inset };
            g.DrawRectangle(inner, gap, gap, w - 1 - gap * 2, h - 1 - gap * 2);
        }

        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        // Title chip, top-left.
        if (_title.Length > 0)
        {
            Font font = Theme.Head(LogicalToDeviceUnits(assign ? 16 : 14));
            string text = _title.Length > 90 ? _title[..90] + "…" : _title;
            float tw = Theme.Measure(g, text, font);
            float padX = LogicalToDeviceUnits(12), ch = font.Size * 2.0f;
            float inset = outer + LogicalToDeviceUnits(assign ? 14 : 8);
            var chip = new RectangleF(inset, inset, Math.Min(tw + padX * 2, w - inset * 2), ch);
            using (var path = Theme.RoundedRect(chip, LogicalToDeviceUnits(6)))
            using (var bg = new SolidBrush(accent))
                g.FillPath(bg, path);
            g.SetClip(chip);
            Theme.DrawRun(g, text, font, ink, chip.X + padX, chip.Y + ch / 2 + font.Size * 0.35f);
            g.ResetClip();
        }

        // Big centred number badge (confirmation of a save / recall).
        if (!string.IsNullOrEmpty(_badge))
        {
            float size = Math.Clamp(Math.Min(w, h) * 0.30f, LogicalToDeviceUnits(72), LogicalToDeviceUnits(190));
            var box = new RectangleF((w - size) / 2, (h - size) / 2, size, size);
            using (var path = Theme.RoundedRect(box, size * 0.22f))
            using (var bg = new SolidBrush(accent))
                g.FillPath(bg, path);
            Font big = Theme.Head(size * 0.58f);
            float bw = Theme.Measure(g, _badge, big);
            Theme.DrawRun(g, _badge, big, ink, box.X + (size - bw) / 2, box.Y + size / 2 + big.Size * 0.36f);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fadeTimer.Dispose();
            _lifetimeGuard.Dispose();
        }
        base.Dispose(disposing);
    }
}
