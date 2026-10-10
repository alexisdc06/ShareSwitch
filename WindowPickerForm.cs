using System.Drawing;
using System.Windows.Forms;

namespace ShareSwitch;

/// <summary>
/// Modal list of capturable windows, in the brand's dark theme. Alternative to
/// hold-to-select for occluded/minimized windows or hook-averse environments.
/// </summary>
public sealed class WindowPickerForm : Form
{
    private readonly ListBox _list;
    private readonly PillButton _okButton;
    private readonly Font _titleFont = new("Segoe UI Semibold", 10.5f);
    private readonly Font _headingFont = new("Segoe UI Semibold", 13f);

    public WindowEnumerator.WindowInfo? SelectedWindow { get; private set; }

    public WindowPickerForm()
    {
        Text = "ShareSwitch — Select Window";
        Icon = AppIcon.Get();
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9.75f);
        BackColor = Theme.Navy;
        ForeColor = Theme.OffWhite;
        Size = LogicalToDeviceUnits(new Size(620, 560));
        MinimumSize = LogicalToDeviceUnits(new Size(440, 360));

        int pad = LogicalToDeviceUnits(22);

        var heading = new Label
        {
            Text = "Which window should your call see?",
            Font = _headingFont,
            ForeColor = Theme.OffWhite,
            Dock = DockStyle.Top,
            Height = LogicalToDeviceUnits(46),
            Padding = new Padding(pad, LogicalToDeviceUnits(16), pad, 0)
        };
        var hint = new Label
        {
            Text = $"Tip: hold {AppSettings.DescribeModifiers(AppSettings.Current.SelectionModifiers)} and click a window to skip this list.",
            ForeColor = Theme.Muted,
            Dock = DockStyle.Top,
            Height = LogicalToDeviceUnits(34),
            Padding = new Padding(pad, LogicalToDeviceUnits(4), pad, 0)
        };

        _list = new ListBox
        {
            Dock = DockStyle.Fill,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = LogicalToDeviceUnits(48),
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Surface,
            ForeColor = Theme.OffWhite,
            IntegralHeight = false
        };
        _list.DrawItem += DrawWindowItem;
        _list.SelectedIndexChanged += (_, _) => _okButton!.Enabled = _list.SelectedIndex >= 0;
        _list.DoubleClick += (_, _) => CommitSelection();
        var listHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(pad, 0, pad, 0), BackColor = Theme.Navy };
        listHost.Controls.Add(_list);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = LogicalToDeviceUnits(68),
            Padding = new Padding(pad, LogicalToDeviceUnits(16), pad - LogicalToDeviceUnits(6), 0),
            BackColor = Theme.Navy
        };
        var cancelButton = new PillButton("Cancel", primary: false) { DialogResult = DialogResult.Cancel };
        cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        _okButton = new PillButton("Mirror this window", primary: true) { Enabled = false };
        _okButton.Click += (_, _) => CommitSelection();
        var refreshButton = new PillButton("Refresh", primary: false);
        refreshButton.Click += (_, _) => RefreshList();
        buttons.Controls.Add(_okButton);
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(refreshButton);

        // Fill first, then edges (WinForms docks in reverse z-order).
        Controls.Add(listHost);
        Controls.Add(buttons);
        Controls.Add(hint);
        Controls.Add(heading);

        AcceptButton = _okButton;
        CancelButton = cancelButton;

        Load += (_, _) => RefreshList();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyDarkChrome(this);
    }

    private void DrawWindowItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        bool sel = (e.State & DrawItemState.Selected) != 0;

        using (var b = new SolidBrush(Theme.Surface))
            g.FillRectangle(b, e.Bounds);
        if (sel)
        {
            var r = RectangleF.Inflate(e.Bounds, -LogicalToDeviceUnits(4), -LogicalToDeviceUnits(3));
            using var p = Theme.RoundedRect(r, LogicalToDeviceUnits(8));
            using var fill = new SolidBrush(Color.FromArgb(70, Theme.Violet));
            using var pen = new Pen(Theme.Violet, LogicalToDeviceUnits(1));
            g.FillPath(fill, p);
            g.DrawPath(pen, p);
        }

        if (_list.Items[e.Index] is not WindowEnumerator.WindowInfo w) return;
        int pad = LogicalToDeviceUnits(16);
        int half = e.Bounds.Height / 2;
        var titleRect = new Rectangle(e.Bounds.X + pad, e.Bounds.Y + LogicalToDeviceUnits(3), e.Bounds.Width - pad * 2, half);
        var procRect = new Rectangle(e.Bounds.X + pad, e.Bounds.Y + half, e.Bounds.Width - pad * 2, half - LogicalToDeviceUnits(3));
        const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis
                                    | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(g, w.Title, _titleFont, titleRect, Theme.OffWhite, flags | TextFormatFlags.Bottom);
        TextRenderer.DrawText(g, w.ProcessName, Font, procRect, Theme.Muted, flags | TextFormatFlags.Top);
    }

    private void RefreshList()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var w in WindowEnumerator.GetCaptureCandidates())
            _list.Items.Add(w);
        _list.EndUpdate();
        _okButton.Enabled = false;
    }

    private void CommitSelection()
    {
        if (_list.SelectedItem is not WindowEnumerator.WindowInfo w) return;
        SelectedWindow = w;
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont.Dispose();
            _headingFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
