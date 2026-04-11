namespace FrameBoost.Models;

public enum OverlayPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public enum OverlayStyle
{
    /// <summary>Dark rounded-rect card — current look.</summary>
    Card,
    /// <summary>Text only, fully transparent background — just the numbers floating on screen.</summary>
    Minimal
}

internal sealed class AppSettings
{
    // ── Detection / startup ───────────────────────────────────────────────────
    public bool StartMonitoringOnLaunch { get; set; } = true;

    public BoostPresetOption DefaultBoostPreset { get; set; } = BoostPresetOption.Performance;

    public bool EnableUpdateChecks { get; set; } = true;

    public string GitHubRepository { get; set; } = string.Empty;

    public string? SkippedUpdateVersion { get; set; }

    // ── Universal boost defaults ──────────────────────────────────────────────
    public bool UniversalSwitchPowerPlan { get; set; } = true;

    public bool UniversalLowerBackgroundProcesses { get; set; } = true;

    public bool UniversalTrimBackgroundMemory { get; set; } = true;

    public bool UniversalCloseBackgroundAppsGracefully { get; set; }

    public bool UniversalUseBackgroundMemoryPriority { get; set; } = true;

    public bool UniversalUseBackgroundEcoQos { get; set; } = true;

    public bool UniversalEnableRecurringMaintenance { get; set; } = true;

    // ── Profiles ──────────────────────────────────────────────────────────────
    public List<GameProfile> Profiles { get; set; } = [];

    // ── Overlay position & style ──────────────────────────────────────────────
    public OverlayPosition OverlayPosition { get; set; } = OverlayPosition.TopLeft;

    public OverlayStyle OverlayStyle { get; set; } = OverlayStyle.Card;

    /// <summary>Show the FPS counter in the overlay.</summary>
    public bool OverlayShowFps { get; set; } = true;

    /// <summary>Show CPU % in the overlay.</summary>
    public bool OverlayShowCpu { get; set; } = true;

    /// <summary>Show GPU % in the overlay.</summary>
    public bool OverlayShowGpu { get; set; } = true;

    /// <summary>
    /// ARGB int for the overlay's accent colour (default = hot pink #EC40C4).
    /// Stored as int so the JSON serialiser handles it without a custom converter.
    /// </summary>
    public int OverlayAccentArgb { get; set; } = unchecked((int)0xFFEC40C4);

    // ── Advanced boost tweaks ─────────────────────────────────────────────────
    /// <summary>
    /// Call timeBeginPeriod(1) during boost to reduce sleep granularity to 1 ms.
    /// Improves frame-time consistency. Requires no elevation.
    /// </summary>
    public bool EnableTimerResolution { get; set; } = true;

    /// <summary>
    /// Set MMCSS NetworkThrottlingIndex + SystemResponsiveness registry values
    /// during boost. Requires elevation; silently skipped when not elevated.
    /// </summary>
    public bool EnableMmcss { get; set; } = true;

    /// <summary>
    /// Disable Xbox Game DVR / Game Bar capture hook during boost.
    /// Removes a hidden DX overhead layer. Requires no elevation.
    /// </summary>
    public bool DisableGameDvr { get; set; } = true;
}
