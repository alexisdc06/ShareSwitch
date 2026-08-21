using System.Drawing;
using System.Windows.Forms;
using ShareSwitch.Interop;

namespace ShareSwitch;

/// <summary>
/// The proxy window the user shares in Teams/Zoom/Meet. Hosts a live DWM
/// thumbnail of the current source window.
///
/// With auto-fit enabled (default) the window continuously resizes itself to
/// match the source window's shape, so viewers see the content edge-to-edge with
/// no letterbox bars — including while the user drags the source window's edges.
///
/// Closing with the X hides the window instead of destroying it, so an active
/// share is never killed by accident — only tray → Exit really closes it.
/// </summary>
public class ShareWindow : Form
{
    /// <summary>How often the source window is polled for size changes / liveness.</summary>
    private const int SourcePollMs = 120;

    /// <summary>Auto-fit never grows the window past this share of the work area.</summary>
    private const double MaxWorkAreaFraction = 0.92;

    private readonly DwmThumbnail _thumbnail = new();
    private readonly Label _hintLabel;
    private readonly System.Windows.Forms.Timer _sourceWatchdog;

    private IntPtr _sourceHwnd = IntPtr.Zero;
    private Size _lastSourceSize = Size.Empty;
    private bool _clickThrough;
    private bool _userResizing;

    /// <summary>When false (default), the X button hides instead of closing.</summary>
    public bool AllowClose { get; set; }

    public ShareWindow()
    {
        Text = "ShareSwitch — no source selected";
        Icon = AppIcon.Get();
        Size = new Size(1280, 720);
        MinimumSize = new Size(240, 160); // low, so small sources can be matched exactly
        StartPosition = FormStartPosition.Manual;
        Location = new Point(80, 80);
        BackColor = Color.Black; // letterbox bars when auto-fit is off or clamped

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

        // Source windows can be resized or closed at any time and DWM never
        // notifies us, so poll: cheap, and far safer than another global hook.
        _sourceWatchdog = new System.Windows.Forms.Timer { Interval = SourcePollMs };
        _sourceWatchdog.Tick += (_, _) => PollSource();
        _sourceWatchdog.Start();

        Resize += (_, _) => UpdateThumbnailLayout();
        ResizeBegin += (_, _) => _userResizing = true;   // don't fight a manual drag
        ResizeEnd += (_, _) => _userResizing = false;
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
        _lastSourceSize = Size.Empty; // force an auto-fit on the next poll
        _hintLabel.Visible = false;
        Text = $"ShareSwitch → {title}";

        ApplyAutoFit(_thumbnail.GetSourceSize());
        UpdateThumbnailLayout();
        return true;
    }

    public void ClearSource(string? reason = null)
    {
        _thumbnail.Unregister();
        _sourceHwnd = IntPtr.Zero;
        _lastSourceSize = Size.Empty;
        _hintLabel.Text = reason is null ? BuildHintText() : reason + "\n\n" + BuildHintText();
        _hintLabel.Visible = true;
        Text = "ShareSwitch — no source selected";
    }

    /// <summary>
    /// During selection mode the window reports HTTRANSPARENT, so
    /// WindowFromPoint resolves to whatever is behind it — letting the user pick
    /// a window that sits underneath the ShareWindow itself.
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
        // DWM thumbnails are bound to the destination HWND. If WinForms recreates
        // our handle, silently re-register the current source.
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

    /// <summary>Watches the source for death and for size changes.</summary>
    private void PollSource()
    {
        if (_sourceHwnd == IntPtr.Zero) return;

        if (!NativeMethods.IsWindow(_sourceHwnd))
        {
            ClearSource("The source window was closed.");
            return;
        }

        Size src = _thumbnail.GetSourceSize();
        if (src.Width <= 0 || src.Height <= 0) return;
        if (src == _lastSourceSize) return;

        _lastSourceSize = src;
        ApplyAutoFit(src);
        UpdateThumbnailLayout();
    }

    /// <summary>
    /// Resizes this window so its client area matches the source's shape exactly,
    /// scaled down to fit the monitor when the source is larger than the screen.
    /// </summary>
    private void ApplyAutoFit(Size src)
    {
        if (!AppSettings.Current.AutoFitToSource) return;
        if (src.Width <= 0 || src.Height <= 0) return;
        if (_userResizing || WindowState != FormWindowState.Normal) return;

        Rectangle work = Screen.FromControl(this).WorkingArea;
        int maxW = (int)(work.Width * MaxWorkAreaFraction);
        int maxH = (int)(work.Height * MaxWorkAreaFraction);

        // Account for the window chrome, since we're sizing the client area.
        Size chrome = new(Width - ClientSize.Width, Height - ClientSize.Height);
        double scale = Math.Min(1.0, Math.Min(
            (double)(maxW - chrome.Width) / src.Width,
            (double)(maxH - chrome.Height) / src.Height));

        var target = new Size(
            Math.Max(MinimumSize.Width - chrome.Width, (int)Math.Round(src.Width * scale)),
            Math.Max(MinimumSize.Height - chrome.Height, (int)Math.Round(src.Height * scale)));

        if (target == ClientSize) return;

        ClientSize = target;

        // Nudge back on-screen if the new size pushed us off the work area.
        int x = Math.Min(Left, work.Right - Width);
        int y = Math.Min(Top, work.Bottom - Height);
        Location = new Point(Math.Max(work.Left, x), Math.Max(work.Top, y));
    }

    /// <summary>
    /// Places the thumbnail in the client area, preserving aspect ratio.
    /// With auto-fit on this normally fills the window edge-to-edge; the
    /// letterbox path still covers the clamped and auto-fit-off cases.
    /// </summary>
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
        else if (_sourceHwnd != IntPtr.Zero)
            ApplyAutoFit(_thumbnail.GetSourceSize());
    }

    private static string BuildHintText()
    {
        string combo = AppSettings.DescribeModifiers(AppSettings.Current.SelectionModifiers);
        return $"No source selected.\n\nHold {combo} and click any window to mirror it here,\n" +
               "or pick one from the tray menu.\n\nShare this window in Teams / Zoom / Meet.";
    }
}
