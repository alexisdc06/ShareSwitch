using System.Windows.Forms;

namespace ShareSwitch;

/// <summary>
/// In-memory application settings.
/// Phase 3: add JSON persistence (Load/Save to %APPDATA%\ShareSwitch\settings.json)
/// and auto-start registration — only this class should need to change.
/// </summary>
public sealed class AppSettings
{
    public static AppSettings Current { get; } = new();

    /// <summary>
    /// Modifier combination that activates hold-to-select mode.
    /// Only Keys.Control / Keys.Alt / Keys.Shift flags are honored.
    /// </summary>
    public Keys SelectionModifiers { get; set; } = Keys.Control | Keys.Alt;

    /// <summary>
    /// Delay before selection mode engages once the combo is held, in ms.
    /// Prevents the overlay from flashing during AltGr typing on AZERTY layouts
    /// (AltGr is reported as Ctrl+Alt) and during ordinary shortcuts.
    /// </summary>
    public int SelectionActivationDelayMs { get; set; } = 150;

    /// <summary>Raised when any setting changes so live components can re-read.</summary>
    public event EventHandler? Changed;

    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public static string DescribeModifiers(Keys modifiers)
    {
        var parts = new List<string>(3);
        if (modifiers.HasFlag(Keys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(Keys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(Keys.Shift)) parts.Add("Shift");
        return parts.Count > 0 ? string.Join(" + ", parts) : "(none)";
    }
}
