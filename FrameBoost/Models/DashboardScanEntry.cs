namespace FrameBoost.Models;

internal sealed class DashboardScanEntry
{
    public GameProfile? Profile { get; init; }

    public DetectedGame? MatchedGame { get; init; }

    public required int ProcessId { get; init; }

    public required string ProcessName { get; init; }

    public required string WindowTitle { get; init; }

    public required string ExecutablePath { get; init; }

    public string? EngineHint { get; init; }

    public string Status { get; init; } = string.Empty;

    public int InstanceCount { get; init; } = 1;

    public bool CanBoost => ProcessId > 0;

    public bool HasProfileMatch => Profile is not null;

    public int BoostProcessId => MatchedGame?.BoostProcessId ?? ProcessId;

    public string DisplayProfileName => Profile?.Name ?? "Universal boost";
}
