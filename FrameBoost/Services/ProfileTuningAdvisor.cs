using FrameBoost.Models;

namespace FrameBoost.Services;

internal sealed class ProfileTuningAdvisor
{
    private const int MaxSamples = 45;
    private const int MinimumSamples = 8;

    private readonly Queue<double> _cpuSamples = new();
    private readonly Queue<double> _gpuSamples = new();
    private readonly Queue<double> _fpsSamples = new();

    private string? _activeSessionKey;
    private string? _activeProfileName;
    private ProfileTuningRecommendation _lastRecommendation = ProfileTuningRecommendation.Empty;

    public void Observe(TelemetrySnapshot telemetry, ActiveBoostSession? session)
    {
        var hasLiveSession = session is not null && !session.RecoveryState.IsPreLaunchBoost;
        if (!hasLiveSession)
        {
            CompleteSessionIfNeeded();
            return;
        }

        var sessionKey = $"{session!.Profile.Id}:{session.RecoveryState.GameProcessId}";
        if (!string.Equals(_activeSessionKey, sessionKey, StringComparison.Ordinal))
        {
            BeginSession(sessionKey, session.Profile.Name);
        }

        Enqueue(_cpuSamples, telemetry.CpuPercent);
        if (telemetry.GpuPercent is double gpu)
        {
            Enqueue(_gpuSamples, gpu);
        }

        if (telemetry.FramesPerSecond is > 0)
        {
            Enqueue(_fpsSamples, telemetry.FramesPerSecond.Value);
        }
    }

    public ProfileTuningRecommendation GetRecommendation(GameProfile profile, ActiveBoostSession? session, FpsDeltaSnapshot fpsDelta)
    {
        var liveRecommendation = BuildLiveRecommendation(profile, fpsDelta);
        if (liveRecommendation is not null)
        {
            return liveRecommendation;
        }

        if (!ReferenceEquals(_lastRecommendation, ProfileTuningRecommendation.Empty))
        {
            return _lastRecommendation;
        }

        return BuildDefaultRecommendation(profile);
    }

    public void ApplyRecommendedTuning(GameProfile profile, ProfileTuningRecommendation recommendation)
    {
        switch (recommendation.Action)
        {
            case ProfileTuningAction.PushMaxFps:
                profile.BoostPreset = BoostPresetOption.MaxFps;
                profile.BoostGamePriority = true;
                profile.TrimBackgroundMemory = true;
                profile.UseBackgroundMemoryPriority = true;
                profile.UseBackgroundEcoQos = true;
                profile.EnableRecurringMaintenance = true;
                break;
            case ProfileTuningAction.PushPerformance:
                profile.BoostPreset = BoostPresetOption.Performance;
                profile.BoostGamePriority = true;
                profile.TrimBackgroundMemory = true;
                profile.UseBackgroundMemoryPriority = true;
                profile.EnableRecurringMaintenance = true;
                break;
            case ProfileTuningAction.FocusOnBackgroundCleanup:
                profile.TrimBackgroundMemory = true;
                profile.LowerBackgroundProcesses = true;
                profile.UseBackgroundMemoryPriority = true;
                profile.UseBackgroundEcoQos = true;
                profile.EnableRecurringMaintenance = true;
                break;
            case ProfileTuningAction.RelaxToBalanced:
                profile.BoostPreset = BoostPresetOption.Balanced;
                profile.UseBackgroundEcoQos = false;
                profile.EnableRecurringMaintenance = false;
                break;
            case ProfileTuningAction.HoldSteady:
            default:
                break;
        }
    }

    private void BeginSession(string sessionKey, string profileName)
    {
        CompleteSessionIfNeeded();
        _activeSessionKey = sessionKey;
        _activeProfileName = profileName;
        _cpuSamples.Clear();
        _gpuSamples.Clear();
        _fpsSamples.Clear();
    }

    private void CompleteSessionIfNeeded()
    {
        if (_activeSessionKey is null)
        {
            return;
        }

        if (_cpuSamples.Count >= MinimumSamples)
        {
            var avgCpu = AverageOrZero(_cpuSamples);
            var avgGpu = AverageOrNull(_gpuSamples);
            _lastRecommendation = new ProfileTuningRecommendation(
                "Last session signal",
                avgGpu is > 90 && avgCpu < 75
                    ? "The last session looked GPU-bound. Big preset jumps are unlikely to help as much as in-game graphics changes."
                    : avgCpu > 85
                        ? "The last session leaned CPU-heavy. Keep stronger background cleanup and priority tuning enabled."
                        : "The last session looked fairly balanced. Use the recommendation card during a live run for a sharper call.",
                ProfileTuningAction.HoldSteady,
                false);
        }

        _activeSessionKey = null;
        _activeProfileName = null;
        _cpuSamples.Clear();
        _gpuSamples.Clear();
        _fpsSamples.Clear();
    }

    private ProfileTuningRecommendation? BuildLiveRecommendation(GameProfile profile, FpsDeltaSnapshot fpsDelta)
    {
        if (_activeSessionKey is null || _cpuSamples.Count < MinimumSamples)
        {
            return null;
        }

        var avgCpu = AverageOrZero(_cpuSamples);
        var avgGpu = AverageOrNull(_gpuSamples);
        var avgFps = AverageOrNull(_fpsSamples);

        if (avgCpu >= 84 && (avgGpu is null || avgGpu <= 78))
        {
            return new ProfileTuningRecommendation(
                "Lean into CPU relief",
                $"CPU load is averaging {avgCpu:0}% while GPU load is {(avgGpu is null ? "unknown" : avgGpu.Value.ToString("0") + "%")}. Push this profile toward stronger background cleanup and Max FPS mode.",
                ProfileTuningAction.PushMaxFps,
                true);
        }

        if (avgGpu is >= 93 && avgCpu < 78)
        {
            return new ProfileTuningRecommendation(
                "You look GPU-bound",
                $"GPU load is averaging {avgGpu.Value:0}% while CPU load is {avgCpu:0}%. CloudFrame can still tidy the session, but the biggest gains will likely come from in-game graphics changes rather than a more aggressive preset.",
                ProfileTuningAction.HoldSteady,
                false);
        }

        if ((fpsDelta.DeltaPercent ?? 0) < 2
            && (!profile.TrimBackgroundMemory || !profile.UseBackgroundMemoryPriority || !profile.EnableRecurringMaintenance))
        {
            return new ProfileTuningRecommendation(
                "Strengthen background cleanup",
                "This session is not showing much measured uplift yet. Enable memory trims, lower background memory priority, and keep recurring maintenance active so CloudFrame keeps pressure off the game throughout the session.",
                ProfileTuningAction.FocusOnBackgroundCleanup,
                true);
        }

        if ((fpsDelta.DeltaPercent ?? 0) > 8 && avgCpu < 72 && avgGpu < 85 && profile.BoostPreset == BoostPresetOption.MaxFps)
        {
            return new ProfileTuningRecommendation(
                "Try a lighter preset",
                "This game is responding well already and the machine is not fully saturated. You can probably step this profile back to Performance or Balanced and keep most of the gain with less aggressive cleanup.",
                ProfileTuningAction.RelaxToBalanced,
                true);
        }

        return new ProfileTuningRecommendation(
            "Current tuning looks healthy",
            $"CloudFrame is seeing a reasonably balanced session so far{(avgFps is > 0 ? $" at about {avgFps.Value:0} FPS" : string.Empty)}. Keep the current profile settings and let the session gather a few more samples before making a bigger move.",
            ProfileTuningAction.HoldSteady,
            false);
    }

    private static ProfileTuningRecommendation BuildDefaultRecommendation(GameProfile profile)
    {
        var detail = profile.BoostPreset switch
        {
            BoostPresetOption.Balanced => "Balanced is a good starting point. If gains stay flat, Performance is the next clean step.",
            BoostPresetOption.Performance => "Performance is the default sweet spot. If CPU pressure stays high, Max FPS is worth testing next.",
            BoostPresetOption.MaxFps => "Max FPS is already the strongest safe preset. Watch the live session for a CPU-heavy or GPU-heavy recommendation before changing it.",
            _ => "Start a boosted session so CloudFrame can recommend profile-specific tuning from real telemetry."
        };

        return new ProfileTuningRecommendation("Need a live session", detail, ProfileTuningAction.HoldSteady, false);
    }

    private static void Enqueue(Queue<double> queue, double value)
    {
        queue.Enqueue(value);
        while (queue.Count > MaxSamples)
        {
            queue.Dequeue();
        }
    }

    private static double AverageOrZero(IEnumerable<double> values)
    {
        var materialized = values as double[] ?? values.ToArray();
        return materialized.Length == 0 ? 0 : materialized.Average();
    }

    private static double? AverageOrNull(IEnumerable<double> values)
    {
        var materialized = values as double[] ?? values.ToArray();
        return materialized.Length == 0 ? null : materialized.Average();
    }
}

internal enum ProfileTuningAction
{
    HoldSteady,
    PushPerformance,
    PushMaxFps,
    FocusOnBackgroundCleanup,
    RelaxToBalanced
}

internal sealed record ProfileTuningRecommendation(
    string Title,
    string Detail,
    ProfileTuningAction Action,
    bool CanAutoApply)
{
    public static ProfileTuningRecommendation Empty { get; } = new(
        "Need a live session",
        "Start a boosted session so CloudFrame can recommend profile-specific tuning from real telemetry.",
        ProfileTuningAction.HoldSteady,
        false);
}

internal static class ProfileTuningRecommendationExtensions
{
    public static string ToStorageValue(this ProfileTuningAction action)
    {
        return action.ToString();
    }
}
