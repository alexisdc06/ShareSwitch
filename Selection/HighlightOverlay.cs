using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShareSwitch.Interop;

namespace ShareSwitch.Selection;

/// <summary>
/// Borderless, topmost, click-through frame drawn around the window the user is
/// about to pick. It follows the cursor during selection, then <b>fades out</b>
/// once a window has been chosen so it never lingers on screen.
///
/// Safety: whenever it is visible a max-lifetime watchdog runs, so the frame
/// cannot stay stuck even if selection mode ends abnormally (missed key-up,
/// dropped hook, app losing focus).
///
/// WS_EX_TRANSPARENT keeps it invisible to WindowFromPoint (so it is never its
/// own selection target); WS_EX_NOACTIVATE means it never steals focus from the
/// call being presented.
/// </summary>
public sealed class HighlightOverlay : Form
{
    private static readonly Color BorderColor = Theme.Violet;
    private const int BorderThickness = 4;

    private const int FadeTickMs = 25;
    private const int FadeTicks = 10;      // ~250 ms fade-out
    private const int MaxLifetimeMs = 30_000; // failsafe, never normally reached

    private readonly System.Windows.Forms.Timer _fadeTimer;
    private readonly System.Windows.Forms.Timer _lifetimeGuard;
    private int _fadeStep;
    private string _title = string.Empty;

    public HighlightOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;

        // Everything painted in BackColor is fully transparent; only the frame
        // and the title chip remain visible.
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
    public void ShowOn(IntPtr hWnd, string title)
    {
        var bounds = NativeMethods.GetVisibleWindowBounds(hWnd);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            HideNow();
            return;
        }

        _fadeTimer.Stop();
        _fadeStep = 0;

        bool changed = _title != title || Bounds != bounds;
        _title = title;
        Bounds = bounds;
        Opacity = 1.0;

        if (!Visible)
        {
            // Must go through Show(), not SetWindowPos(SWP_SHOWWINDOW): showing
            // the window behind WinForms' back leaves Visible == false, and then
            // Hide() silently does nothing — which is exactly how the frame used
            // to get stuck on screen. ShowWithoutActivation + WS_EX_NOACTIVATE
            // already guarantee focus is never stolen.
            Show();

            _lifetimeGuard.Stop();
            _lifetimeGuard.Start(); // failsafe: never visible longer than this
        }

        if (changed) Invalidate();
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

        // Unconditional: never trust a cached visibility flag to decide whether
        // the frame needs hiding.
        if (IsHandleCreated) Hide();
        Opacity = 1.0; // reset for the next show
    }

    private void OnFadeTick(object? sender, EventArgs e)
    {
        _fadeStep++;
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
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var pen = new Pen(BorderColor, BorderThickness))
        {
            int half = BorderThickness / 2;
            g.DrawRectangle(pen, half, half,
                ClientSize.Width - BorderThickness, ClientSize.Height - BorderThickness);
        }

        if (_title.Length > 0)
        {
            // Brand title chip. Shape edges stay aliased on purpose: this form
            // keys out pure black, so anti-aliased edges would leave dark fringes.
            Font font = Theme.Head(LogicalToDeviceUnits(14));
            string text = _title.Length > 80 ? _title[..80] + "…" : _title;
            float tw = Theme.Measure(g, text, font);
            float padX = LogicalToDeviceUnits(12), h = font.Size * 2.0f;
            var chip = new RectangleF(BorderThickness + 8, BorderThickness + 8, tw + padX * 2, h);
            g.SmoothingMode = SmoothingMode.None;
            using (var path = Theme.RoundedRect(chip, LogicalToDeviceUnits(6)))
            using (var bg = new SolidBrush(BorderColor))
                g.FillPath(bg, path);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            Theme.DrawRun(g, text, font, Color.White, chip.X + padX, chip.Y + h / 2 + font.Size * 0.35f);
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
