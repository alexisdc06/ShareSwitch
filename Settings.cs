using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ShareSwitch;

/// <summary>
/// Application settings, persisted as JSON in
/// <c>%APPDATA%\ShareSwitch\settings.json</c>.
///
/// Slot assignments are deliberately <b>not</b> persisted: they hold window
/// handles, which are only meaningful within one Windows session.
/// </summary>
public sealed class AppSettings
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "ShareSwitch";

    public static AppSettings Current { get; private set; } = new();

    /// <summary>
    /// Modifier combination that activates hold-to-select mode and prefixes the
    /// slot/cycle shortcuts. Only Control / Alt / Shift flags are honored.
    /// </summary>
    public Keys SelectionModifiers { get; set; } = Keys.Control | Keys.Alt;

    /// <summary>
    /// Delay before selection mode engages once the combo is held, in ms.
    /// Prevents the highlight from flashing during AltGr typing on AZERTY
    /// layouts (AltGr reports as Ctrl+Alt) and during ordinary shortcuts.
    /// </summary>
    public int SelectionActivationDelayMs { get; set; } = 150;

    /// <summary>
    /// When true, ShareWindow continuously resizes itself to match the source
    /// window's shape, so viewers see no letterbox bars.
    /// </summary>
    public bool AutoFitToSource { get; set; } = true;

    /// <summary>
    /// Custom backdrop shown behind the mirror (and before a source is picked).
    /// Null or empty uses the built-in ShareSwitch artwork.
    ///
    /// No file picker is exposed in the UI yet — this is the natural hook for a
    /// branded/company tier. Editing the JSON by hand works in the meantime.
    /// </summary>
    public string? BackgroundImagePath { get; set; }

    /// <summary>Enables recalling assigned windows with modifier + 1…9.</summary>
    public bool EnableSlotShortcuts { get; set; } = true;

    /// <summary>Enables stepping through open windows with modifier + ← / →.</summary>
    public bool EnableCycleShortcuts { get; set; } = true;

    /// <summary>Launch ShareSwitch when Windows starts (HKCU Run key).</summary>
    [JsonIgnore]
    public bool LaunchAtStartup
    {
        get
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(RunValueName) is not null;
            }
            catch
            {
                return false; // registry unreadable — report "off" rather than crash
            }
        }
        set
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                if (key is null) return;

                if (value)
                    key.SetValue(RunValueName, $"\"{Application.ExecutablePath}\"");
                else
                    key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
            catch
            {
                // Locked-down machine or policy-managed key: silently ignore.
                // The checkbox re-reads the real state, so the UI stays truthful.
            }
        }
    }

    /// <summary>Raised when settings change so live components can re-read.</summary>
    public event EventHandler? Changed;

    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    #region Persistence

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ShareSwitch", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Loads saved settings, falling back to defaults on any problem.</summary>
    public static void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;

            var loaded = JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(SettingsPath), JsonOptions);

            if (loaded is not null)
                Current = loaded;
        }
        catch
        {
            // Corrupt or unreadable file: keep defaults rather than block startup.
        }
    }

    /// <summary>Writes settings to disk. Never throws.</summary>
    public static void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(SettingsPath);
            if (dir is not null) Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Current, JsonOptions));
        }
        catch
        {
            // Read-only profile or disk full: losing preferences is preferable
            // to crashing on exit.
        }
    }

    #endregion

    public static string DescribeModifiers(Keys modifiers)
    {
        var parts = new List<string>(3);
        if (modifiers.HasFlag(Keys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(Keys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(Keys.Shift)) parts.Add("Shift");
        return parts.Count > 0 ? string.Join(" + ", parts) : "(none)";
    }
}
