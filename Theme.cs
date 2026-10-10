using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ShareSwitch.Interop;

namespace ShareSwitch;

/// <summary>
/// The ShareSwitch brand, in code: palette from the brand guideline, embedded
/// Figtree (SIL OFL — a licensed stand-in for the trial-only Aeonik), the
/// glass backdrop, the logo lockup, and the drawing helpers every surface uses.
///
/// Fonts returned by <see cref="Head"/>, <see cref="Accent"/> and
/// <see cref="Body"/> are cached for the app's lifetime — callers must not
/// dispose them.
/// </summary>
internal static class Theme
{
    // ── Palette (brand guideline) ───────────────────────────────────────────
    public static readonly Color Violet = Color.FromArgb(144, 133, 255);      // Light Slate Blue  #9085FF
    public static readonly Color Navy = Color.FromArgb(23, 21, 47);           // Dark Slate Violet #17152F
    public static readonly Color Charcoal = Color.FromArgb(18, 18, 18);       // Dark Charcoal     #121212
    public static readonly Color OffWhite = Color.FromArgb(242, 242, 242);    // Anti-Flash White  #F2F2F2
    // UI tints derived from the palette
    public static readonly Color Surface = Color.FromArgb(32, 29, 62);        // raised panel on navy
    public static readonly Color SurfaceHover = Color.FromArgb(46, 42, 86);
    public static readonly Color Line = Color.FromArgb(60, 55, 109);          // hairlines, off-state tracks
    public static readonly Color Muted = Color.FromArgb(168, 163, 209);       // secondary text
    public static readonly Color SoftText = Color.FromArgb(214, 211, 240);
    public static readonly Color VioletDeep = Color.FromArgb(101, 90, 214);   // pressed / shadow of violet

    // ── Embedded resources ─────────────────────────────────────────────────
    private const string FontBold = "ShareSwitch.fonts.Figtree-Bold.ttf";
    private const string FontRegular = "ShareSwitch.fonts.Figtree-Regular.ttf";
    private const string FontLightItalic = "ShareSwitch.fonts.Figtree-LightItalic.ttf";

    private static readonly List<PrivateFontCollection> Collections = new();   // keep alive
    private static readonly Dictionary<string, FontFamily?> Families = new();
    private static readonly Dictionary<(string, int), Font> FontCache = new();

    private static Image? _lockup;
    private static Image? _backdrop;
    private static bool _lockupTried, _backdropTried;

    /// <summary>White "SS Share Switch" lockup, or null if the resource is missing.</summary>
    public static Image? Lockup
    {
        get
        {
            if (!_lockupTried) { _lockupTried = true; _lockup = LoadImage("ShareSwitch.lockup-white.png"); }
            return _lockup;
        }
    }

    /// <summary>The brand backdrop: gradient + oversized glass SS (cached, do not dispose).</summary>
    public static Image? Backdrop
    {
        get
        {
            if (!_backdropTried) { _backdropTried = true; _backdrop = LoadImage("ShareSwitch.backdrop.jpg"); }
            return _backdrop;
        }
    }

    private static Image? LoadImage(string resource)
    {
        try
        {
            using Stream? s = typeof(Theme).Assembly.GetManifestResourceStream(resource);
            if (s is null) return null;
            // Copy out: Image.FromStream needs its stream alive for the image's lifetime.
            var ms = new MemoryStream();
            s.CopyTo(ms);
            ms.Position = 0;
            return Image.FromStream(ms);
        }
        catch
        {
            return null;
        }
    }

    // ── Fonts ──────────────────────────────────────────────────────────────
    /// <summary>Headline: Figtree Bold (Aeonik Bold role). Size in pixels.</summary>
    public static Font Head(float px) => Get(FontBold, px, FontStyle.Bold);

    /// <summary>Accent words: Figtree Light Italic. Size in pixels.</summary>
    public static Font Accent(float px) => Get(FontLightItalic, px, FontStyle.Italic);

    /// <summary>Body copy: Figtree Regular. Size in pixels.</summary>
    public static Font Body(float px) => Get(FontRegular, px, FontStyle.Regular);

    private static Font Get(string resource, float px, FontStyle fallback)
    {
        int key = Math.Max(6, (int)Math.Round(px));
        if (FontCache.TryGetValue((resource, key), out Font? f))
            return f;

        f = null;
        FontFamily? family = LoadFamily(resource);
        if (family is not null)
        {
            // Each file is a single face; use whichever style GDI+ reports for it.
            foreach (FontStyle st in new[] { FontStyle.Regular, FontStyle.Bold, FontStyle.Italic, FontStyle.Bold | FontStyle.Italic })
            {
                if (family.IsStyleAvailable(st))
                {
                    f = new Font(family, key, st, GraphicsUnit.Pixel);
                    break;
                }
            }
        }
        f ??= new Font("Segoe UI", key, fallback, GraphicsUnit.Pixel);
        FontCache[(resource, key)] = f;
        return f;
    }

    private static FontFamily? LoadFamily(string resource)
    {
        if (Families.TryGetValue(resource, out FontFamily? cached))
            return cached;

        FontFamily? family = null;
        try
        {
            using Stream? s = typeof(Theme).Assembly.GetManifestResourceStream(resource);
            if (s is not null)
            {
                var data = new byte[s.Length];
                s.ReadExactly(data);
                // GDI+ reads the font from this memory for as long as the family
                // exists, so it is intentionally never freed (a few KB, once).
                IntPtr mem = Marshal.AllocCoTaskMem(data.Length);
                Marshal.Copy(data, 0, mem, data.Length);
                var pfc = new PrivateFontCollection();
                pfc.AddMemoryFont(mem, data.Length);
                Collections.Add(pfc);
                family = pfc.Families.Length > 0 ? pfc.Families[0] : null;
            }
        }
        catch
        {
            family = null;   // fall back to Segoe UI
        }
        Families[resource] = family;
        return family;
    }

    // ── Text helpers (GDI+, baseline-aligned so mixed fonts sit on one line) ──
    private static readonly StringFormat Typo = CreateTypo();

    private static StringFormat CreateTypo()
    {
        var f = (StringFormat)StringFormat.GenericTypographic.Clone();
        f.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
        return f;
    }

    /// <summary>Distance from the top of the line box to the baseline, in pixels.</summary>
    public static float Ascent(Font f)
    {
        FontFamily fam = f.FontFamily;
        return f.Size * fam.GetCellAscent(f.Style) / fam.GetEmHeight(f.Style);
    }

    public static float Measure(Graphics g, string text, Font f)
        => g.MeasureString(text, f, PointF.Empty, Typo).Width;

    /// <summary>Draws text with its baseline at <paramref name="baseline"/>; returns the end x.</summary>
    public static float DrawRun(Graphics g, string text, Font f, Color color, float x, float baseline)
    {
        using var b = new SolidBrush(color);
        g.DrawString(text, f, b, x, baseline - Ascent(f), Typo);
        return x + Measure(g, text, f);
    }

    // ── Shapes ─────────────────────────────────────────────────────────────
    public static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0.5f)
        {
            p.AddRectangle(r);
            return p;
        }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>A pressed-looking violet keycap; returns its rectangle.</summary>
    public static RectangleF DrawKeycap(Graphics g, string label, float x, float centerY, float h, Font f)
    {
        float tw = Measure(g, label, f);
        float w = Math.Max(h * 1.25f, tw + h * 0.75f);
        var r = new RectangleF(x, centerY - h / 2, w, h);
        float radius = h * 0.24f;

        using (var glow = RoundedRect(RectangleF.Inflate(r, h * 0.12f, h * 0.12f), radius * 1.5f))
        using (var gb = new SolidBrush(Color.FromArgb(55, Violet)))
            g.FillPath(gb, glow);
        using (var lip = RoundedRect(new RectangleF(r.X, r.Y + h * 0.08f, r.Width, r.Height), radius))
        using (var lb = new SolidBrush(VioletDeep))
            g.FillPath(lb, lip);
        using (var top = RoundedRect(new RectangleF(r.X, r.Y, r.Width, r.Height * 0.92f), radius))
        using (var tb = new SolidBrush(Violet))
        using (var pen = new Pen(Color.FromArgb(200, 214, 209, 255), Math.Max(1f, h * 0.03f)))
        {
            g.FillPath(tb, top);
            g.DrawPath(pen, top);
        }
        // optical centre: baseline a little below the middle of the key face
        DrawRun(g, label, f, Color.White, r.X + (w - tw) / 2, r.Y + h * 0.46f + f.Size * 0.36f);
        return r;
    }

    /// <summary>Scale-to-fill an image into a rectangle (centre crop).</summary>
    public static void PaintCover(Graphics g, Image img, Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        double s = Math.Max((double)bounds.Width / img.Width, (double)bounds.Height / img.Height);
        int w = (int)Math.Ceiling(img.Width * s), h = (int)Math.Ceiling(img.Height * s);
        var prev = g.InterpolationMode;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(img, new Rectangle(bounds.X + (bounds.Width - w) / 2, bounds.Y + (bounds.Height - h) / 2, w, h));
        g.InterpolationMode = prev;
    }

    /// <summary>Dark title bar + brand-tinted chrome for app dialogs (Windows 11; no-op elsewhere).</summary>
    public static void ApplyDarkChrome(Form form)
    {
        if (!form.IsHandleCreated) return;
        try
        {
            uint on = 1;
            _ = NativeMethods.DwmSetWindowAttribute(form.Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(uint));
            NativeMethods.TrySetWindowChrome(form.Handle, border: Line, caption: Navy, text: OffWhite);
        }
        catch
        {
            // Older Windows: keep the default chrome.
        }
    }
}
