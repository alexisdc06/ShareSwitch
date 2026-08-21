using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShareSwitch.Interop;

namespace ShareSwitch.Selection;

/// <summary>
/// Borderless, topmost, click-through frame that <b>flashes once</b> over the
/// window you just picked, then fades out on its own.
///
/// Self-limiting by design: the overlay starts its own countdown the moment it
/// becomes visible, so it can never remain stuck on screen even if selection
/// mode ends abnormally (missed key-up, dropped hook, app losing focus).
///
/// WS_EX_TRANSPARENT keeps it invisible to WindowFromPoint; WS_EX_NOACTIVATE
/// means it never steals focus from the call you're presenting in.
/// </summary>
public sealed class HighlightOverlay : Form
{
    private static readonly Color BorderColor = Color.FromArgb(0, 170, 255);
    private const int BorderThickness = 4;

    // ~30 ms per tick: hold at full opacity, then fade out.
    private const int TickMs = 30;
    private const int HoldTicks = 7;  // ~210 ms
    private const int FadeTicks = 8;  // ~240 ms

    private readonly System.Windows.Forms.Timer _timer;
    private int _tick;
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

        _timer = new System.Windows.Forms.Timer { Interval = TickMs };
        _timer.Tick += OnTick;
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
    /// Flashes the confirmation frame over <paramref name="hWnd"/>.
    /// Safe to call repeatedly; a new flash restarts the countdown.
    /// </summary>
    public void Flash(IntPtr hWnd, string title)
    {
        var bounds = NativeMethods.GetVisibleWindowBounds(hWnd);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        _title = title;
        _tick = 0;
        Bounds = bounds;
        Opacity = 1.0;

        // Show without activating, so focus stays where the user left it.
        NativeMethods.SetWindowPos(Handle, IntPtr.Zero, bounds.X, bounds.Y,
            bounds.Width, bounds.Height,
            NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOACTIVATE);

        Invalidate();
        _timer.Start();
    }

    /// <summary>Cancels any in-flight flash immediately (used on shutdown).</summary>
    public void HideNow()
    {
        _timer.Stop();
        _tick = 0;
        if (Visible) Hide();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _tick++;

        if (_tick <= HoldTicks) return;

        int fadeStep = _tick - HoldTicks;
        if (fadeStep >= FadeTicks)
        {
            HideNow();
            return;
        }

        Opacity = 1.0 - (double)fadeStep / FadeTicks;
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
            using var font = new Font("Segoe UI", 10f, FontStyle.Bold);
            string text = _title.Length > 80 ? _title[..80] + "…" : _title;
            SizeF size = g.MeasureString(text, font);

            var chip = new RectangleF(BorderThickness + 6, BorderThickness + 6,
                size.Width + 16, size.Height + 8);
            using var bg = new SolidBrush(BorderColor);
            g.FillRectangle(bg, chip);
            g.DrawString(text, font, Brushes.White, chip.X + 8, chip.Y + 4);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
