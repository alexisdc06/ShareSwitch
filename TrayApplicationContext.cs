using System.Windows.Forms;
using ShareSwitch.Selection;

namespace ShareSwitch;

/// <summary>
/// Application root: owns the tray icon, the ShareWindow and the
/// SelectionController, and tears everything down on exit.
///
/// The tray menu is the app's only chrome by design — ShareWindow's content is
/// exactly what a screen share captures, so no application UI may live there.
/// </summary>
public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly SelectionController _selectionController;
    private readonly ToolStripMenuItem _statusItem;
    private ShareWindow? _shareWindow;
    private SettingsForm? _settingsForm;

    public TrayApplicationContext()
    {
        _statusItem = new ToolStripMenuItem("No source selected") { Enabled = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Show Share Window", null, (_, _) => ShowShareWindow());
        menu.Items.Add("Select Window…", null, (_, _) => ShowWindowPicker());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings…", null, (_, _) => ShowSettings());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _trayIcon = new NotifyIcon
        {
            Icon = AppIcon.Get(),
            Visible = true,
            Text = "ShareSwitch",
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

        if (AppSettings.Current.StartMinimizedToTray)
        {
            // Create it (so a hotkey switch works immediately) but leave it hidden,
            // and say so — an app that starts invisibly is otherwise unfindable.
            GetOrCreateShareWindow();
            _trayIcon.ShowBalloonTip(4000, "ShareSwitch",
                "Running in the tray. Double-click the icon to open the share window.",
                ToolTipIcon.Info);
        }
        else
        {
            ShowShareWindow();
        }
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

    private void ShowSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(_selectionController.Slots);
        _settingsForm.FormClosed += (_, _) =>
        {
            AppSettings.Save(); // persist as soon as the user is done fiddling
            _settingsForm = null;
        };
        _settingsForm.Show();
    }

    private void ExitApplication()
    {
        _trayIcon.Visible = false;
        _selectionController.Dispose(); // uninstalls hooks before UI teardown

        _settingsForm?.Close();

        if (_shareWindow is { IsDisposed: false })
        {
            _shareWindow.AllowClose = true;
            _shareWindow.Close();
        }

        AppSettings.Save();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _selectionController.Dispose(); // idempotent
            _trayIcon.Dispose();
            _settingsForm?.Dispose();
            _shareWindow?.Dispose();
        }
        base.Dispose(disposing);
    }
}
