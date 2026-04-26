namespace FrameBoost.Models;

internal readonly record struct FramePerformanceMetrics(
    double AverageFps,
    double OnePercentLowFps,
    double AverageFrameTimeMs,
    double P95FrameTimeMs,
    double PacingScore,
    int SampleCount);
