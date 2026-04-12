using System.Diagnostics;

namespace FrameBoost.Models;

internal sealed class GameProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string ExecutablePath { get; set; } = string.Empty;

    public bool AutoBoost { get; set; } = true;

    public string? PreferredPowerPlanGuid { get; set; }

    public string? PreferredPowerPlanName { get; set; }

    public bool SwitchPowerPlan { get; set; } = true;

    public BoostPresetOption BoostPreset { get; set; } = BoostPresetOption.Performance;

    public bool BoostGamePriority { get; set; } = true;

    public ProcessPriorityOption GamePriority { get; set; } = ProcessPriorityOption.High;

    public bool LowerBackgroundProcesses { get; set; } = true;

    public bool TrimBackgroundMemory { get; set; } = true;

    public bool CloseBackgroundAppsGracefully { get; set; }

    public bool UseBackgroundMemoryPriority { get; set; } = true;

    public bool UseBackgroundEcoQos { get; set; } = true;

    public bool EnableRecurringMaintenance { get; set; } = true;

    public string BackgroundProcessesRaw { get; set; } = "Discord,SteamWebHelper,Chrome,msedge,opera,firefox,EpicGamesLauncher";

    public string ExecutableName =>
        string.IsNullOrWhiteSpace(ExecutablePath)
            ? string.Empty
            : Path.GetFileNameWithoutExtension(ExecutablePath);

    public IEnumerable<string> GetBackgroundProcessNames()
    {
        var configuredNames = BackgroundProcessesRaw
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static name => Path.GetFileNameWithoutExtension(name))
            .Where(static name => !string.IsNullOrWhiteSpace(name));

        return configuredNames
            .Concat(GetPresetBackgroundProcessNames())
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public ProcessPriorityClass ToBackgroundPriorityClass()
    {
        return BoostPreset switch
        {
            BoostPresetOption.Balanced => ProcessPriorityClass.BelowNormal,
            BoostPresetOption.Performance => ProcessPriorityClass.BelowNormal,
            BoostPresetOption.MaxFps => ProcessPriorityClass.Idle,
            _ => ProcessPriorityClass.BelowNormal
        };
    }

    public uint GetBackgroundMemoryPriority()
    {
        return BoostPreset switch
        {
            BoostPresetOption.Balanced => 4,
            BoostPresetOption.Performance => 2,
            BoostPresetOption.MaxFps => 1,
            _ => 4
        };
    }

    public bool ShouldApplyBackgroundEcoQos()
    {
        return UseBackgroundEcoQos && (BoostPreset is BoostPresetOption.Performance or BoostPresetOption.MaxFps);
    }

    public bool ShouldLowerBackgroundMemoryPriority()
    {
        return UseBackgroundMemoryPriority;
    }

    public bool ShouldRunRecurringMaintenance()
    {
        return EnableRecurringMaintenance;
    }

    public long GetMinimumBackgroundWorkingSetBytes()
    {
        return BoostPreset switch
        {
            BoostPresetOption.Balanced => 120L * 1024 * 1024,
            BoostPresetOption.Performance => 80L * 1024 * 1024,
            BoostPresetOption.MaxFps => 40L * 1024 * 1024,
            _ => 80L * 1024 * 1024
        };
    }

    public TimeSpan GetMaintenanceInterval()
    {
        return BoostPreset switch
        {
            BoostPresetOption.Balanced => TimeSpan.FromSeconds(18),
            BoostPresetOption.Performance => TimeSpan.FromSeconds(10),
            BoostPresetOption.MaxFps => TimeSpan.FromSeconds(6),
            _ => TimeSpan.FromSeconds(12)
        };
    }

    public ProcessPriorityClass ToProcessPriorityClass()
    {
        return GamePriority switch
        {
            ProcessPriorityOption.Normal => ProcessPriorityClass.Normal,
            ProcessPriorityOption.AboveNormal => ProcessPriorityClass.AboveNormal,
            _ => ProcessPriorityClass.High
        };
    }

    public override string ToString() => Name;

    private IEnumerable<string> GetPresetBackgroundProcessNames()
    {
        return BoostPreset switch
        {
            BoostPresetOption.Balanced =>
            [
                "Discord",
                "SteamWebHelper",
                "Chrome",
                "msedge",
                "firefox",
                "opera"
            ],
            BoostPresetOption.Performance =>
            [
                "Discord",
                "SteamWebHelper",
                "Chrome",
                "msedge",
                "firefox",
                "opera",
                "brave",
                "EpicGamesLauncher",
                "GalaxyClient",
                "Overwolf",
                "OneDrive",
                "Dropbox"
            ],
            BoostPresetOption.MaxFps =>
            [
                "Discord",
                "SteamWebHelper",
                "Chrome",
                "msedge",
                "firefox",
                "opera",
                "brave",
                "EpicGamesLauncher",
                "GalaxyClient",
                "Overwolf",
                "OneDrive",
                "Dropbox",
                "Telegram",
                "Slack",
                "Spotify",
                "Teams",
                "RiotClientServices",
                "Battle.net",
                "EADesktop",
                "UbisoftConnect",
                "GOGGalaxy",
                "Adobe Desktop Service"
            ],
            _ => []
        };
    }
}

internal enum ProcessPriorityOption
{
    Normal,
    AboveNormal,
    High
}

internal enum BoostPresetOption
{
    Balanced,
    Performance,
    MaxFps
}
