using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShareSwitch.Interop;

namespace ShareSwitch.Selection;

/// <summary>
/// Borderless, topmost, click-through window that draws a colored frame around
/// the window currently under the mouse during selection mode.
///
/// WS_EX_TRANSPARENT keeps it invisible to WindowFromPoint, so it never
/// becomes its own selection target; WS_EX_NOACTIVATE ensures it never steals
/// focus from the call/app the user is presenting.
/// </summary>
public sealed class HighlightOverlay : Form
{
    private static readonly Color BorderColor = Color.FromArgb(0, 170, 255);
    private const int BorderThickness = 4;

    private string _targetTitle = string.Empty;

    public HighlightOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;

        // Everything painted in BackColor becomes fully transparent;
        // only the border + title chip remain visible.
        BackColor = Color.Black;
        TransparencyKey = Color.Black;

        SetStyle(ControlStyles.AllPaintingInWmPaint
               | ControlStyles.UserPaint
               | ControlStyles.OptimizedDoubleBuffer, true);
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

    /// <summary>Moves the overlay frame onto the given window.</summary>
    public void HighlightWindow(IntPtr hWnd, string title)
    {
        var bounds = NativeMethods.GetVisibleWindowBounds(hWnd);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            HideHighlight();
            return;
        }

        _targetTitle = title;
        Bounds = bounds;

        if (!Visible)
            NativeMethods.SetWindowPos(Handle, IntPtr.Zero, bounds.X, bounds.Y,
                bounds.Width, bounds.Height,
                NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOACTIVATE);

        Invalidate();
    }

    public void HideHighlight()
    {
        _targetTitle = string.Empty;
        if (Visible) Hide();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;

        // Frame (drawn inset so the full thickness stays inside our bounds)
        using (var pen = new Pen(BorderColor, BorderThickness))
        {
            int half = BorderThickness / 2;
            g.DrawRectangle(pen, half, half,
                ClientSize.Width - BorderThickness, ClientSize.Height - BorderThickness);
        }

        // Title chip
        if (_targetTitle.Length > 0)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var font = new Font("Segoe UI", 10f, FontStyle.Bold);
            string text = _targetTitle.Length > 80 ? _targetTitle[..80] + "…" : _targetTitle;
            SizeF size = g.MeasureString(text, font);

            var chip = new RectangleF(BorderThickness + 6, BorderThickness + 6,
                size.Width + 16, size.Height + 8);
            using var bg = new SolidBrush(BorderColor);
            g.FillRectangle(bg, chip);
            g.DrawString(text, font, Brushes.White, chip.X + 8, chip.Y + 4);
        }
    }
}
