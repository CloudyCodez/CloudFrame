using System.Diagnostics;
using FrameBoost.Models;

namespace FrameBoost.Services;

internal sealed class GameDetectionService : IDisposable
{
    private readonly Func<IReadOnlyList<GameProfile>> _profilesProvider;
    private readonly ProcessService _processService;
    private readonly Logger _logger;
    private readonly System.Windows.Forms.Timer _timer;
    private Dictionary<int, DetectedGame> _currentGames = [];

    public GameDetectionService(Func<IReadOnlyList<GameProfile>> profilesProvider, ProcessService processService, Logger logger)
    {
        _profilesProvider = profilesProvider;
        _processService = processService;
        _logger = logger;
        _timer = new System.Windows.Forms.Timer
        {
            Interval = 2500
        };
        _timer.Tick += (_, _) => Scan();
    }

    public event EventHandler<IReadOnlyList<DetectedGame>>? SnapshotUpdated;

    public event EventHandler<DetectedGame>? GameDetected;

    public bool IsRunning => _timer.Enabled;

    public void Start()
    {
        Scan();
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        _currentGames = [];
        SnapshotUpdated?.Invoke(this, []);
    }

    public void ForceScan()
    {
        Scan();
    }

    private void Scan()
    {
        var profiles = _profilesProvider()
            .Where(static profile => !string.IsNullOrWhiteSpace(profile.ExecutablePath))
            .ToList();

        if (profiles.Count == 0)
        {
            _currentGames = [];
            SnapshotUpdated?.Invoke(this, []);
            return;
        }

        var nextSnapshot = new Dictionary<int, DetectedGame>();
        var profilesByName = profiles
            .GroupBy(static profile => profile.ExecutableName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.HasExited)
                {
                    continue;
                }

                if (!profilesByName.TryGetValue(process.ProcessName, out var candidateProfiles))
                {
                    continue;
                }

                if (!TryMatchProfile(process, candidateProfiles, out var profile, out var executablePath))
                {
                    continue;
                }

                var resolvedGameProcess = _processService.ResolveGameProcess(profile!, process.Id);
                var effectiveProcessId = resolvedGameProcess?.ProcessId ?? process.Id;
                var effectiveProcessName = resolvedGameProcess?.ProcessName ?? process.ProcessName;
                var effectiveExecutablePath = resolvedGameProcess?.ExecutablePath ?? executablePath;

                var detected = new DetectedGame
                {
                    Profile = profile!,
                    ProcessId = effectiveProcessId,
                    ProcessName = effectiveProcessName,
                    ExecutablePath = effectiveExecutablePath,
                    AnchorProcessId = process.Id,
                    AnchorProcessName = process.ProcessName,
                    EngineHint = resolvedGameProcess?.EngineHint ?? _processService.GetEngineHint(profile!, effectiveExecutablePath, effectiveProcessName)
                };

                nextSnapshot[effectiveProcessId] = detected;

                if (!_currentGames.ContainsKey(effectiveProcessId))
                {
                    if (detected.HasLauncherHandoff)
                    {
                        _logger.Log($"Detected profiled game '{profile!.Name}' through launcher PID {detected.AnchorProcessId}, resolved to game PID {detected.ProcessId}{(string.IsNullOrWhiteSpace(detected.EngineHint) ? string.Empty : $" ({detected.EngineHint})")}.");
                    }
                    else
                    {
                        _logger.Log($"Detected profiled game '{profile!.Name}' (PID {detected.ProcessId}){(string.IsNullOrWhiteSpace(detected.EngineHint) ? string.Empty : $" ({detected.EngineHint})")}.");
                    }

                    GameDetected?.Invoke(this, detected);
                }
            }
        }

        _currentGames = nextSnapshot;
        SnapshotUpdated?.Invoke(this, _currentGames.Values.OrderBy(static item => item.Profile.Name).ToList());
    }

    private bool TryMatchProfile(
        Process process,
        IReadOnlyList<GameProfile> candidateProfiles,
        out GameProfile? profile,
        out string? executablePath)
    {
        executablePath = null;

        if (!_processService.TryGetExecutablePath(process, out executablePath))
        {
            profile = candidateProfiles.FirstOrDefault(candidate =>
                string.Equals(candidate.ExecutableName, process.ProcessName, StringComparison.OrdinalIgnoreCase));
            return profile is not null;
        }

        var capturedPath = executablePath;
        profile = candidateProfiles.FirstOrDefault(candidate =>
            string.Equals(candidate.ExecutablePath, capturedPath, StringComparison.OrdinalIgnoreCase));

        if (profile is not null)
        {
            return true;
        }

        profile = candidateProfiles.FirstOrDefault(candidate =>
            string.Equals(candidate.ExecutableName, process.ProcessName, StringComparison.OrdinalIgnoreCase));

        return profile is not null;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
