namespace FrameBoost.Models;

internal sealed class SessionReport
{
    public string ProfileName { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public string ResultLabel { get; init; } = string.Empty;

    public bool HasMeasuredGain { get; init; }

    public double? BaselineAverageFps { get; init; }

    public double? LiveAverageFps { get; init; }

    public double? BaselineOnePercentLowFps { get; init; }

    public double? LiveOnePercentLowFps { get; init; }

    public double? BaselinePacingScore { get; init; }

    public double? LivePacingScore { get; init; }
}
