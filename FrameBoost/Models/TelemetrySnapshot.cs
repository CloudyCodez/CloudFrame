namespace FrameBoost.Models;

internal sealed class TelemetrySnapshot
{
    public double CpuPercent { get; init; }

    public double? GpuPercent { get; init; }

    public double? FramesPerSecond { get; init; }

    public double? FrameTimeMs { get; init; }

    public string FpsStatus { get; init; } = "Waiting for game";

    public int? TargetProcessId { get; init; }

    public string? TargetProcessName { get; init; }

    public bool CompatibilityMode { get; init; }

    public string? CompatibilityLabel { get; init; }
}
