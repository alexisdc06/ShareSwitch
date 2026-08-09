using System.Drawing;
using System.Windows.Forms;
using ShareSwitch.Interop;

namespace ShareSwitch;

/// <summary>
/// The proxy window the user shares in Teams/Zoom/Meet. Hosts a live DWM
/// thumbnail of the current source window, letterboxed to preserve aspect ratio.
///
/// Closing with the X hides the window instead of destroying it, so an active
/// share is never killed by accident — only the tray Exit really closes it.
/// </summary>
public class ShareWindow : Form
{
    private readonly DwmThumbnail _thumbnail = new();
    private readonly Label _hintLabel;
    private readonly System.Windows.Forms.Timer _sourceWatchdog;

    private IntPtr _sourceHwnd = IntPtr.Zero;
    private string _sourceTitle = string.Empty;
    private bool _clickThrough;

    /// <summary>When false (default), the X button hides instead of closing.</summary>
    public bool AllowClose { get; set; }

    public ShareWindow()
    {
        Text = "ShareSwitch — no source selected";
        Icon = AppIcon.Get();
        Size = new Size(1280, 720);
        MinimumSize = new Size(640, 360);
        StartPosition = FormStartPosition.Manual;
        Location = new Point(80, 80);
        BackColor = Color.Black; // letterbox bars

        _hintLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 12f),
            Text = BuildHintText()
        };
        Controls.Add(_hintLabel);
        AppSettings.Current.Changed += OnSettingsChanged;

        // Source windows can be closed at any time; DWM does not notify us,
        // so poll cheaply and clear the dead thumbnail.
        _sourceWatchdog = new System.Windows.Forms.Timer { Interval = 1000 };
        _sourceWatchdog.Tick += (_, _) => CheckSourceAlive();
        _sourceWatchdog.Start();

        Resize += (_, _) => UpdateThumbnailLayout();
    }

    public IntPtr SourceHwnd => _sourceHwnd;

    /// <summary>Switches the live thumbnail to a new source window.</summary>
    /// <returns>false if DWM refused the window (protected, destroyed, …).</returns>
    public bool SetSource(IntPtr hwnd, string title)
    {
        try
        {
            _thumbnail.Register(Handle, hwnd);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Could not capture \"{title}\":\n{ex.Message}",
                "ShareSwitch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        _sourceHwnd = hwnd;
        _sourceTitle = title;
        _hintLabel.Visible = false;
        Text = $"ShareSwitch → {title}";
        UpdateThumbnailLayout();
        return true;
    }

    public void ClearSource(string? reason = null)
    {
        _thumbnail.Unregister();
        _sourceHwnd = IntPtr.Zero;
        _sourceTitle = string.Empty;
        _hintLabel.Text = reason is null ? BuildHintText() : reason + "\n\n" + BuildHintText();
        _hintLabel.Visible = true;
        Text = "ShareSwitch — no source selected";
    }

    /// <summary>
    /// During selection mode the window reports HTTRANSPARENT, so
    /// WindowFromPoint resolves to whatever is behind it — letting the user
    /// pick a window that sits underneath the ShareWindow itself.
    /// (Deliberately not WS_EX_LAYERED/WS_EX_TRANSPARENT: layered destination
    /// windows can break DWM thumbnail rendering.)
    /// </summary>
    public void SetClickThrough(bool enabled) => _clickThrough = enabled;

    protected override void WndProc(ref Message m)
    {
        if (_clickThrough && m.Msg == NativeMethods.WM_NCHITTEST)
        {
            m.Result = NativeMethods.HTTRANSPARENT;
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // DWM thumbnails are bound to the destination HWND. If WinForms
        // recreates our handle, silently re-register the current source.
        if (_sourceHwnd != IntPtr.Zero && NativeMethods.IsWindow(_sourceHwnd))
        {
            try
            {
                _thumbnail.Register(Handle, _sourceHwnd);
                UpdateThumbnailLayout();
            }
            catch
            {
                ClearSource("The previous source window could no longer be captured.");
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!AllowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide(); // keep the thumbnail registration alive
            return;
        }
        AppSettings.Current.Changed -= OnSettingsChanged;
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sourceWatchdog.Dispose();
            _thumbnail.Dispose();
        }
        base.Dispose(disposing);
    }

    private void CheckSourceAlive()
    {
        if (_sourceHwnd != IntPtr.Zero && !NativeMethods.IsWindow(_sourceHwnd))
            ClearSource("The source window was closed.");
    }

    /// <summary>Letterboxes the thumbnail into the client area, preserving aspect ratio.</summary>
    private void UpdateThumbnailLayout()
    {
        if (!_thumbnail.IsRegistered) return;

        Rectangle client = ClientRectangle;
        Size src = _thumbnail.GetSourceSize();

        Rectangle dest = client;
        if (src.Width > 0 && src.Height > 0 && client.Width > 0 && client.Height > 0)
        {
            double scale = Math.Min((double)client.Width / src.Width,
                                    (double)client.Height / src.Height);
            int w = Math.Max(1, (int)(src.Width * scale));
            int h = Math.Max(1, (int)(src.Height * scale));
            dest = new Rectangle((client.Width - w) / 2, (client.Height - h) / 2, w, h);
        }

        _thumbnail.SetDestinationRect(dest);
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_hintLabel.Visible)
            _hintLabel.Text = BuildHintText();
    }

    private static string BuildHintText()
    {
        string combo = AppSettings.DescribeModifiers(AppSettings.Current.SelectionModifiers);
        return $"No source selected.\n\nHold {combo} and click any window to mirror it here,\n" +
               "or pick one from the tray menu.\n\nShare this window in Teams / Zoom / Meet.";
    }
}
