using System.Diagnostics;
using FrameBoost.Models;

namespace FrameBoost.Services;

internal sealed class FrameGenPrototypeService
{
    private readonly GpuTechnologyAdvisorService _gpuTechnologyAdvisorService;
    private readonly AntiCheatCompatibilityService _antiCheatCompatibilityService;
    private readonly WindowResolutionService _windowResolutionService;

    public FrameGenPrototypeService(
        GpuTechnologyAdvisorService gpuTechnologyAdvisorService,
        AntiCheatCompatibilityService antiCheatCompatibilityService,
        WindowResolutionService windowResolutionService)
    {
        _gpuTechnologyAdvisorService = gpuTechnologyAdvisorService;
        _antiCheatCompatibilityService = antiCheatCompatibilityService;
        _windowResolutionService = windowResolutionService;
    }

    public FrameGenPrototypeReport Evaluate(GameProfile? selectedProfile, ActiveBoostSession? activeSession, AppSettings settings)
    {
        var profile = activeSession?.Profile ?? selectedProfile;
        if (profile is null || string.IsNullOrWhiteSpace(profile.ExecutablePath))
        {
            return new FrameGenPrototypeReport
            {
                Summary = "Select a saved game profile first.",
                Detail = "Frame Gen Lab only evaluates real game profiles because it needs a launch path, executable identity, and anti-cheat context.",
                CanLaunchPrototype = false,
                CanPrepareLiveWindow = false,
                IsBlocked = true
            };
        }

        var antiCheat = _antiCheatCompatibilityService.Evaluate(profile.ExecutablePath);
        if (settings.FrameGenDisableOnAntiCheat && antiCheat.IsDetected)
        {
            return new FrameGenPrototypeReport
            {
                Profile = profile,
                AntiCheatStatus = antiCheat,
                Summary = $"Blocked by {antiCheat.DisplayName}.",
                Detail = "CloudFrame keeps the frame-gen lab disabled on anti-cheat titles. The safe path is to leave those games on normal monitoring and boost-only behavior.",
                CanLaunchPrototype = false,
                CanPrepareLiveWindow = false,
                IsBlocked = true
            };
        }

        var techReport = _gpuTechnologyAdvisorService.GetReport();
        var backendReason = GetBackendBlockReason(settings.FrameGenBackend, techReport);
        if (!string.IsNullOrWhiteSpace(backendReason))
        {
            return new FrameGenPrototypeReport
            {
                Profile = profile,
                AntiCheatStatus = antiCheat,
                Summary = "Backend not viable on this rig.",
                Detail = backendReason,
                CanLaunchPrototype = false,
                CanPrepareLiveWindow = false,
                IsBlocked = true
            };
        }

        var match = FindMatchingWindow(profile, activeSession);
        if (match is null)
        {
            return new FrameGenPrototypeReport
            {
                Profile = profile,
                AntiCheatStatus = antiCheat,
                Summary = "Ready to launch a prototype session.",
                Detail = settings.FrameGenRequireBorderless
                    ? "CloudFrame will look for the launched game's top-level window and force it into a borderless low-overhead shape before future external interpolation work."
                    : "CloudFrame can launch the game into the experimental lab flow when you are ready.",
                CanLaunchPrototype = File.Exists(profile.ExecutablePath),
                CanPrepareLiveWindow = false,
                IsBlocked = false
            };
        }

        if (settings.FrameGenRequireBorderless)
        {
            return new FrameGenPrototypeReport
            {
                Profile = profile,
                AntiCheatStatus = antiCheat,
                MatchingProcess = match.Value.Process,
                MatchingWindow = match.Value.Window,
                Summary = $"Live window ready: {match.Value.Process.ProcessName}.",
                Detail = "CloudFrame can prep this live window into the borderless low-overhead shape needed for an eventual external interpolation path.",
                CanLaunchPrototype = true,
                CanPrepareLiveWindow = true,
                IsBlocked = false
            };
        }

        return new FrameGenPrototypeReport
        {
            Profile = profile,
            AntiCheatStatus = antiCheat,
            MatchingProcess = match.Value.Process,
            MatchingWindow = match.Value.Window,
            Summary = $"Live window detected: {match.Value.Process.ProcessName}.",
            Detail = "The game is already running and eligible for the prototype lab workflow.",
            CanLaunchPrototype = true,
            CanPrepareLiveWindow = true,
            IsBlocked = false
        };
    }

    public async Task<FrameGenPrototypeActionResult> LaunchPrototypeSessionAsync(FrameGenPrototypeReport report, AppSettings settings)
    {
        if (report.Profile is null || string.IsNullOrWhiteSpace(report.Profile.ExecutablePath) || !File.Exists(report.Profile.ExecutablePath))
        {
            return new FrameGenPrototypeActionResult(false, "The selected profile does not have a valid executable path.");
        }

        if (report.IsBlocked)
        {
            return new FrameGenPrototypeActionResult(false, report.Detail);
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = report.Profile.ExecutablePath,
                WorkingDirectory = Path.GetDirectoryName(report.Profile.ExecutablePath),
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            return new FrameGenPrototypeActionResult(false, $"CloudFrame could not launch the profile: {ex.Message}");
        }

        var deadline = DateTimeOffset.Now.AddSeconds(15);
        while (DateTimeOffset.Now < deadline)
        {
            await Task.Delay(600).ConfigureAwait(false);
            var updatedReport = Evaluate(report.Profile, null, settings);
            if (updatedReport.CanPrepareLiveWindow)
            {
                return await PrepareLiveWindowAsync(updatedReport, settings).ConfigureAwait(false);
            }
        }

        return new FrameGenPrototypeActionResult(true, "Prototype session launched. The game window was not ready for borderless prep yet, so open the lab again once the game is fully visible.");
    }

    public async Task<FrameGenPrototypeActionResult> PrepareLiveWindowAsync(FrameGenPrototypeReport report, AppSettings settings)
    {
        if (report.IsBlocked)
        {
            return new FrameGenPrototypeActionResult(false, report.Detail);
        }

        if (report.MatchingWindow is null)
        {
            return new FrameGenPrototypeActionResult(false, "No live top-level game window is available yet.");
        }

        var window = report.MatchingWindow;
        return await Task.Run(() =>
        {
            if (!_windowResolutionService.TryGetWindowBounds(window.Handle, out var bounds, out var boundsError))
            {
                return new FrameGenPrototypeActionResult(false, boundsError ?? "CloudFrame could not read the live window bounds.");
            }

            var success = _windowResolutionService.TryApplyWindowLayout(
                window.Handle,
                bounds.Width,
                bounds.Height,
                settings.FrameGenRequireBorderless,
                out var error);

            if (!success)
            {
                return new FrameGenPrototypeActionResult(false, error ?? "CloudFrame could not prepare the live window.");
            }

            var suffix = settings.FrameGenRequireBorderless ? "borderless low-overhead prep applied" : "prototype prep applied";
            return new FrameGenPrototypeActionResult(true, $"{window.ProcessName} is ready — {suffix}.");
        }).ConfigureAwait(false);
    }

    private (RunningProcessEntry Process, WindowTargetEntry Window)? FindMatchingWindow(GameProfile profile, ActiveBoostSession? activeSession)
    {
        var windowsByProcess = _windowResolutionService.ListWindowedProcesses();
        if (windowsByProcess.Count == 0)
        {
            return null;
        }

        var activeProcessId = activeSession?.RecoveryState.GameProcessId ?? 0;
        RunningProcessEntry? matchingProcess = null;

        if (activeProcessId > 0)
        {
            matchingProcess = windowsByProcess.FirstOrDefault(entry => entry.ProcessId == activeProcessId);
        }

        matchingProcess ??= windowsByProcess.FirstOrDefault(entry =>
            MatchesExecutablePath(entry.ExecutablePath, profile.ExecutablePath)
            || string.Equals(
                Path.GetFileNameWithoutExtension(entry.ExecutablePath),
                profile.ExecutableName,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(entry.ProcessName, profile.ExecutableName, StringComparison.OrdinalIgnoreCase));

        if (matchingProcess is null)
        {
            return null;
        }

        var windows = _windowResolutionService.ListWindowsForProcess(matchingProcess.ProcessId);
        var matchingWindow = windows
            .OrderByDescending(static window => window.WindowTitle.Length)
            .FirstOrDefault();

        if (matchingWindow is null)
        {
            return null;
        }

        return (matchingProcess, matchingWindow);
    }

    private static bool MatchesExecutablePath(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetBackendBlockReason(FrameGenBackend backend, GpuTechnologyReport report)
    {
        return backend switch
        {
            FrameGenBackend.NvidiaOpticalFlow when !report.GpuNames.Any(name => name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)) =>
                "NVIDIA Optical Flow was selected, but no NVIDIA GPU was detected on this rig.",
            _ => null
        };
    }
}

internal sealed class FrameGenPrototypeReport
{
    public GameProfile? Profile { get; init; }

    public AntiCheatStatus AntiCheatStatus { get; init; } = AntiCheatStatus.None;

    public RunningProcessEntry? MatchingProcess { get; init; }

    public WindowTargetEntry? MatchingWindow { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public bool CanLaunchPrototype { get; init; }

    public bool CanPrepareLiveWindow { get; init; }

    public bool IsBlocked { get; init; }
}

internal readonly record struct FrameGenPrototypeActionResult(bool Success, string Message);
