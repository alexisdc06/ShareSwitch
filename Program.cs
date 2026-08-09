using System.Windows.Forms;

namespace ShareSwitch;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "ShareSwitch_SingleInstance_v1", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("ShareSwitch is already running (check the system tray).",
                "ShareSwitch", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize(); // DPI mode comes from the .csproj

        // Surface unexpected UI-thread exceptions instead of dying silently in the tray.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            MessageBox.Show($"Unexpected error:\n{e.Exception}", "ShareSwitch",
                MessageBoxButtons.OK, MessageBoxIcon.Error);

        try
        {
            Application.Run(new TrayApplicationContext());
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }
}
