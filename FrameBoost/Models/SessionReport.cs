namespace FrameBoost.Models;

internal sealed class SessionReport
{
    public string ProfileName { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public string ResultLabel { get; init; } = string.Empty;

    public bool HasMeasuredGain { get; init; }
}
