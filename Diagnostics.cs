namespace ShareSwitch;

/// <summary>
/// Minimal error log at <c>%APPDATA%\ShareSwitch\errors.log</c>, so failures
/// inside the input hooks (which must never throw) leave a trace instead of
/// vanishing silently.
///
/// Privacy: only exception type, message and stack trace are written — never
/// keys, window titles or anything else the user typed or viewed.
/// </summary>
internal static class Diagnostics
{
    private static readonly object Gate = new();
    private const long MaxBytes = 256 * 1024;

    public static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShareSwitch", "errors.log");

    public static void LogError(string where, Exception ex)
    {
        try
        {
            lock (Gate)
            {
                string path = LogPath;
                string? dir = Path.GetDirectoryName(path);
                if (dir is not null) Directory.CreateDirectory(dir);
                if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                    File.Delete(path);   // keep it small; only recent errors matter
                File.AppendAllText(path,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  [{where}]  {ex.GetType().Name}: {ex.Message}{Environment.NewLine}" +
                    $"{ex.StackTrace}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never become a second failure.
        }
    }
}
