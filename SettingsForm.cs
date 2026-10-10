using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using ShareSwitch.Selection;

namespace ShareSwitch;

/// <summary>
/// Settings and About, opened from the tray — in the brand's dark theme.
///
/// Deliberately a separate window: anything placed inside ShareWindow is part of
/// what a screen share captures, so app UI must never live there.
/// </summary>
public sealed class SettingsForm : Form
{
    private static readonly (string Label, Keys Modifiers)[] HotkeyChoices =
    {
        ("Ctrl + Alt", Keys.Control | Keys.Alt),
        ("Ctrl + Shift", Keys.Control | Keys.Shift),
        ("Alt + Shift", Keys.Alt | Keys.Shift),
        ("Ctrl + Alt + Shift", Keys.Control | Keys.Alt | Keys.Shift),
    };

    private readonly WindowSlots _slots;
    private readonly ComboBox _hotkeyCombo = new();
    private readonly ToggleSwitch _startupCheck = new("Start ShareSwitch when Windows starts");
    private readonly ToggleSwitch _minimizedCheck = new("Start minimized to the tray");
    private readonly ToggleSwitch _autoFitCheck = new("Resize the share window to fit what it mirrors");
    private readonly ToggleSwitch _slotsCheck = new("Number shortcuts (jump to an assigned window)");
    private readonly ToggleSwitch _cycleCheck = new("Arrow shortcuts (step through open windows)");
    private readonly ListBox _slotList = new();
    private readonly Label _shortcutHelp = new();
    private readonly Font _sectionFont = new("Segoe UI Semibold", 8.5f);
    private readonly Font _semibold = new("Segoe UI Semibold", 9.75f);
    private bool _loading;
    private int _flashSlot = -1;
    private readonly System.Windows.Forms.Timer _flashTimer = new() { Interval = 1600 };

    public SettingsForm(WindowSlots slots)
    {
        _slots = slots;

        Text = "ShareSwitch — Settings";
        Icon = AppIcon.Get();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9.75f);
        BackColor = Theme.Navy;
        ForeColor = Theme.OffWhite;
        ClientSize = LogicalToDeviceUnits(new Size(800, 600));

        BuildLayout();
        LoadValues();

        // Live: saving a window with Ctrl+Alt+Shift updates this list immediately,
        // and the changed row glows green for a moment.
        _slots.Changed += OnSlotsChanged;
        _flashTimer.Tick += (_, _) => { _flashTimer.Stop(); _flashSlot = -1; _slotList.Invalidate(); };
    }

    private void OnSlotsChanged(int index)
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => OnSlotsChanged(index)));
            return;
        }
        RefreshSlotList();
        if (index >= 0 && index < _slotList.Items.Count && !string.IsNullOrEmpty(_slotList.Items[index] as string))
        {
            _flashSlot = index;
            _flashTimer.Stop();
            _flashTimer.Start();
            _slotList.Invalidate();
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyDarkChrome(this);
    }

    private void BuildLayout()
    {
        // ── body: two columns ────────────────────────────────────
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Navy,
            Padding = new Padding(LogicalToDeviceUnits(26), LogicalToDeviceUnits(8), LogicalToDeviceUnits(26), 0)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));

        var left = Column();
        var right = Column();
        body.Controls.Add(left, 0, 0);
        body.Controls.Add(right, 1, 0);

        // General
        left.Controls.Add(new SectionLabel("General", _sectionFont));
        _startupCheck.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            AppSettings.Current.LaunchAtStartup = _startupCheck.Checked;
            // Re-read: on a policy-locked machine the write may be refused.
            _startupCheck.Checked = AppSettings.Current.LaunchAtStartup;
        };
        _minimizedCheck.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            AppSettings.Current.StartMinimizedToTray = _minimizedCheck.Checked;
            AppSettings.Current.NotifyChanged();
        };
        _autoFitCheck.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            AppSettings.Current.AutoFitToSource = _autoFitCheck.Checked;
            AppSettings.Current.NotifyChanged();
        };
        left.Controls.Add(_startupCheck);
        left.Controls.Add(_minimizedCheck);
        left.Controls.Add(_autoFitCheck);

        var hotkeyRow = new FlowLayoutPanel
        {
            AutoSize = true, WrapContents = false, BackColor = Theme.Navy,
            Margin = new Padding(0, LogicalToDeviceUnits(8), 0, 0)
        };
        hotkeyRow.Controls.Add(new Label
        {
            Text = "Selection hotkey", AutoSize = true, ForeColor = Theme.SoftText,
            Margin = new Padding(0, LogicalToDeviceUnits(6), LogicalToDeviceUnits(12), 0)
        });
        _hotkeyCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _hotkeyCombo.DrawMode = DrawMode.OwnerDrawFixed;
        _hotkeyCombo.FlatStyle = FlatStyle.Flat;
        _hotkeyCombo.BackColor = Theme.Surface;
        _hotkeyCombo.ForeColor = Theme.OffWhite;
        _hotkeyCombo.ItemHeight = LogicalToDeviceUnits(24);
        _hotkeyCombo.Width = LogicalToDeviceUnits(180);
        _hotkeyCombo.DrawItem += DrawComboItem;
        foreach (var (label, _) in HotkeyChoices) _hotkeyCombo.Items.Add(label);
        _hotkeyCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _hotkeyCombo.SelectedIndex < 0) return;
            AppSettings.Current.SelectionModifiers = HotkeyChoices[_hotkeyCombo.SelectedIndex].Modifiers;
            AppSettings.Current.NotifyChanged();
            UpdateShortcutHelp();
        };
        hotkeyRow.Controls.Add(_hotkeyCombo);
        left.Controls.Add(hotkeyRow);

        // Fast switching
        left.Controls.Add(new SectionLabel("Fast switching", _sectionFont));
        _slotsCheck.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            AppSettings.Current.EnableSlotShortcuts = _slotsCheck.Checked;
            AppSettings.Current.NotifyChanged();
        };
        _cycleCheck.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            AppSettings.Current.EnableCycleShortcuts = _cycleCheck.Checked;
            AppSettings.Current.NotifyChanged();
        };
        left.Controls.Add(_slotsCheck);
        left.Controls.Add(_cycleCheck);
        _shortcutHelp.AutoSize = true;
        _shortcutHelp.ForeColor = Theme.Muted;
        _shortcutHelp.MaximumSize = new Size(LogicalToDeviceUnits(400), 0);
        _shortcutHelp.Margin = new Padding(0, LogicalToDeviceUnits(8), 0, 0);
        left.Controls.Add(_shortcutHelp);

        // Assigned windows
        right.Controls.Add(new SectionLabel("Assigned windows · this session", _sectionFont));
        _slotList.DrawMode = DrawMode.OwnerDrawFixed;
        _slotList.ItemHeight = LogicalToDeviceUnits(30);
        _slotList.BorderStyle = BorderStyle.None;
        _slotList.BackColor = Theme.Surface;
        _slotList.ForeColor = Theme.OffWhite;
        _slotList.IntegralHeight = false;
        _slotList.Size = new Size(LogicalToDeviceUnits(326), LogicalToDeviceUnits(30) * WindowSlots.Count + 4);
        _slotList.DrawItem += DrawSlotItem;
        right.Controls.Add(_slotList);

        var clearButton = new PillButton("Clear selected", primary: false)
        {
            Margin = new Padding(0, LogicalToDeviceUnits(10), 0, 0)
        };
        clearButton.Click += (_, _) =>
        {
            if (_slotList.SelectedIndex < 0) return;
            _slots.Clear(_slotList.SelectedIndex);
            RefreshSlotList();
        };
        right.Controls.Add(clearButton);

        // ── footer ───────────────────────────────────────────────
        var footer = new Panel { Dock = DockStyle.Bottom, Height = LogicalToDeviceUnits(64), BackColor = Theme.Navy };
        var closeButton = new PillButton("Close", primary: true);
        closeButton.Click += (_, _) => Close();
        closeButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        footer.Controls.Add(closeButton);
        footer.Layout += (_, _) =>
            closeButton.Location = new Point(footer.Width - closeButton.Width - LogicalToDeviceUnits(26),
                                             (footer.Height - closeButton.Height) / 2);

        var aboutLink = new LinkLabel
        {
            Text = "MIT licensed · Open source on GitHub",
            AutoSize = true,
            LinkColor = Theme.Violet,
            ActiveLinkColor = Theme.OffWhite,
            VisitedLinkColor = Theme.Violet,
            LinkBehavior = LinkBehavior.HoverUnderline,
            BackColor = Theme.Navy,
            Location = new Point(LogicalToDeviceUnits(26), LogicalToDeviceUnits(22))
        };
        aboutLink.LinkClicked += (_, _) => OpenProjectPage();
        footer.Controls.Add(aboutLink);

        var header = new BrandHeader(VersionString) { Dock = DockStyle.Top, Height = LogicalToDeviceUnits(112) };

        // Fill first, then edges (WinForms docks in reverse z-order).
        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(header);

        AcceptButton = closeButton;
        CancelButton = closeButton;
    }

    private FlowLayoutPanel Column() => new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = false,
        BackColor = Theme.Navy,
        Margin = new Padding(0)
    };

    private void DrawComboItem(object? sender, DrawItemEventArgs e)
    {
        bool sel = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
        using (var b = new SolidBrush(sel ? Theme.Violet : Theme.Surface))
            e.Graphics.FillRectangle(b, e.Bounds);
        if ((e.State & DrawItemState.ComboBoxEdit) != 0 && (e.State & DrawItemState.Focus) != 0)
        {
            using var focusPen = new Pen(Theme.Violet);
            e.Graphics.DrawRectangle(focusPen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
        }
        if (e.Index < 0) return;
        var r = new Rectangle(e.Bounds.X + LogicalToDeviceUnits(8), e.Bounds.Y, e.Bounds.Width - LogicalToDeviceUnits(8), e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, _hotkeyCombo.Items[e.Index]?.ToString() ?? string.Empty, e.Font ?? Font, r,
            Theme.OffWhite, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
    }

    private void DrawSlotItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        bool sel = (e.State & DrawItemState.Selected) != 0;
        bool flash = e.Index == _flashSlot;
        using (var b = new SolidBrush(flash ? Color.FromArgb(60, Theme.Fluo) : sel ? Theme.SurfaceHover : Theme.Surface))
            g.FillRectangle(b, e.Bounds);

        string? title = _slotList.Items[e.Index] as string;
        bool assigned = !string.IsNullOrEmpty(title);

        int pad = LogicalToDeviceUnits(10);
        int badge = e.Bounds.Height - LogicalToDeviceUnits(10);
        var br = new RectangleF(e.Bounds.X + pad, e.Bounds.Y + (e.Bounds.Height - badge) / 2f, badge, badge);
        using (var p = Theme.RoundedRect(br, LogicalToDeviceUnits(5)))
        using (var bb = new SolidBrush(flash ? Theme.Fluo : assigned ? Theme.Violet : Theme.Line))
            g.FillPath(bb, p);
        TextRenderer.DrawText(g, (e.Index + 1).ToString(), _semibold, Rectangle.Round(br),
            flash ? Theme.Navy : Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        var tr = new Rectangle((int)br.Right + pad, e.Bounds.Y, e.Bounds.Right - (int)br.Right - pad * 2, e.Bounds.Height);
        TextRenderer.DrawText(g, assigned ? title : "Not assigned", Font, tr, assigned ? Theme.OffWhite : Theme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        if (sel)
        {
            using var pen = new Pen(Theme.Violet, LogicalToDeviceUnits(2));
            g.DrawLine(pen, e.Bounds.X + 1, e.Bounds.Y + 2, e.Bounds.X + 1, e.Bounds.Bottom - 2);
        }
    }

    private void LoadValues()
    {
        _loading = true;
        _startupCheck.Checked = AppSettings.Current.LaunchAtStartup;
        _minimizedCheck.Checked = AppSettings.Current.StartMinimizedToTray;
        _autoFitCheck.Checked = AppSettings.Current.AutoFitToSource;
        _slotsCheck.Checked = AppSettings.Current.EnableSlotShortcuts;
        _cycleCheck.Checked = AppSettings.Current.EnableCycleShortcuts;

        int index = Array.FindIndex(HotkeyChoices, c => c.Modifiers == AppSettings.Current.SelectionModifiers);
        _hotkeyCombo.SelectedIndex = index >= 0 ? index : 0;
        _loading = false;

        UpdateShortcutHelp();
        RefreshSlotList();
    }

    private void UpdateShortcutHelp()
    {
        string combo = AppSettings.DescribeModifiers(AppSettings.Current.SelectionModifiers);
        _shortcutHelp.Text =
            $"Hold {combo} and click a window to mirror it.\n" +
            $"{combo} + Shift + click — save it to the next free number.\n" +
            $"{combo} + Shift + 1…9 — save it to that number.\n" +
            $"{combo} + 1…9 — switch to that window.\n" +
            $"{combo} + ← / → — step through open windows.";
    }

    private void RefreshSlotList()
    {
        int keep = _slotList.SelectedIndex;
        _slotList.BeginUpdate();
        _slotList.Items.Clear();
        foreach (string? title in _slots.Describe())
            _slotList.Items.Add(title ?? string.Empty);
        _slotList.EndUpdate();
        if (keep >= 0 && keep < _slotList.Items.Count) _slotList.SelectedIndex = keep;
    }

    private static string VersionString
        => Assembly.GetExecutingAssembly().GetName().Version is { } v
            ? $"Version {v.Major}.{v.Minor}.{v.Build}"
            : string.Empty;

    private static void OpenProjectPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/alexisdc06/ShareSwitch") { UseShellExecute = true });
        }
        catch
        {
            // No default browser configured — nothing useful to do.
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _slots.Changed -= OnSlotsChanged;
            _flashTimer.Dispose();
            _sectionFont.Dispose();
            _semibold.Dispose();
        }
        base.Dispose(disposing);
    }
}
