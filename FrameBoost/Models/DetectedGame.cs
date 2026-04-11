namespace FrameBoost.Models;

internal sealed class DetectedGame
{
    public required GameProfile Profile { get; init; }

    public required int ProcessId { get; init; }

    public required string ProcessName { get; init; }

    public string? ExecutablePath { get; init; }
}
