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

    /// <summary>Accent used to mark this window as "the ShareSwitch one".</summary>
    private static readonly Color AccentColor = Color.FromArgb(0, 170, 255);

    /// <summary>
    /// Thickness of the fallback in-content frame drawn on Windows 10, where the
    /// DWM chrome-tinting attributes don't exist. Viewers <i>do</i> see this one,
    /// which is why it stays thin and is only used when chrome tinting fails.
    /// </summary>
    private const int FallbackBorderPx = 3;

    private readonly DwmThumbnail _thumbnail = new();
    private readonly System.Windows.Forms.Timer _sourceWatchdog;
    private string? _noticeMessage;

    private IntPtr _sourceHwnd = IntPtr.Zero;
    private string _sourceTitle = string.Empty;
    private Size _lastSourceSize = Size.Empty;
    private bool _clickThrough;
    private bool _userResizing;
    private bool _useFallbackBorder;

    /// <summary>Raised whenever the mirrored source changes (or is cleared).</summary>
    public event EventHandler? SourceChanged;

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

        // Painted directly rather than using child controls: the backdrop needs
        // to sit behind the text, and WinForms child controls don't composite
        // over a custom-painted background cleanly.
        SetStyle(ControlStyles.AllPaintingInWmPaint
               | ControlStyles.UserPaint
               | ControlStyles.OptimizedDoubleBuffer, true);

        AppSettings.Current.Changed += OnSettingsChanged;

        // Source windows can be resized or closed at any time and DWM never
        // notifies us, so poll: cheap, and far safer than another global hook.
        _sourceWatchdog = new System.Windows.Forms.Timer { Interval = SourcePollMs };
        _sourceWatchdog.Tick += (_, _) => PollSource();
        _sourceWatchdog.Start();

        Resize += (_, _) =>
        {
            UpdateThumbnailLayout();
            Invalidate(); // re-centre the title card / redraw the backdrop
        };
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
        _sourceTitle = title;
        _lastSourceSize = Size.Empty; // force an auto-fit on the next poll
        _noticeMessage = null;
        Text = $"● ShareSwitch — mirroring: {title}";

        ApplyAccent();
        ApplyAutoFit(_thumbnail.GetSourceSize());
        UpdateThumbnailLayout();
        Invalidate(); // clear the empty-state title card
        SourceChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void ClearSource(string? reason = null)
    {
        _thumbnail.Unregister();
        _sourceHwnd = IntPtr.Zero;
        _sourceTitle = string.Empty;
        _lastSourceSize = Size.Empty;
        _noticeMessage = reason;
        Text = "ShareSwitch — no source selected";

        ApplyAccent();
        Invalidate();
        SourceChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Human-readable status for the tray menu / tooltip.</summary>
    public string StatusText => _sourceHwnd == IntPtr.Zero
        ? "No source selected"
        : $"Mirroring: {_sourceTitle}";

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

    /// <summary>
    /// Marks this window as "the ShareSwitch one" so it can't be confused with
    /// the app it's mirroring — the two are otherwise pixel-identical.
    ///
    /// Uses the Windows 11 DWM chrome attributes (border + title bar + caption
    /// text), which live in the <b>non-client area</b> and are therefore not
    /// part of what a video call captures: your viewers never see the accent.
    /// On Windows 10 those attributes don't exist, so we fall back to a thin
    /// frame drawn inside the content — visible to viewers, hence kept subtle.
    /// </summary>
    private void ApplyAccent()
    {
        bool active = _sourceHwnd != IntPtr.Zero;

        bool chromeApplied = NativeMethods.TrySetWindowChrome(
            Handle,
            border: AccentColor,
            caption: active ? AccentColor : Color.FromArgb(32, 38, 46),
            text: active ? Color.White : Color.Gainsboro);

        bool fallback = !chromeApplied && active;
        if (fallback != _useFallbackBorder)
        {
            _useFallbackBorder = fallback;
            ApplyAutoFit(_thumbnail.GetSourceSize());
            UpdateThumbnailLayout();
            Invalidate();
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // The backdrop shows through wherever the DWM thumbnail isn't drawn:
        // the whole window before a source is picked, and the letterbox bars
        // when auto-fit is off.
        BackgroundRenderer.Paint(e.Graphics, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (_sourceHwnd == IntPtr.Zero)
            PaintEmptyState(e.Graphics);

        if (_useFallbackBorder)
        {
            using var pen = new Pen(AccentColor, FallbackBorderPx);
            int half = FallbackBorderPx / 2;
            e.Graphics.DrawRectangle(pen, half, half,
                ClientSize.Width - FallbackBorderPx, ClientSize.Height - FallbackBorderPx);
        }
    }

    /// <summary>
    /// The "nothing selected yet" screen. This is what a call sees if the user
    /// shares before picking a source, so it reads as a deliberate title card
    /// rather than an error.
    /// </summary>
    private void PaintEmptyState(Graphics g)
    {
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        Rectangle client = ClientRectangle;
        string combo = AppSettings.DescribeModifiers(AppSettings.Current.SelectionModifiers);

        // Scrim keeps the text readable over any custom artwork.
        using (var scrim = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
            g.FillRectangle(scrim, client);

        using var titleFont = new Font("Segoe UI", 26f, FontStyle.Bold);
        using var bodyFont = new Font("Segoe UI", 12f);
        using var mutedFont = new Font("Segoe UI", 10.5f);
        using var titleBrush = new SolidBrush(Color.White);
        using var bodyBrush = new SolidBrush(Color.FromArgb(225, 232, 240));
        using var mutedBrush = new SolidBrush(Color.FromArgb(150, 162, 178));
        using var accentBrush = new SolidBrush(AccentColor);

        var center = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        float y = client.Height / 2f - 86;

        g.DrawString("ShareSwitch", titleFont, titleBrush,
            new RectangleF(0, y, client.Width, 44), center);
        y += 52;

        g.DrawString("Share this window in Teams, Zoom or Meet.", bodyFont, bodyBrush,
            new RectangleF(0, y, client.Width, 26), center);
        y += 30;

        g.DrawString($"Then hold {combo} and click any window to mirror it here.",
            bodyFont, accentBrush, new RectangleF(0, y, client.Width, 26), center);
        y += 42;

        g.DrawString("Settings and shortcuts live in the tray icon, near the clock.",
            mutedFont, mutedBrush, new RectangleF(0, y, client.Width, 24), center);

        if (_noticeMessage is { Length: > 0 })
        {
            y += 34;
            g.DrawString(_noticeMessage, mutedFont, mutedBrush,
                new RectangleF(0, y, client.Width, 24), center);
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyAccent(); // chrome attributes are per-HWND, so re-apply after recreation
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

        // Account for the window chrome, since we're sizing the client area,
        // plus the fallback frame if it's drawn inside the content.
        Size chrome = new(Width - ClientSize.Width, Height - ClientSize.Height);
        int inset = _useFallbackBorder ? FallbackBorderPx * 2 : 0;

        double scale = Math.Min(1.0, Math.Min(
            (double)(maxW - chrome.Width - inset) / src.Width,
            (double)(maxH - chrome.Height - inset) / src.Height));

        var target = new Size(
            Math.Max(MinimumSize.Width - chrome.Width, (int)Math.Round(src.Width * scale) + inset),
            Math.Max(MinimumSize.Height - chrome.Height, (int)Math.Round(src.Height * scale) + inset));

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
        if (_useFallbackBorder)
            client.Inflate(-FallbackBorderPx, -FallbackBorderPx);

        Size src = _thumbnail.GetSourceSize();

        Rectangle dest = client;
        if (src.Width > 0 && src.Height > 0 && client.Width > 0 && client.Height > 0)
        {
            double scale = Math.Min((double)client.Width / src.Width,
                                    (double)client.Height / src.Height);
            int w = Math.Max(1, (int)(src.Width * scale));
            int h = Math.Max(1, (int)(src.Height * scale));
            dest = new Rectangle(
                client.Left + (client.Width - w) / 2,
                client.Top + (client.Height - h) / 2, w, h);
        }

        _thumbnail.SetDestinationRect(dest);
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        BackgroundRenderer.Invalidate(); // the backdrop may have been swapped
        if (_sourceHwnd != IntPtr.Zero)
            ApplyAutoFit(_thumbnail.GetSourceSize());
        Invalidate();
    }
}
