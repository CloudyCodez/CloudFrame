namespace FrameBoost.Core;

internal static class AppPaths
{
    private static readonly string BasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CloudsFrameBoost");

    public static string DataDirectory => BasePath;

    public static string SettingsPath => Path.Combine(BasePath, "settings.json");

    public static string RecoveryPath => Path.Combine(BasePath, "recovery.json");

    public static string LogPath => Path.Combine(BasePath, "frameboost.log");

    public static string UpdatesDirectory => Path.Combine(BasePath, "updates");

    public static string UpdateStagingDirectory => Path.Combine(UpdatesDirectory, "staging");

    public static void EnsureDataDirectory()
    {
        Directory.CreateDirectory(BasePath);
        Directory.CreateDirectory(UpdatesDirectory);
    }
}
