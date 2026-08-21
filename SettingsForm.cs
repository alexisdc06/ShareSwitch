using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using ShareSwitch.Selection;

namespace ShareSwitch;

/// <summary>
/// Settings and About, opened from the tray.
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
    private readonly CheckBox _startupCheck = new();
    private readonly CheckBox _autoFitCheck = new();
    private readonly CheckBox _slotsCheck = new();
    private readonly CheckBox _cycleCheck = new();
    private readonly ListView _slotList = new();
    private readonly Label _shortcutHelp = new();

    public SettingsForm(WindowSlots slots)
    {
        _slots = slots;

        Text = "ShareSwitch — Settings";
        Icon = AppIcon.Get();
        ClientSize = new Size(520, 560);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9.5f);

        BuildLayout();
        LoadValues();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // ── General ───────────────────────────────────────────────
        var (generalBox, general) = NewGroup("General");

        _startupCheck.Text = "Start ShareSwitch when Windows starts";
        _startupCheck.AutoSize = true;
        _startupCheck.CheckedChanged += (_, _) =>
        {
            AppSettings.Current.LaunchAtStartup = _startupCheck.Checked;
            // Re-read: on a policy-locked machine the write may be refused.
            _startupCheck.Checked = AppSettings.Current.LaunchAtStartup;
        };
        general.Controls.Add(_startupCheck);

        _autoFitCheck.Text = "Resize the share window to match the mirrored window";
        _autoFitCheck.AutoSize = true;
        _autoFitCheck.CheckedChanged += (_, _) =>
        {
            AppSettings.Current.AutoFitToSource = _autoFitCheck.Checked;
            AppSettings.Current.NotifyChanged();
        };
        general.Controls.Add(_autoFitCheck);

        var hotkeyRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
        hotkeyRow.Controls.Add(new Label { Text = "Selection hotkey:", AutoSize = true, Margin = new Padding(0, 6, 8, 0) });
        _hotkeyCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _hotkeyCombo.Width = 160;
        foreach (var (label, _) in HotkeyChoices) _hotkeyCombo.Items.Add(label);
        _hotkeyCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_hotkeyCombo.SelectedIndex < 0) return;
            AppSettings.Current.SelectionModifiers = HotkeyChoices[_hotkeyCombo.SelectedIndex].Modifiers;
            AppSettings.Current.NotifyChanged();
            UpdateShortcutHelp();
        };
        hotkeyRow.Controls.Add(_hotkeyCombo);
        general.Controls.Add(hotkeyRow);

        root.Controls.Add(generalBox);

        // ── Shortcuts ─────────────────────────────────────────────
        var (shortcutsBox, shortcuts) = NewGroup("Fast switching");

        _slotsCheck.Text = "Number shortcuts (switch to a window you assigned)";
        _slotsCheck.AutoSize = true;
        _slotsCheck.CheckedChanged += (_, _) =>
        {
            AppSettings.Current.EnableSlotShortcuts = _slotsCheck.Checked;
            AppSettings.Current.NotifyChanged();
        };
        shortcuts.Controls.Add(_slotsCheck);

        _cycleCheck.Text = "Arrow shortcuts (step through open windows)";
        _cycleCheck.AutoSize = true;
        _cycleCheck.CheckedChanged += (_, _) =>
        {
            AppSettings.Current.EnableCycleShortcuts = _cycleCheck.Checked;
            AppSettings.Current.NotifyChanged();
        };
        shortcuts.Controls.Add(_cycleCheck);

        _shortcutHelp.AutoSize = true;
        _shortcutHelp.ForeColor = SystemColors.GrayText;
        _shortcutHelp.Margin = new Padding(0, 6, 0, 0);
        shortcuts.Controls.Add(_shortcutHelp);

        root.Controls.Add(shortcutsBox);

        // ── Assigned windows ──────────────────────────────────────
        var slotsGroup = new GroupBox
        {
            Text = "Assigned windows (this session)",
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        _slotList.Dock = DockStyle.Fill;
        _slotList.View = View.Details;
        _slotList.FullRowSelect = true;
        _slotList.MultiSelect = false;
        _slotList.Columns.Add("Key", 60);
        _slotList.Columns.Add("Window", 400);

        var clearButton = new Button { Text = "Clear selected", Dock = DockStyle.Bottom, Height = 28, Margin = new Padding(0, 6, 0, 0) };
        clearButton.Click += (_, _) =>
        {
            if (_slotList.SelectedItems.Count == 0) return;
            _slots.Clear((int)_slotList.SelectedItems[0].Tag!);
            RefreshSlotList();
        };

        slotsGroup.Controls.Add(_slotList);
        slotsGroup.Controls.Add(clearButton);
        root.Controls.Add(slotsGroup);

        // ── Footer ────────────────────────────────────────────────
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };

        var closeButton = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.OK };
        var aboutLink = new LinkLabel
        {
            Text = $"ShareSwitch {VersionString} — MIT licensed — github.com/alexisdc06/ShareSwitch",
            AutoSize = true,
            Margin = new Padding(0, 8, 12, 0)
        };
        aboutLink.LinkClicked += (_, _) => OpenProjectPage();

        footer.Controls.Add(closeButton);
        footer.Controls.Add(aboutLink);
        root.Controls.Add(footer);

        AcceptButton = closeButton;
        CancelButton = closeButton;
        Controls.Add(root);
    }

    /// <summary>
    /// Creates a titled group and returns both the box (to add to the layout)
    /// and the inner stack panel (to add the controls to).
    /// </summary>
    private static (GroupBox Box, FlowLayoutPanel Inner) NewGroup(string title)
    {
        var inner = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        var box = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 6, 10, 10),
            Margin = new Padding(0, 0, 0, 12)
        };
        box.Controls.Add(inner);

        return (box, inner);
    }

    private void LoadValues()
    {
        _startupCheck.Checked = AppSettings.Current.LaunchAtStartup;
        _autoFitCheck.Checked = AppSettings.Current.AutoFitToSource;
        _slotsCheck.Checked = AppSettings.Current.EnableSlotShortcuts;
        _cycleCheck.Checked = AppSettings.Current.EnableCycleShortcuts;

        int index = Array.FindIndex(HotkeyChoices,
            c => c.Modifiers == AppSettings.Current.SelectionModifiers);
        _hotkeyCombo.SelectedIndex = index >= 0 ? index : 0;

        UpdateShortcutHelp();
        RefreshSlotList();
    }

    private void UpdateShortcutHelp()
    {
        string combo = AppSettings.DescribeModifiers(AppSettings.Current.SelectionModifiers);
        _shortcutHelp.Text =
            $"Hold {combo} and click a window to mirror it.\n" +
            $"{combo} + Shift + 1…9  assign the window under the cursor to that number.\n" +
            $"{combo} + 1…9  switch to that window.\n" +
            $"{combo} + ← / →  step through open windows.";
    }

    private void RefreshSlotList()
    {
        _slotList.BeginUpdate();
        _slotList.Items.Clear();

        var described = _slots.Describe();
        for (int i = 0; i < described.Count; i++)
        {
            var item = new ListViewItem((i + 1).ToString()) { Tag = i };
            item.SubItems.Add(described[i] ?? "— not assigned —");
            if (described[i] is null) item.ForeColor = SystemColors.GrayText;
            _slotList.Items.Add(item);
        }

        _slotList.EndUpdate();
    }

    private static string VersionString
        => Assembly.GetExecutingAssembly().GetName().Version is { } v
            ? $"v{v.Major}.{v.Minor}.{v.Build}"
            : string.Empty;

    private static void OpenProjectPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/alexisdc06/ShareSwitch")
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // No default browser configured — nothing useful to do.
        }
    }
}
