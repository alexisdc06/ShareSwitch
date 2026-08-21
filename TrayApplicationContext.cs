using System.Drawing;
using System.Windows.Forms;
using ShareSwitch.Selection;

namespace ShareSwitch;

/// <summary>
/// Application root: owns the tray icon, the ShareWindow and the
/// SelectionController, and tears everything down on exit.
/// </summary>
public class TrayApplicationContext : ApplicationContext
{
    private static readonly (string Label, Keys Modifiers)[] HotkeyChoices =
    {
        ("Ctrl + Alt", Keys.Control | Keys.Alt),
        ("Ctrl + Shift", Keys.Control | Keys.Shift),
        ("Alt + Shift", Keys.Alt | Keys.Shift),
        ("Ctrl + Alt + Shift", Keys.Control | Keys.Alt | Keys.Shift),
    };

    private readonly NotifyIcon _trayIcon;
    private readonly SelectionController _selectionController;
    private readonly ToolStripMenuItem _hotkeyMenu;
    private readonly ToolStripMenuItem _statusItem;
    private ShareWindow? _shareWindow;

    public TrayApplicationContext()
    {
        _hotkeyMenu = BuildHotkeyMenu();

        _statusItem = new ToolStripMenuItem("No source selected") { Enabled = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Show Share Window", null, (_, _) => ShowShareWindow());
        menu.Items.Add("Select Window…", null, (_, _) => ShowWindowPicker());
        menu.Items.Add(_hotkeyMenu);

        var autoFitItem = new ToolStripMenuItem("Auto-fit window to source")
        {
            Checked = AppSettings.Current.AutoFitToSource,
            CheckOnClick = true
        };
        autoFitItem.CheckedChanged += (sender, _) =>
        {
            AppSettings.Current.AutoFitToSource = ((ToolStripMenuItem)sender!).Checked;
            AppSettings.Current.NotifyChanged();
        };
        menu.Items.Add(autoFitItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _trayIcon = new NotifyIcon
        {
            Icon = AppIcon.Get(),
            Visible = true,
            Text = "ShareSwitch — window share proxy",
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => ShowShareWindow();

        _selectionController = new SelectionController(GetOrCreateShareWindow);
        try
        {
            _selectionController.Start();
        }
        catch (Exception ex)
        {
            // The app is still usable through the tray picker without the hotkey.
            _trayIcon.ShowBalloonTip(5000, "ShareSwitch",
                $"Global hotkey unavailable: {ex.Message}\nUse the tray menu to select windows.",
                ToolTipIcon.Warning);
        }

        ShowShareWindow();
    }

    private ShareWindow GetOrCreateShareWindow()
    {
        if (_shareWindow == null || _shareWindow.IsDisposed)
        {
            _shareWindow = new ShareWindow();
            _shareWindow.SourceChanged += (_, _) => UpdateStatus();
            UpdateStatus();
        }
        return _shareWindow;
    }

    private void ShowShareWindow()
    {
        var w = GetOrCreateShareWindow();
        w.Show();
        if (w.WindowState == FormWindowState.Minimized)
            w.WindowState = FormWindowState.Normal;
        w.Activate();
    }

    /// <summary>Keeps the tray menu header and tooltip showing the live source.</summary>
    private void UpdateStatus()
    {
        string status = _shareWindow is { IsDisposed: false }
            ? _shareWindow.StatusText
            : "No source selected";

        _statusItem.Text = status;

        // NotifyIcon.Text is capped at 63 characters by Windows.
        string tip = $"ShareSwitch — {status}";
        _trayIcon.Text = tip.Length > 63 ? tip[..60] + "…" : tip;
    }

    private void ShowWindowPicker()
    {
        using var picker = new WindowPickerForm();
        if (picker.ShowDialog() == DialogResult.OK && picker.SelectedWindow is { } w)
        {
            var share = GetOrCreateShareWindow();
            share.Show();
            share.SetSource(w.Hwnd, w.Title);
        }
    }

    private ToolStripMenuItem BuildHotkeyMenu()
    {
        var root = new ToolStripMenuItem("Selection Hotkey");

        foreach (var (label, modifiers) in HotkeyChoices)
        {
            var item = new ToolStripMenuItem(label)
            {
                Checked = AppSettings.Current.SelectionModifiers == modifiers,
                Tag = modifiers
            };
            item.Click += (sender, _) =>
            {
                AppSettings.Current.SelectionModifiers = (Keys)((ToolStripMenuItem)sender!).Tag!;
                AppSettings.Current.NotifyChanged();
                foreach (ToolStripMenuItem mi in root.DropDownItems)
                    mi.Checked = (Keys)mi.Tag! == AppSettings.Current.SelectionModifiers;
            };
            root.DropDownItems.Add(item);
        }

        return root;
    }

    private void ExitApplication()
    {
        _trayIcon.Visible = false;
        _selectionController.Dispose(); // uninstalls hooks before UI teardown

        if (_shareWindow is { IsDisposed: false })
        {
            _shareWindow.AllowClose = true;
            _shareWindow.Close();
        }

        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _selectionController.Dispose(); // idempotent
            _trayIcon.Dispose();
            _shareWindow?.Dispose();
        }
        base.Dispose(disposing);
    }
}
