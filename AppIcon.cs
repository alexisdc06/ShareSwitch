using System.Drawing;
using System.Windows.Forms;

namespace ShareSwitch;

/// <summary>
/// Single source for the application icon: extracted from the executable
/// (set via ApplicationIcon in the .csproj), with a system fallback so the
/// app still runs from environments where extraction fails (e.g. F5 designer quirks).
/// </summary>
public static class AppIcon
{
    private static Icon? _icon;

    public static Icon Get()
    {
        if (_icon is not null) return _icon;
        try
        {
            _icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // fall through
        }
        return _icon ??= SystemIcons.Application;
    }
}
