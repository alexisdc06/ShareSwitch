using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;

namespace ShareSwitch;

/// <summary>
/// Supplies the backdrop painted behind the mirror: the branded default that
/// ships with the app, or a custom image the user points at.
///
/// This is what viewers see in the moments before a source is picked, and
/// behind the letterbox bars when auto-fit is off — so it's the natural place
/// for a company to put its own artwork.
///
/// The image is loaded once and cached; failures fall back to the built-in one,
/// then to a plain fill, so a bad path can never break the share.
/// </summary>
public static class BackgroundRenderer
{
    private static Image? _cached;
    private static string? _cachedKey;

    /// <summary>Solid colour used if no image can be loaded at all.</summary>
    public static Color FallbackColor { get; } = Color.FromArgb(14, 17, 22);

    /// <summary>Drops the cache so the next paint reloads (call when settings change).</summary>
    public static void Invalidate()
    {
        _cached?.Dispose();
        _cached = null;
        _cachedKey = null;
    }

    /// <summary>
    /// Paints the backdrop across <paramref name="bounds"/>, scaled to cover
    /// (preserving aspect ratio, cropping the overflow).
    /// </summary>
    public static void Paint(Graphics g, Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        using (var brush = new SolidBrush(FallbackColor))
            g.FillRectangle(brush, bounds);

        Image? image = GetImage();
        if (image is null) return;

        // "Cover": scale so the image fills the box, centring the overflow.
        double scale = Math.Max((double)bounds.Width / image.Width,
                                (double)bounds.Height / image.Height);
        int w = (int)Math.Ceiling(image.Width * scale);
        int h = (int)Math.Ceiling(image.Height * scale);
        var dest = new Rectangle(
            bounds.X + (bounds.Width - w) / 2,
            bounds.Y + (bounds.Height - h) / 2, w, h);

        var previous = g.InterpolationMode;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(image, dest);
        g.InterpolationMode = previous;
    }

    private static Image? GetImage()
    {
        string key = AppSettings.Current.BackgroundImagePath ?? string.Empty;

        if (_cached is not null && _cachedKey == key)
            return _cached;

        Invalidate();
        _cachedKey = key;

        if (key.Length > 0)
        {
            try
            {
                // Copy into memory so the file isn't locked while we run.
                using var stream = new MemoryStream(File.ReadAllBytes(key));
                _cached = Image.FromStream(stream);
                return _cached;
            }
            catch
            {
                // Missing, unreadable or not an image — fall through to default.
            }
        }

        _cached = LoadEmbeddedDefault();
        return _cached;
    }

    private static Image? LoadEmbeddedDefault()
    {
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            // Match by suffix rather than a hard-coded logical name, which
            // depends on root namespace and folder layout.
            string? name = Array.Find(assembly.GetManifestResourceNames(),
                n => n.EndsWith("background.png", StringComparison.OrdinalIgnoreCase));
            if (name is null) return null;

            using Stream? stream = assembly.GetManifestResourceStream(name);
            return stream is null ? null : Image.FromStream(stream);
        }
        catch
        {
            return null;
        }
    }
}
