using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShareSwitch;

/// <summary>On/off switch in brand colours (replaces the stock checkbox).</summary>
internal sealed class ToggleSwitch : Control
{
    private bool _checked;

    public event EventHandler? CheckedChanged;

    public ToggleSwitch(string text)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
               | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
        SetStyle(ControlStyles.StandardDoubleClick, false);   // rapid clicks must each toggle
        Text = text;
        TabStop = true;
        Cursor = Cursors.Hand;
        BackColor = Theme.Navy;
        ForeColor = Theme.OffWhite;
        Margin = new Padding(0, LogicalToDeviceUnits(3), 0, LogicalToDeviceUnits(3));
    }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private int TrackW => LogicalToDeviceUnits(38);
    private int TrackH => LogicalToDeviceUnits(22);
    private int Gap => LogicalToDeviceUnits(12);

    private void UpdateSize()
    {
        Size t = TextRenderer.MeasureText(Text, Font);
        Size = new Size(TrackW + Gap + t.Width + 2, Math.Max(LogicalToDeviceUnits(30), t.Height + 8));
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); UpdateSize(); Invalidate(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); UpdateSize(); }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); UpdateSize(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); UpdateSize(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnClick(EventArgs e)
    {
        if (e is MouseEventArgs { Button: not MouseButtons.Left }) return;
        base.OnClick(e);
        Focus();
        Checked = !Checked;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Space)
        {
            Checked = !Checked;
            e.Handled = true;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var track = new RectangleF(1, (Height - TrackH) / 2f, TrackW, TrackH);
        using (GraphicsPath p = Theme.RoundedRect(track, TrackH / 2f))
        using (var b = new SolidBrush(_checked ? Theme.Violet : Theme.Line))
            g.FillPath(b, p);

        float pad = LogicalToDeviceUnits(3);
        float k = TrackH - pad * 2;
        float kx = _checked ? track.Right - k - pad : track.X + pad;
        using (var kb = new SolidBrush(Color.White))
            g.FillEllipse(kb, kx, track.Y + pad, k, k);

        var textRect = new Rectangle(TrackW + Gap, 0, Width - TrackW - Gap, Height);
        TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? ForeColor : Theme.Muted, BackColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);

        if (Focused && ShowFocusCues)
        {
            using var pen = new Pen(Theme.Violet) { DashStyle = DashStyle.Dot };
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }
}

/// <summary>Rounded brand button. Primary = violet fill; secondary = quiet surface.</summary>
internal sealed class PillButton : Control, IButtonControl
{
    private bool _hover, _down, _isDefault;

    public PillButton(string text, bool primary)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
               | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
        SetStyle(ControlStyles.StandardDoubleClick, false);   // rapid clicks must each fire
        Text = text;
        Primary = primary;
        TabStop = true;
        Cursor = Cursors.Hand;
        BackColor = Theme.Navy;
        ForeColor = Color.White;
        Margin = new Padding(LogicalToDeviceUnits(6), 0, 0, 0);
    }

    public bool Primary { get; set; }
    public DialogResult DialogResult { get; set; }

    public void NotifyDefault(bool value) { _isDefault = value; Invalidate(); }

    public void PerformClick()
    {
        if (Enabled && Visible) OnClick(EventArgs.Empty);
    }

    protected override void OnClick(EventArgs e)
    {
        if (e is MouseEventArgs { Button: not MouseButtons.Left }) return;
        base.OnClick(e);
        if (DialogResult != DialogResult.None && FindForm() is { Modal: true } form)
            form.DialogResult = DialogResult;
    }

    private void UpdateSize()
    {
        Size t = TextRenderer.MeasureText(Text, Font);
        Size = new Size(t.Width + LogicalToDeviceUnits(36), LogicalToDeviceUnits(36));
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); UpdateSize(); Invalidate(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); UpdateSize(); }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); UpdateSize(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); UpdateSize(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; _down = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); _down = true; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _down = false; Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            PerformClick();
            e.Handled = true;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);

        Color fill = Primary
            ? (!Enabled ? Theme.Line : _down ? Theme.VioletDeep : _hover ? Color.FromArgb(166, 157, 255) : Theme.Violet)
            : (_down ? Theme.Line : _hover ? Theme.SurfaceHover : Theme.Surface);

        using (GraphicsPath p = Theme.RoundedRect(r, r.Height / 2))
        {
            using (var b = new SolidBrush(fill)) g.FillPath(b, p);
            bool focusRing = Focused && ShowFocusCues;
            if (!Primary || focusRing)
            {
                using var pen = new Pen(focusRing ? (Primary ? Theme.OffWhite : Theme.Violet) : Theme.Line,
                                        LogicalToDeviceUnits(focusRing ? 2 : 1));
                g.DrawPath(pen, p);
            }
        }

        TextRenderer.DrawText(g, Text, Font, ClientRectangle, Enabled ? ForeColor : Theme.Muted, fill,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

/// <summary>Dialog header banner: brand backdrop, logo lockup, optional caption on the right.</summary>
internal sealed class BrandHeader : Control
{
    public BrandHeader(string caption)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
               | ControlStyles.ResizeRedraw, true);
        Text = caption;
        BackColor = Theme.Navy;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Rectangle rc = ClientRectangle;
        if (rc.Width <= 0 || rc.Height <= 0) return;

        if (Theme.Backdrop is { } bd)
            Theme.PaintCover(g, bd, rc);
        else
            using (var b = new SolidBrush(Theme.Navy)) g.FillRectangle(b, rc);

        // navy wash from the left so the lockup sits on calm ground
        using (var wash = new LinearGradientBrush(rc, Color.FromArgb(170, Theme.Navy), Color.FromArgb(0, Theme.Navy), 0f))
        {
            wash.WrapMode = WrapMode.TileFlipX;
            g.FillRectangle(wash, rc);
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        float pad = LogicalToDeviceUnits(26);
        if (Theme.Lockup is { } lk)
        {
            float h = rc.Height * 0.40f;
            float w = lk.Width * h / lk.Height;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(lk, pad, (rc.Height - h) / 2, w, h);
        }

        if (Text.Length > 0)
        {
            Font f = Theme.Body(LogicalToDeviceUnits(13));
            float tw = Theme.Measure(g, Text, f);
            Theme.DrawRun(g, Text, f, Theme.SoftText, rc.Width - pad - tw, rc.Height / 2f + f.Size * 0.35f);
        }

        using var line = new Pen(Color.FromArgb(90, Theme.Violet));
        g.DrawLine(line, 0, rc.Height - 1, rc.Width, rc.Height - 1);
    }
}

/// <summary>Small uppercase section heading in brand violet.</summary>
internal sealed class SectionLabel : Label
{
    public SectionLabel(string text, Font font)
    {
        Text = text.ToUpperInvariant();
        Font = font;
        ForeColor = Theme.Violet;
        AutoSize = true;
        Margin = new Padding(0, LogicalToDeviceUnits(14), 0, LogicalToDeviceUnits(6));
    }
}

/// <summary>Tray menu in brand colours.</summary>
internal sealed class BrandMenuRenderer : ToolStripProfessionalRenderer
{
    public BrandMenuRenderer() : base(new BrandMenuColors()) => RoundedEdges = false;

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (!e.Item.Enabled)
        {
            // The base renderer forces SystemColors.GrayText on disabled items;
            // the status header should read in brand violet instead.
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, e.TextRectangle, Theme.Violet, e.TextFormat);
            return;
        }
        e.TextColor = Theme.OffWhite;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.OffWhite;
        base.OnRenderArrow(e);
    }

    private sealed class BrandMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Navy;
        public override Color ImageMarginGradientBegin => Theme.Navy;
        public override Color ImageMarginGradientMiddle => Theme.Navy;
        public override Color ImageMarginGradientEnd => Theme.Navy;
        public override Color MenuBorder => Theme.Line;
        public override Color MenuItemBorder => Theme.Violet;
        public override Color MenuItemSelected => Theme.SurfaceHover;
        public override Color MenuItemSelectedGradientBegin => Theme.SurfaceHover;
        public override Color MenuItemSelectedGradientEnd => Theme.SurfaceHover;
        public override Color MenuItemPressedGradientBegin => Theme.Surface;
        public override Color MenuItemPressedGradientEnd => Theme.Surface;
        public override Color SeparatorDark => Theme.Line;
        public override Color SeparatorLight => Theme.Navy;
        public override Color CheckBackground => Theme.Violet;
        public override Color CheckSelectedBackground => Theme.Violet;
        public override Color CheckPressedBackground => Theme.VioletDeep;
    }
}
