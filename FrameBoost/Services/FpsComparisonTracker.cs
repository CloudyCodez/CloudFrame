using FrameBoost.Models;

namespace FrameBoost.Services;

internal sealed class FpsComparisonTracker
{
    private const int IdleBaselineSampleLimit = 24;
    private const int SessionSampleLimit = 36;
    private const int MinimumBaselineSamples = 5;
    private const int MinimumSessionSamples = 4;

    private readonly Queue<double> _idleSamples = new();
    private readonly Queue<double> _sessionSamples = new();

    private string? _activeSessionKey;
    private string? _activeSessionName;
    private double? _baselineAtSessionStart;
    private FpsDeltaSnapshot _lastCompleted = FpsDeltaSnapshot.Empty;

    public void Observe(TelemetrySnapshot telemetry, ActiveBoostSession? session)
    {
        var fps = telemetry.FramesPerSecond;
        var hasActiveBoost = session is not null && !session.RecoveryState.IsPreLaunchBoost;

        if (hasActiveBoost)
        {
            var sessionKey = $"{session!.Profile.Id}:{session.RecoveryState.GameProcessId}";
            if (!string.Equals(_activeSessionKey, sessionKey, StringComparison.Ordinal))
            {
                BeginSession(sessionKey, session.Profile.Name);
            }

            if (fps is > 0)
            {
                Enqueue(_sessionSamples, fps.Value, SessionSampleLimit);
            }

            return;
        }

        CompleteSessionIfNeeded();

        if (fps is > 0)
        {
            Enqueue(_idleSamples, fps.Value, IdleBaselineSampleLimit);
        }
    }

    public FpsDeltaSnapshot GetSnapshot()
    {
        if (_activeSessionKey is not null)
        {
            return BuildActiveSnapshot();
        }

        if (_lastCompleted.HasResult)
        {
            return _lastCompleted with { Status = "Last completed boost result" };
        }

        var baseline = AverageOrNull(_idleSamples);
        if (baseline is null)
        {
            return new FpsDeltaSnapshot(
                "Calibrating",
                "Need idle FPS samples",
                "Let CloudFrame watch a running game for a few seconds so it can learn your baseline.",
                null,
                null,
                null,
                0,
                false);
        }

        return new FpsDeltaSnapshot(
            "Ready",
            $"{baseline.Value:0} FPS baseline",
            "Boost a selected game to start a live before/after comparison.",
            baseline,
            null,
            null,
            _idleSamples.Count,
            false);
    }

    public FpsDeltaSnapshot CompleteCurrentSession()
    {
        CompleteSessionIfNeeded();
        return _lastCompleted;
    }

    private void BeginSession(string sessionKey, string sessionName)
    {
        CompleteSessionIfNeeded();
        _activeSessionKey = sessionKey;
        _activeSessionName = sessionName;
        _baselineAtSessionStart = _idleSamples.Count >= MinimumBaselineSamples
            ? AverageOrNull(_idleSamples)
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

    private FpsDeltaSnapshot BuildActiveSnapshot()
    {
        var liveAverage = AverageOrNull(_sessionSamples);

        if (_baselineAtSessionStart is null)
        {
            return new FpsDeltaSnapshot(
                "Calibrating",
                "Building a baseline",
                $"CloudFrame needs a few idle FPS samples before it can compare gains for {_activeSessionName ?? "this game"}.",
                null,
                liveAverage,
                null,
                _sessionSamples.Count,
                false);
        }

        if (liveAverage is null || _sessionSamples.Count < MinimumSessionSamples)
        {
            return new FpsDeltaSnapshot(
                "Sampling",
                "Collecting live samples",
                $"Tracking {_activeSessionName ?? "the boosted game"} now. Delta appears after a few more FPS samples.",
                _baselineAtSessionStart,
                liveAverage,
                null,
                _sessionSamples.Count,
                false);
        }

        var deltaFps = liveAverage.Value - _baselineAtSessionStart.Value;
        var deltaPercent = _baselineAtSessionStart.Value <= 0
            ? 0
            : (deltaFps / _baselineAtSessionStart.Value) * 100d;
        var sign = deltaFps >= 0 ? "+" : string.Empty;

        return new FpsDeltaSnapshot(
            "Measured",
            $"{sign}{deltaFps:0.#} FPS",
            $"Baseline {_baselineAtSessionStart.Value:0.#} FPS -> Live {liveAverage.Value:0.#} FPS ({sign}{deltaPercent:0.#}%)",
            _baselineAtSessionStart,
            liveAverage,
            deltaPercent,
            _sessionSamples.Count,
            true);
    }

    private static void Enqueue(Queue<double> queue, double value, int maxCount)
    {
        queue.Enqueue(value);
        while (queue.Count > maxCount)
        {
            queue.Dequeue();
        }
    }

    private static double? AverageOrNull(IEnumerable<double> values)
    {
        var materialized = values as double[] ?? values.ToArray();
        return materialized.Length == 0 ? null : materialized.Average();
    }
}

internal sealed record FpsDeltaSnapshot(
    string Status,
    string Value,
    string Detail,
    double? BaselineAverage,
    double? LiveAverage,
    double? DeltaPercent,
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
        0,
        false);
}
