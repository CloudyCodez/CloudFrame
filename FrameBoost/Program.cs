using FrameBoost.Core;

namespace FrameBoost;

static class Program
{
    [STAThread]
    static void Main()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            try
            {
                File.AppendAllLines(AppPaths.LogPath,
                [
                    $"[{DateTime.Now:HH:mm:ss}] Unhandled AppDomain exception: {args.ExceptionObject}"
                ]);
            }
            catch
            {
            }
        };

        Application.ThreadException += (_, args) =>
        {
            try
            {
                File.AppendAllLines(AppPaths.LogPath,
                [
                    $"[{DateTime.Now:HH:mm:ss}] Unhandled UI exception: {args.Exception}"
                ]);
            }
            catch
            {
            }
        };

        try
        {
            AppPaths.EnsureDataDirectory();
            File.AppendAllLines(AppPaths.LogPath,
            [
                $"[{DateTime.Now:HH:mm:ss}] CloudFrame startup entered Main."
            ]);
        }
        catch
        {
        }

        ApplicationConfiguration.Initialize();
        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllLines(AppPaths.LogPath,
                [
                    $"[{DateTime.Now:HH:mm:ss}] Startup failed before main loop: {ex}"
                ]);
            }
            catch
            {
            }

            MessageBox.Show(
                $"CloudFrame could not start.\r\n\r\n{ex.Message}",
                "CloudFrame",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
