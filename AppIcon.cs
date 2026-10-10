using System.Drawing;
using System.Windows.Forms;

namespace ShareSwitch;

/// <summary>
/// The app icon, loaded from the embedded multi-size .ico (16–256 px, with
/// hand-tuned 16/20/24/32 px frames so the tray icon stays crisp).
/// Falls back to the exe's icon, then the system default.
/// </summary>
public static class AppIcon
{
    private static Icon? _icon;
    private static Icon? _small;

    /// <summary>Full multi-size icon, for window title bars and Alt+Tab.</summary>
    public static Icon Get()
    {
        if (_icon is not null) return _icon;
        try
        {
            using Stream? s = typeof(AppIcon).Assembly.GetManifestResourceStream("ShareSwitch.icon.ico");
            if (s is not null) _icon = new Icon(s);
        }
        catch
        {
            _icon = null;
        }
        if (_icon is null)
        {
            try { _icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { /* fall through */ }
        }
        return _icon ??= SystemIcons.Application;
    }

    /// <summary>
    /// The frame that matches the tray's size at the current DPI (16/20/24 px),
    /// rather than a downscaled 32 px icon.
    /// </summary>
    public static Icon GetSmall()
    {
        if (_small is not null) return _small;
        try { _small = new Icon(Get(), SystemInformation.SmallIconSize); }
        catch { _small = null; }
        return _small ??= Get();
    }
}
