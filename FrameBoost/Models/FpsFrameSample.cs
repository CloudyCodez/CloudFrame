namespace FrameBoost.Models;

internal readonly record struct FpsFrameSample(
    DateTimeOffset CapturedAt,
    int? ProcessId,
    string? Application,
    double Fps,
    double? FrameTimeMs);
