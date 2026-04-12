using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FrameBoost.Core;
namespace FrameBoost.Services;

internal sealed class PresentMonFpsService : IDisposable
{
    private const string PresentMonExeName = "PresentMon-2.4.1-x64.exe";
    private static readonly string SessionName = $"CloudFrame-FPS-{Environment.ProcessId}";
    private static readonly TimeSpan SampleFreshness = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan RestartCooldown = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan TargetPidRefreshInterval = TimeSpan.FromSeconds(1);
    private readonly Logger _logger;
    private readonly object _sync = new();
    private readonly SemaphoreSlim _refreshSignal = new(0, int.MaxValue);
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Task _refreshWorker;
    private readonly string? _presentMonPath;

    private Process? _presentMonProcess;
    private CancellationTokenSource? _captureCts;
    private bool _disposed;
    private bool _captureActive;
    private bool _refreshQueued;
    private int _sessionVersion;

    private int? _targetProcessId;
    private string? _targetProcessName;
    private string? _targetProcessMatchName;
    private bool _compatibilityMode;
    private string? _compatibilityMessage;
    private bool _foregroundMode; // track foreground window even without a boosted PID

    private double? _latestFps;
    private DateTimeOffset _lastSampleAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastStartFailureAt = DateTimeOffset.MinValue;
    private string _status = "Waiting for a boosted game";
    private string? _lastError;
    private bool _loggedFirstSample;
    private HashSet<int> _acceptedTargetProcessIds = [];
    private DateTimeOffset _acceptedTargetPidsSampledAt = DateTimeOffset.MinValue;

    // Cached foreground PID — refreshed on a slow poll so per-frame Win32 calls
    // don't race against the CSV reader and drop legitimate frames.
    private int _cachedForegroundPid;
    private DateTimeOffset _foregroundPidSampledAt = DateTimeOffset.MinValue;
    private static readonly int OwnProcessId = Environment.ProcessId;
    public PresentMonFpsService(Logger logger)
    {
        _logger = logger;
        _presentMonPath = TryResolvePresentMonPath();

        if (_presentMonPath is null)
        {
            _logger.Log("PresentMon FPS backend could not be found in the app directory.");
            _status = "FPS unavailable: PresentMon was not found.";
        }
        else
        {
            _logger.Log($"Official PresentMon FPS backend detected at {_presentMonPath}.");
            TerminateStalePresentMonProcesses(_presentMonPath);
            KillStaleEtwSessions(_presentMonPath);
        }

        _refreshWorker = Task.Run(ProcessRefreshLoopAsync);
    }

    public double? LatestFps
    {
        get
        {
            lock (_sync)
            {
                return DateTimeOffset.UtcNow - _lastSampleAt <= SampleFreshness
                    ? _latestFps
                    : null;
            }
        }
    }

    public bool IsBackendAvailable => _presentMonPath is not null;

    public string? BackendPath => _presentMonPath;

    public string Status
    {
        get
        {
            lock (_sync)
            {
                if (_targetProcessId is null or <= 0 && !_foregroundMode)
                {
                    return "Waiting for a boosted game";
                }

                if (DateTimeOffset.UtcNow - _lastSampleAt <= SampleFreshness && _latestFps is double)
                {
                    return _status;
                }

                if (!string.IsNullOrWhiteSpace(_lastError))
                {
                    return $"FPS unavailable: {_lastError}";
                }

                return _status;
            }
        }
    }

    /// <summary>
    /// Enable global foreground-mode FPS capture so the overlay shows live FPS
    /// for whatever window has focus, even without an active boost session.
    /// </summary>
    public void SetForegroundMode(bool enabled)
    {
        lock (_sync)
        {
            if (_disposed || _foregroundMode == enabled) return;
            _foregroundMode = enabled;
            if (!enabled && _targetProcessId is null or <= 0)
            {
                _latestFps = null;
                _lastSampleAt = DateTimeOffset.MinValue;
                _status = "Waiting for a boosted game";
                _lastAcceptedGamePid = 0;
            }
        }
        RequestRefresh();
    }

    public void SetTarget(
        int? processId,
        string? processDisplayName,
        string? processMatchName,
        bool compatibilityMode,
        string? compatibilityMessage)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _compatibilityMode = compatibilityMode;
            _compatibilityMessage = compatibilityMessage;

            if (compatibilityMode)
            {
                // PresentMon 2.4.1 uses ETW global capture — no process injection,
                // so it is safe with EAC, BattlEye, and other anti-cheat systems.
                // Keep tracking the PID so ReadStdoutAsync can match frames.
                _targetProcessId = processId is int validCompat && validCompat > 0 ? validCompat : null;
                _targetProcessName = string.IsNullOrWhiteSpace(processDisplayName) ? processMatchName : processDisplayName;
                _targetProcessMatchName = processMatchName;
                ResetAcceptedTargetProcessIds();
                _latestFps = null;
                _lastSampleAt = DateTimeOffset.MinValue;
                _lastError = null;
                _status = $"Tracking FPS for {_targetProcessName ?? processMatchName ?? "game"}";
            }
            else if (processId is int validProcessId && validProcessId > 0)
            {
                var targetChanged = _targetProcessId != validProcessId;
                _targetProcessId = validProcessId;
                _targetProcessName = string.IsNullOrWhiteSpace(processDisplayName) ? processMatchName : processDisplayName;
                _targetProcessMatchName = processMatchName;
                if (targetChanged)
                {
                    ResetAcceptedTargetProcessIds();
                }
                _lastError = null;

                if (DateTimeOffset.UtcNow - _lastSampleAt > SampleFreshness || _latestFps is null)
                {
                    _latestFps = null;
                    _status = $"Tracking FPS for {_targetProcessName ?? _targetProcessMatchName ?? "game"}";
                }
            }
            else
            {
                _targetProcessId = null;
                _targetProcessName = null;
                _targetProcessMatchName = null;
                ResetAcceptedTargetProcessIds();
                _lastError = null;
                if (!_foregroundMode)
                {
                    _latestFps = null;
                    _lastSampleAt = DateTimeOffset.MinValue;
                }
                _status = _foregroundMode ? "Waiting for FPS data" : "Waiting for a boosted game";
            }

        }

        RequestRefresh();
    }

    private async Task ProcessRefreshLoopAsync()
    {
        try
        {
            while (true)
            {
                await _refreshSignal.WaitAsync(_disposeCts.Token).ConfigureAwait(false);

                while (_refreshSignal.Wait(0))
                {
                }

                CaptureSnapshot snapshot;
                lock (_sync)
                {
                    _refreshQueued = false;
                    snapshot = new CaptureSnapshot(
                        _presentMonPath,
                        _targetProcessId,
                        _targetProcessName,
                        _compatibilityMode,
                        _compatibilityMessage,
                        _foregroundMode,
                        _captureActive,
                        _disposed);
                }

                if (snapshot.IsDisposed)
                {
                    return;
                }

                // Compatibility mode no longer gates FPS capture — PresentMon 2.4.1
                // uses ETW global capture with no injection, safe with all anti-cheat.

                if (snapshot.PresentMonPath is null)
                {
                    lock (_sync)
                    {
                        _status = "FPS unavailable: PresentMon was not found.";
                    }

                    StopCaptureCore();
                    continue;
                }

                // Run capture if we have a boosted PID OR foreground mode is on
                if (snapshot.TargetProcessId is null or <= 0 && !snapshot.ForegroundMode)
                {
                    StopCaptureCore();
                    continue;
                }

                if (snapshot.CaptureAlreadyActive)
                {
                    continue;
                }

                if (DateTimeOffset.UtcNow - _lastStartFailureAt < RestartCooldown)
                {
                    continue;
                }

                StartCapture(snapshot.PresentMonPath);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void StartCapture(string presentMonPath)
    {
        Process? process = null;
        CancellationTokenSource? captureCts = null;
        var sessionVersion = 0;
        string targetLabel;

        lock (_sync)
        {
            // Allow capture when foreground mode is on even if no boosted PID is set
            if (_disposed || _captureActive || _compatibilityMode)
            {
                return;
            }

            if (_targetProcessId is null or <= 0 && !_foregroundMode)
            {
                return;
            }

            targetLabel = _targetProcessName ?? _targetProcessMatchName ?? "foreground window";
            _captureActive = true;
            _loggedFirstSample = false;
            _lastError = null;
            _status = _latestFps is double && DateTimeOffset.UtcNow - _lastSampleAt <= SampleFreshness
                ? $"Tracking FPS for {targetLabel}"
                : $"Sampling FPS for {targetLabel}";
            sessionVersion = ++_sessionVersion;
            _captureCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
            captureCts = _captureCts;
        }

        try
        {
            TerminateStalePresentMonProcesses(presentMonPath);
            KillStaleEtwSessions(presentMonPath);

            // Run global capture (no --process_id). The CSV reader filters by
            // foreground window PID so launcher-wrapped games are handled automatically.
            // --stop_existing_session cleans up any orphaned ETW session from a
            // previous crash so we never get exit code 6.
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = presentMonPath,
                    Arguments =
                        $"--session_name {SessionName} " +
                        "--stop_existing_session " +
                        "--output_stdout " +
                        "--no_console_stats " +
                        "--exclude_dropped",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("PresentMon did not start.");
            }

            lock (_sync)
            {
                if (_disposed || sessionVersion != _sessionVersion)
                {
                    process.Kill(entireProcessTree: true);
                    process.Dispose();
                    return;
                }

                _presentMonProcess = process;
            }

            _logger.Log($"PresentMon started in global capture mode for '{targetLabel}'.");

            _ = Task.Run(() => ReadStdoutAsync(process, sessionVersion, captureCts!.Token));
            _ = Task.Run(() => ReadStderrAsync(process, sessionVersion, captureCts!.Token));
            _ = Task.Run(() => WaitForExitAsync(process, sessionVersion, captureCts!.Token));
        }
        catch (Exception ex)
        {
            lock (_sync)
            {
                _captureActive = false;
                _lastStartFailureAt = DateTimeOffset.UtcNow;
                _lastError = ex.Message;
                _status = $"FPS unavailable: {ex.Message}";
                _captureCts?.Dispose();
                _captureCts = null;
            }

            process?.Dispose();
            _logger.Log($"Failed to start PresentMon: {ex.Message}");
        }
    }

    private async Task ReadStderrAsync(Process process, int sessionVersion, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await process.StandardError.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith("warning:", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Log($"PresentMon warning: {line["warning:".Length..].Trim()}");
                    continue;
                }

                var error = CleanupErrorLine(line);
                lock (_sync)
                {
                    if (_disposed || sessionVersion != _sessionVersion)
                    {
                        return;
                    }

                    _lastError = error;
                    _status = $"FPS unavailable: {error}";
                }

                _logger.Log($"PresentMon error: {error}");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            lock (_sync)
            {
                if (sessionVersion == _sessionVersion && !_disposed)
                {
                    _lastError = ex.Message;
                }
            }
        }
    }

    private async Task ReadStdoutAsync(Process process, int sessionVersion, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, int>? headerMap = null;
        var headerLogged = false;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (headerMap is null)
                {
                    if (!LooksLikeCsvHeader(line))
                    {
                        continue;
                    }

                    headerMap = BuildHeaderMap(ParseCsv(line));
                    if (!headerLogged)
                    {
                        headerLogged = true;
                        _logger.Log($"PresentMon header: {line}");
                    }

                    continue;
                }

                if (!TryParseSampleLine(line, headerMap, out var sample))
                {
                    continue;
                }

                // Filter: prefer frames from the explicitly boosted target PID first.
                // This handles launcher-wrapped games (e.g. Outlaws via EA launcher)
                // where the renderer PID differs from the foreground (launcher) PID.
                // Fall back to foreground-PID filtering when no target is set.
                //
                // Special case: when CloudFrame itself is the foreground window (user
                // alt-tabbed to check the app or take a screenshot), we use the last
                // accepted game PID so FPS stays live instead of dropping to --.
                if (sample.ProcessId.HasValue)
                {
                    var pid = sample.ProcessId.Value;
                    if (pid == OwnProcessId) continue;  // never show CloudFrame's own frames

                    var acceptedTargetPids = GetAcceptedTargetProcessIds();
                    if (acceptedTargetPids.Count > 0)
                    {
                        if (!acceptedTargetPids.Contains(pid))
                        {
                            continue;
                        }

                        _lastAcceptedGamePid = pid;
                    }
                    else
                    {
                        var fgPid = GetCachedForegroundProcessId();

                        // If CloudFrame is the foreground, use the last game PID we saw.
                        int effectiveFgPid = (fgPid == OwnProcessId || fgPid == 0)
                            ? _lastAcceptedGamePid
                            : fgPid;

                        if (effectiveFgPid > 0 && pid != effectiveFgPid)
                            continue;  // not the focused window and not the boosted target

                        if (effectiveFgPid > 0 && pid == effectiveFgPid)
                            _lastAcceptedGamePid = pid;  // keep cache fresh
                    }
                }

                lock (_sync)
                {
                    if (_disposed || sessionVersion != _sessionVersion)
                    {
                        return;
                    }

                    _latestFps = sample.Fps;
                    _lastSampleAt = DateTimeOffset.UtcNow;
                    _lastError = null;
                    _status = $"Tracking FPS for {_targetProcessName ?? _targetProcessMatchName ?? sample.Application ?? "game"}";

                    if (!_loggedFirstSample)
                    {
                        _loggedFirstSample = true;
                        _logger.Log($"PresentMon first FPS sample: {sample.Fps:0.0} FPS from '{sample.Application ?? "unknown"}' (PID {sample.ProcessId?.ToString() ?? "unknown"})");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            lock (_sync)
            {
                if (sessionVersion == _sessionVersion && !_disposed)
                {
                    _lastError = ex.Message;
                }
            }

            _logger.Log($"PresentMon stdout reader failed: {ex.Message}");
        }
    }

    private async Task WaitForExitAsync(Process process, int sessionVersion, CancellationToken cancellationToken)
    {
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
        }

        var shouldRestart = false;
        var exitCode = process.HasExited ? process.ExitCode : 0;
        lock (_sync)
        {
            if (_disposed || sessionVersion != _sessionVersion)
            {
                return;
            }

            _captureActive = false;
            _presentMonProcess?.Dispose();
            _presentMonProcess = null;
            _captureCts?.Dispose();
            _captureCts = null;

            // Only apply the restart cooldown on failure (non-zero exit).
            // Clean exits in foreground mode should restart immediately.
            if (exitCode != 0)
            {
                _lastStartFailureAt = DateTimeOffset.UtcNow;
            }

            // Restart if we have a boosted PID OR foreground mode is keeping capture alive.
            if ((_targetProcessId is not null && !_compatibilityMode) || _foregroundMode)
            {
                if (_latestFps is double && DateTimeOffset.UtcNow - _lastSampleAt <= SampleFreshness)
                {
                    _lastError = null;
                    _status = $"Tracking FPS for {_targetProcessName ?? _targetProcessMatchName ?? "foreground window"}";
                }
                else if (exitCode != 0)
                {
                    _lastError ??= $"PresentMon exited ({exitCode})";
                    _status = $"FPS unavailable: {_lastError}";
                }
                else
                {
                    _lastError = null;
                    _status = _foregroundMode
                        ? "Waiting for FPS data"
                        : $"Sampling FPS for {_targetProcessName ?? _targetProcessMatchName ?? "game"}";
                }

                shouldRestart = true;
            }
        }

        _logger.Log($"PresentMon exited with code {exitCode}.");

        if (shouldRestart && !_disposeCts.IsCancellationRequested)
        {
            await Task.Delay(500, _disposeCts.Token).ConfigureAwait(false);
            RequestRefresh();
        }
    }

    // Last non-CloudFrame PID that we accepted a frame from.
    // Used to keep FPS live when the user focuses CloudFrame itself.
    private int _lastAcceptedGamePid;

    private int GetCachedForegroundProcessId()
    {
        var now = DateTimeOffset.UtcNow;
        // Only hit Win32 at most once per poll interval
        if (now - _foregroundPidSampledAt >= ForegroundPollInterval)
        {
            var pid = GetForegroundProcessId();
            _cachedForegroundPid = pid;
            _foregroundPidSampledAt = now;
        }
        return _cachedForegroundPid;
    }

    private HashSet<int> GetAcceptedTargetProcessIds()
    {
        lock (_sync)
        {
            if (_targetProcessId is not int targetProcessId || targetProcessId <= 0)
            {
                return [];
            }

            var now = DateTimeOffset.UtcNow;
            if (now - _acceptedTargetPidsSampledAt < TargetPidRefreshInterval && _acceptedTargetProcessIds.Count > 0)
            {
                return _acceptedTargetProcessIds;
            }

            _acceptedTargetProcessIds = BuildAcceptedTargetProcessIds(targetProcessId);
            _acceptedTargetPidsSampledAt = now;
            return _acceptedTargetProcessIds;
        }
    }

    private void ResetAcceptedTargetProcessIds()
    {
        _acceptedTargetProcessIds = [];
        _acceptedTargetPidsSampledAt = DateTimeOffset.MinValue;
    }

    private static HashSet<int> BuildAcceptedTargetProcessIds(int targetProcessId)
    {
        var accepted = new HashSet<int> { targetProcessId };
        foreach (var descendant in EnumerateDescendants(targetProcessId))
        {
            accepted.Add(descendant.ProcessId);
        }

        return accepted;
    }

    private static int GetForegroundProcessId()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return 0;
            GetWindowThreadProcessId(hwnd, out var pid);
            return (int)pid;
        }
        catch
        {
            return 0;
        }
    }

    private static IEnumerable<ProcessTreeEntry> EnumerateDescendants(int rootProcessId)
    {
        var snapshot = CreateToolhelp32Snapshot(Th32csSnapprocess, 0);
        if (snapshot == InvalidHandleValue)
        {
            yield break;
        }

        try
        {
            var entry = new ProcessEntry32
            {
                DwSize = (uint)Marshal.SizeOf<ProcessEntry32>()
            };

            var entries = new List<ProcessTreeEntry>();
            if (Process32First(snapshot, ref entry))
            {
                do
                {
                    entries.Add(new ProcessTreeEntry((int)entry.Th32ProcessId, (int)entry.Th32ParentProcessID, entry.SzExeFile));
                    entry.DwSize = (uint)Marshal.SizeOf<ProcessEntry32>();
                }
                while (Process32Next(snapshot, ref entry));
            }

            var childrenByParent = entries
                .GroupBy(static candidate => candidate.ParentProcessId)
                .ToDictionary(static group => group.Key, static group => group.ToList());

            var queue = new Queue<int>();
            var visited = new HashSet<int>();
            queue.Enqueue(rootProcessId);

            while (queue.Count > 0)
            {
                var parentProcessId = queue.Dequeue();
                if (!visited.Add(parentProcessId))
                {
                    continue;
                }

                if (!childrenByParent.TryGetValue(parentProcessId, out var children))
                {
                    continue;
                }

                foreach (var child in children)
                {
                    yield return child;
                    queue.Enqueue(child.ProcessId);
                }
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    private void StopCaptureCore()
    {
        Process? process = null;
        CancellationTokenSource? captureCts = null;

        lock (_sync)
        {
            if (!_captureActive && _presentMonProcess is null && _captureCts is null)
            {
                return;
            }

            _sessionVersion++;
            _captureActive = false;
            _latestFps = null;
            _lastSampleAt = DateTimeOffset.MinValue;
            ResetAcceptedTargetProcessIds();
            process = _presentMonProcess;
            captureCts = _captureCts;
            _presentMonProcess = null;
            _captureCts = null;
        }

        try
        {
            captureCts?.Cancel();
        }
        catch
        {
        }

        captureCts?.Dispose();

        if (process is not null)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(2000);
                }
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private void RequestRefresh()
    {
        if (_disposed)
        {
            return;
        }

        var releaseSignal = false;
        lock (_sync)
        {
            if (!_refreshQueued)
            {
                _refreshQueued = true;
                releaseSignal = true;
            }
        }

        if (releaseSignal)
        {
            try
            {
                _refreshSignal.Release();
            }
            catch
            {
            }
        }
    }

    private static bool LooksLikeCsvHeader(string line)
    {
        return line.Contains("Application", StringComparison.OrdinalIgnoreCase)
            && line.Contains("ProcessID", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, int> BuildHeaderMap(IReadOnlyList<string> headers)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Count; index++)
        {
            var normalized = NormalizeHeader(headers[index]);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                result[normalized] = index;
            }
        }

        return result;
    }

    private static bool TryParseSampleLine(string line, IReadOnlyDictionary<string, int> headerMap, out FpsSample sample)
    {
        sample = default;
        var values = ParseCsv(line);
        if (values.Count == 0)
        {
            return false;
        }

        if (!TryReadDouble(values, headerMap, out var fps))
        {
            return false;
        }

        var processId = TryReadInt(values, headerMap);
        var application = TryReadString(values, headerMap, "application");

        sample = new FpsSample(processId, application, fps);
        return true;
    }

    private static bool TryReadDouble(IReadOnlyList<string> values, IReadOnlyDictionary<string, int> headerMap, out double value)
    {
        value = 0;

        if (TryReadDouble(values, headerMap, "avgfps", out value))
        {
            return value > 0;
        }

        if (TryReadDouble(values, headerMap, "fps", out value))
        {
            return value > 0;
        }

        if (TryReadDouble(values, headerMap, "msbetweenpresents", out var msBetweenPresents) && msBetweenPresents > 0)
        {
            value = 1000d / msBetweenPresents;
            return double.IsFinite(value) && value > 0;
        }

        if (TryReadDouble(values, headerMap, "msbetweendisplaychange", out var msBetweenDisplay) && msBetweenDisplay > 0)
        {
            value = 1000d / msBetweenDisplay;
            return double.IsFinite(value) && value > 0;
        }

        return false;
    }

    private static bool TryReadDouble(IReadOnlyList<string> values, IReadOnlyDictionary<string, int> headerMap, string columnName, out double value)
    {
        value = 0;
        if (!headerMap.TryGetValue(columnName, out var index) || index < 0 || index >= values.Count)
        {
            return false;
        }

        var raw = values[index];
        return double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static int? TryReadInt(IReadOnlyList<string> values, IReadOnlyDictionary<string, int> headerMap)
    {
        if (!headerMap.TryGetValue("processid", out var index) || index < 0 || index >= values.Count)
        {
            return null;
        }

        return int.TryParse(values[index], out var parsed) ? parsed : null;
    }

    private static string? TryReadString(IReadOnlyList<string> values, IReadOnlyDictionary<string, int> headerMap, string columnName)
    {
        if (!headerMap.TryGetValue(columnName, out var index) || index < 0 || index >= values.Count)
        {
            return null;
        }

        var value = values[index]?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string NormalizeHeader(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> ParseCsv(string line)
    {
        var values = new List<string>();
        var builder = new StringBuilder();
        var insideQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (insideQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    builder.Append('"');
                    index++;
                    continue;
                }

                insideQuotes = !insideQuotes;
                continue;
            }

            if (character == ',' && !insideQuotes)
            {
                values.Add(builder.ToString());
                builder.Clear();
                continue;
            }

            builder.Append(character);
        }

        values.Add(builder.ToString());
        return values;
    }

    private static string? NormalizeProcessName(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var trimmed = rawValue.Trim().Trim('"');
        if (trimmed.Length == 0)
        {
            return null;
        }

        try
        {
            trimmed = Path.GetFileName(trimmed);
        }
        catch
        {
        }

        if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = Path.GetFileNameWithoutExtension(trimmed);
        }

        return trimmed.Trim();
    }

    private static string CleanupErrorLine(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed["error:".Length..].Trim();
        }

        return trimmed;
    }

    private static string? TryResolvePresentMonPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, PresentMonExeName),
            Path.Combine(AppContext.BaseDirectory, "tools", "PresentMon", PresentMonExeName),
            Path.Combine(AppContext.BaseDirectory, "tools", PresentMonExeName),
            Path.Combine(AppContext.BaseDirectory, "ThirdParty", "PresentMon", PresentMonExeName)
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static void KillStaleEtwSessions(string presentMonPath)
    {
        // ETW trace sessions survive process death. If CloudFrame crashed or was
        // force-killed, the session named CloudFrame-FPS-<oldpid> is still alive
        // and blocks any new PresentMon launch with exit code 6.
        // Run: logman stop "CloudFrame-FPS-*" -ets  for each stale session.
        try
        {
            using var query = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = "logman",
                    Arguments              = "query -ets",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow         = true
                }
            };
            query.Start();
            var output = query.StandardOutput.ReadToEnd();
            query.WaitForExit(3000);

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("CloudFrame-FPS-", StringComparison.OrdinalIgnoreCase))
                    continue;

                var sessionName = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                using var stop = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName        = "logman",
                        Arguments       = $"stop \"{sessionName}\" -ets",
                        UseShellExecute = false,
                        CreateNoWindow  = true
                    }
                };
                stop.Start();
                stop.WaitForExit(3000);
            }
        }
        catch
        {
            // logman not available or access denied — PresentMon's own
            // --stop_existing_session flag is the fallback.
        }
    }

    private static void TerminateStalePresentMonProcesses(string presentMonPath)
    {
        var expectedPath = Path.GetFullPath(presentMonPath);

        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(PresentMonExeName)))
        {
            using (process)
            {
                try
                {
                    var candidatePath = process.MainModule?.FileName;
                    if (!string.Equals(candidatePath, expectedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(2000);
                    }
                }
                catch
                {
                }
            }
        }
    }

    private static void TryDeleteFile(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        StopCaptureCore();
        _disposeCts.Cancel();
        _refreshSignal.Release();

        try
        {
            _refreshWorker.Wait(3000);
        }
        catch
        {
        }

        _disposeCts.Dispose();
        _refreshSignal.Dispose();
    }

    private readonly record struct CaptureSnapshot(
        string? PresentMonPath,
        int? TargetProcessId,
        string? TargetProcessName,
        bool CompatibilityMode,
        string? CompatibilityMessage,
        bool ForegroundMode,
        bool CaptureAlreadyActive,
        bool IsDisposed);

    private readonly record struct FpsSample(int? ProcessId, string? Application, double Fps);

    private readonly record struct ProcessTreeEntry(int ProcessId, int ParentProcessId, string ExecutableName);

    private const uint Th32csSnapprocess = 0x00000002;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(IntPtr hSnapshot, ref ProcessEntry32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(IntPtr hSnapshot, ref ProcessEntry32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct ProcessEntry32
    {
        public uint DwSize;
        public uint CntUsage;
        public uint Th32ProcessId;
        public IntPtr Th32DefaultHeapId;
        public uint Th32ModuleId;
        public uint CntThreads;
        public uint Th32ParentProcessID;
        public int PcPriClassBase;
        public uint DwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string SzExeFile;
    }
}
