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
    private static readonly Color AccentColor = Theme.Violet;

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
        MinimumSize = new Size(240, 160); // low, so small sources can be matched exactly
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Navy; // behind the backdrop, before first paint

        RestoreBounds();

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
        ResizeEnd += (_, _) =>
        {
            _userResizing = false;
            PersistBounds(); // fires after both move and resize drags
        };
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

    #region Window position

    /// <summary>
    /// Small by default: the idle window is just a placeholder, and auto-fit
    /// resizes it the moment a source is picked, so a large default would only
    /// get in the user's way.
    /// </summary>
    private static readonly Size DefaultSize = new(480, 270);

    private void RestoreBounds()
    {
        var settings = AppSettings.Current;

        // Size is only meaningful to restore when the user controls it.
        Size size = !settings.AutoFitToSource
                    && settings.WindowWidth is { } w && settings.WindowHeight is { } h
            ? new Size(Math.Max(MinimumSize.Width, w), Math.Max(MinimumSize.Height, h))
            : DefaultSize;

        Point location = settings.WindowX is { } x && settings.WindowY is { } y
            ? new Point(x, y)
            : new Point(120, 120);

        Size = size;
        Location = EnsureOnScreen(new Rectangle(location, size));
    }

    /// <summary>
    /// Keeps the window reachable: a saved position can point at a monitor that
    /// has since been unplugged, or off the edge of a smaller screen.
    /// </summary>
    private static Point EnsureOnScreen(Rectangle bounds)
    {
        // Visible if it overlaps any screen's working area meaningfully.
        foreach (Screen screen in Screen.AllScreens)
        {
            Rectangle overlap = Rectangle.Intersect(screen.WorkingArea, bounds);
            if (overlap.Width >= 80 && overlap.Height >= 40)
                return bounds.Location;
        }

        Rectangle work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1024, 768);
        return new Point(
            work.Left + Math.Max(0, (work.Width - bounds.Width) / 2),
            work.Top + Math.Max(0, (work.Height - bounds.Height) / 2));
    }

    private void PersistBounds()
    {
        if (WindowState != FormWindowState.Normal) return;

        var settings = AppSettings.Current;
        settings.WindowX = Left;
        settings.WindowY = Top;

        // With auto-fit on, the size belongs to the mirrored window, not the user.
        if (!settings.AutoFitToSource)
        {
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
        }
    }

    #endregion

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
            caption: active ? AccentColor : Theme.Navy,
            text: active ? Color.White : Theme.SoftText);

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
        Rectangle c = ClientRectangle;
        if (c.Width < 60 || c.Height < 60) return;

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        // Laid out on a 960x540 grid and scaled, so the card keeps its
        // proportions at any window size (the default window is 480x270).
        float u = Math.Min(c.Width / 960f, c.Height / 540f);
        float m = Math.Max(14f, c.Width * 0.075f);

        // Navy wash from the left: the copy sits on calm ground, the glass SS
        // stays bright on the right.
        var washRect = new RectangleF(0, 0, Math.Max(1f, c.Width * 0.78f), c.Height);
        using (var wash = new System.Drawing.Drawing2D.LinearGradientBrush(
                   washRect, Color.FromArgb(200, Theme.Navy), Color.FromArgb(0, Theme.Navy), 0f))
        {
            wash.WrapMode = System.Drawing.Drawing2D.WrapMode.TileFlipX;
            g.FillRectangle(wash, washRect);
        }

        if (Theme.Lockup is { } lk)
        {
            float lh = 50 * u;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(lk, m, c.Height * 0.12f, lk.Width * lh / lk.Height, lh);
        }

        Font head = Theme.Head(Math.Max(15f, 46 * u));
        Font accent = Theme.Accent(Math.Max(15f, 46 * u));
        Font body = Theme.Body(Math.Max(11f, 19 * u));
        Font small = Theme.Body(Math.Max(10f, 14 * u));
        Font keyFont = Theme.Head(Math.Max(10f, 15 * u));

        float line1 = c.Height * 0.47f;
        float x = Theme.DrawRun(g, "Share ", head, Theme.OffWhite, m, line1);
        Theme.DrawRun(g, "this window", accent, Theme.Violet, x, line1);
        float line2 = line1 + head.Size * 1.08f;
        Theme.DrawRun(g, "in Teams, Zoom or Meet.", head, Theme.OffWhite, m, line2);

        // The gesture, drawn as keycaps: [Ctrl] + [Alt] + click any window
        float rowY = line2 + head.Size * 0.9f + 22 * u;
        float kh = Math.Max(18f, 32 * u);
        float kx = m;
        float plusGap = Math.Max(5f, 9 * u);
        foreach (string key in ModifierNames(AppSettings.Current.SelectionModifiers))
        {
            RectangleF r = Theme.DrawKeycap(g, key, kx, rowY, kh, keyFont);
            kx = r.Right + plusGap;
            kx = Theme.DrawRun(g, "+", body, Theme.SoftText, kx, rowY + body.Size * 0.35f) + plusGap;
        }
        float room = c.Width - m - kx;
        foreach (string tail in new[] { "click any window to mirror it here", "click any window", "click a window" })
        {
            if (Theme.Measure(g, tail, body) <= room)
            {
                Theme.DrawRun(g, tail, body, Theme.SoftText, kx, rowY + body.Size * 0.35f);
                break;
            }
        }

        float foot = c.Height - Math.Max(12f, c.Height * 0.08f);
        if (_noticeMessage is { Length: > 0 })
            Theme.DrawRun(g, _noticeMessage, small, Theme.Violet, m, foot - small.Size * 1.6f);
        Theme.DrawRun(g, "Settings and shortcuts live in the tray icon, near the clock.", small, Theme.Muted, m, foot);
    }

    private static IEnumerable<string> ModifierNames(Keys modifiers)
    {
        if (modifiers.HasFlag(Keys.Control)) yield return "Ctrl";
        if (modifiers.HasFlag(Keys.Alt)) yield return "Alt";
        if (modifiers.HasFlag(Keys.Shift)) yield return "Shift";
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
        PersistBounds();

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
