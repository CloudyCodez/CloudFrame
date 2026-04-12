namespace FrameBoost.Models;

internal sealed class DetectedGame
{
    public required GameProfile Profile { get; init; }

    public required int ProcessId { get; init; }

    public required string ProcessName { get; init; }

    public string? ExecutablePath { get; init; }

    public int AnchorProcessId { get; init; }

    public string? AnchorProcessName { get; init; }

    public string? EngineHint { get; init; }

    public bool HasLauncherHandoff => AnchorProcessId > 0 && AnchorProcessId != ProcessId;

    public int BoostProcessId => AnchorProcessId > 0 ? AnchorProcessId : ProcessId;
}
