using FrameBoost.Models;

namespace FrameBoost.Services;

internal sealed class FpsComparisonTracker
{
    private const int IdleBaselineSampleLimit = 1200;
    private const int SessionSampleLimit = 1800;
    private const int MinimumBaselineSamples = 90;
    private const int MinimumSessionSamples = 120;

    private readonly object _sync = new();
    private readonly Queue<PerformanceSample> _idleSamples = new();
    private readonly Queue<PerformanceSample> _sessionSamples = new();

    private string? _activeSessionKey;
    private string? _activeSessionName;
    private FramePerformanceMetrics? _baselineAtSessionStart;
    private FpsDeltaSnapshot _lastCompleted = FpsDeltaSnapshot.Empty;

    public void Observe(TelemetrySnapshot telemetry, ActiveBoostSession? session, bool baselineEligible)
    {
        lock (_sync)
        {
            EnsureSessionState(session);

            if (telemetry.FramesPerSecond is > 0)
            {
                RecordSampleCore(
                    new PerformanceSample(DateTimeOffset.UtcNow, telemetry.FramesPerSecond.Value, telemetry.FrameTimeMs),
                    session,
                    baselineEligible);
            }
        }
    }

    public void ObserveFrameSample(FpsFrameSample sample, ActiveBoostSession? session, bool baselineEligible)
    {
        lock (_sync)
        {
            EnsureSessionState(session);
            RecordSampleCore(
                new PerformanceSample(sample.CapturedAt, sample.Fps, sample.FrameTimeMs),
                session,
                baselineEligible);
        }
    }

    public FpsDeltaSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            if (_activeSessionKey is not null)
            {
                return BuildActiveSnapshot();
            }

            if (_lastCompleted.HasResult)
            {
                return _lastCompleted with { Status = "Last completed boost result" };
            }

            var baseline = BuildMetricsOrNull(_idleSamples, MinimumBaselineSamples);
            if (baseline is null)
            {
                return new FpsDeltaSnapshot(
                    "Calibrating",
                    "Need idle FPS samples",
                    "Let CloudFrame watch a running game for a few seconds so it can learn your baseline.",
                    null,
                    null,
                    null,
                    null,
                    0,
                    false);
            }

            return new FpsDeltaSnapshot(
                "Ready",
                $"{baseline.Value.AverageFps:0} FPS avg",
                $"Baseline 1% low {baseline.Value.OnePercentLowFps:0.#} FPS | Pacing {baseline.Value.PacingScore:0} | Boost a selected game to start a live comparison.",
                baseline,
                null,
                null,
                null,
                baseline.Value.SampleCount,
                false);
        }
    }

    public FpsDeltaSnapshot CompleteCurrentSession()
    {
        lock (_sync)
        {
            CompleteSessionIfNeeded();
            return _lastCompleted;
        }
    }

    private void EnsureSessionState(ActiveBoostSession? session)
    {
        var hasActiveBoost = session is not null && !session.RecoveryState.IsPreLaunchBoost;
        if (!hasActiveBoost)
        {
            CompleteSessionIfNeeded();
            return;
        }

        var sessionKey = $"{session!.Profile.Id}:{session.RecoveryState.GameProcessId}";
        if (!string.Equals(_activeSessionKey, sessionKey, StringComparison.Ordinal))
        {
            BeginSession(sessionKey, session.Profile.Name);
        }
    }

    private void BeginSession(string sessionKey, string sessionName)
    {
        CompleteSessionIfNeeded();
        _activeSessionKey = sessionKey;
        _activeSessionName = sessionName;
        _baselineAtSessionStart = _idleSamples.Count >= MinimumBaselineSamples
            ? BuildMetricsOrNull(_idleSamples, MinimumBaselineSamples)
            : null;
        _sessionSamples.Clear();
    }

    private void CompleteSessionIfNeeded()
    {
        if (_activeSessionKey is null)
        {
            return;
        }

        _lastCompleted = BuildActiveSnapshot();
        _activeSessionKey = null;
        _activeSessionName = null;
        _baselineAtSessionStart = null;
        _sessionSamples.Clear();
    }

    private void RecordSampleCore(PerformanceSample sample, ActiveBoostSession? session, bool baselineEligible)
    {
        if (session is not null && !session.RecoveryState.IsPreLaunchBoost)
        {
            Enqueue(_sessionSamples, sample, SessionSampleLimit);
            return;
        }

        if (baselineEligible)
        {
            Enqueue(_idleSamples, sample, IdleBaselineSampleLimit);
        }
    }

    private FpsDeltaSnapshot BuildActiveSnapshot()
    {
        var liveMetrics = BuildMetricsOrNull(_sessionSamples, MinimumSessionSamples);

        if (_baselineAtSessionStart is null)
        {
            return new FpsDeltaSnapshot(
                "Calibrating",
                "Building a baseline",
                $"CloudFrame needs a few idle FPS samples before it can compare gains for {_activeSessionName ?? "this game"}.",
                null,
                liveMetrics,
                null,
                null,
                _sessionSamples.Count,
                false);
        }

        if (liveMetrics is null)
        {
            return new FpsDeltaSnapshot(
                "Sampling",
                "Collecting live samples",
                $"Tracking {_activeSessionName ?? "the boosted game"} now. Delta appears after a few more FPS samples.",
                _baselineAtSessionStart,
                null,
                null,
                null,
                _sessionSamples.Count,
                false);
        }

        var baselineMetrics = _baselineAtSessionStart.Value;
        var deltaFps = liveMetrics.Value.AverageFps - baselineMetrics.AverageFps;
        var deltaLow = liveMetrics.Value.OnePercentLowFps - baselineMetrics.OnePercentLowFps;
        var deltaPercent = baselineMetrics.AverageFps <= 0
            ? 0
            : (deltaFps / baselineMetrics.AverageFps) * 100d;

        var avgSign = deltaFps >= 0 ? "+" : string.Empty;
        var lowSign = deltaLow >= 0 ? "+" : string.Empty;

        return new FpsDeltaSnapshot(
            "Measured",
            $"{avgSign}{deltaFps:0.#} avg | {lowSign}{deltaLow:0.#} 1%",
            $"Avg {baselineMetrics.AverageFps:0.#} -> {liveMetrics.Value.AverageFps:0.#} FPS | 1% low {baselineMetrics.OnePercentLowFps:0.#} -> {liveMetrics.Value.OnePercentLowFps:0.#} | Pacing {baselineMetrics.PacingScore:0} -> {liveMetrics.Value.PacingScore:0}",
            baselineMetrics,
            liveMetrics,
            deltaPercent,
            deltaLow,
            liveMetrics.Value.SampleCount,
            true);
    }

    private static void Enqueue(Queue<PerformanceSample> queue, PerformanceSample sample, int maxCount)
    {
        queue.Enqueue(sample);
        while (queue.Count > maxCount)
        {
            queue.Dequeue();
        }
    }

    private static FramePerformanceMetrics? BuildMetricsOrNull(IEnumerable<PerformanceSample> samples, int minimumSampleCount)
    {
        var materialized = samples as PerformanceSample[] ?? samples.ToArray();
        if (materialized.Length < minimumSampleCount)
        {
            return null;
        }

        var averageFps = materialized.Average(static sample => sample.Fps);
        var frameTimes = materialized
            .Select(static sample => sample.FrameTimeMs is > 0
                ? sample.FrameTimeMs.Value
                : sample.Fps > 0
                    ? 1000d / sample.Fps
                    : 0d)
            .Where(static value => value > 0 && double.IsFinite(value))
            .OrderBy(static value => value)
            .ToArray();

        if (frameTimes.Length < minimumSampleCount)
        {
            return null;
        }

        var averageFrameTime = frameTimes.Average();
        var p50FrameTime = Percentile(frameTimes, 0.50d);
        var p95FrameTime = Percentile(frameTimes, 0.95d);
        var p99FrameTime = Percentile(frameTimes, 0.99d);
        var onePercentLow = p99FrameTime > 0 ? 1000d / p99FrameTime : averageFps;
        var pacingSpreadRatio = p50FrameTime <= 0 ? 0 : (p95FrameTime - p50FrameTime) / p50FrameTime;
        var pacingScore = Math.Clamp(100d - (pacingSpreadRatio * 115d), 0d, 100d);

        return new FramePerformanceMetrics(
            averageFps,
            onePercentLow,
            averageFrameTime,
            p95FrameTime,
            pacingScore,
            frameTimes.Length);
    }

    private static double Percentile(IReadOnlyList<double> orderedValues, double percentile)
    {
        if (orderedValues.Count == 0)
        {
            return 0;
        }

        if (orderedValues.Count == 1)
        {
            return orderedValues[0];
        }

        var clamped = Math.Clamp(percentile, 0d, 1d);
        var position = (orderedValues.Count - 1) * clamped;
        var lowerIndex = (int)Math.Floor(position);
        var upperIndex = (int)Math.Ceiling(position);
        if (lowerIndex == upperIndex)
        {
            return orderedValues[lowerIndex];
        }

        var weight = position - lowerIndex;
        return orderedValues[lowerIndex] + ((orderedValues[upperIndex] - orderedValues[lowerIndex]) * weight);
    }
}

internal sealed record FpsDeltaSnapshot(
    string Status,
    string Value,
    string Detail,
    FramePerformanceMetrics? BaselineMetrics,
    FramePerformanceMetrics? LiveMetrics,
    double? DeltaPercent,
    double? DeltaOnePercentLowFps,
    int SampleCount,
    bool HasResult)
{
    public static FpsDeltaSnapshot Empty { get; } = new(
        "Calibrating",
        "Need samples",
        "Let CloudFrame watch a game for a few seconds so it can build a baseline.",
        null,
        null,
        null,
        null,
        0,
        false);
}

internal readonly record struct PerformanceSample(
    DateTimeOffset CapturedAt,
    double Fps,
    double? FrameTimeMs);
