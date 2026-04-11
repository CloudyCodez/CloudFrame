using System.Diagnostics;
using FrameBoost.Models;

namespace FrameBoost.Services;

internal sealed class BoostCoordinator : IDisposable
{
    private readonly AntiCheatCompatibilityService _antiCheatCompatibilityService;
    private readonly Logger _logger;
    private readonly PowerPlanService _powerPlanService;
    private readonly PriorityService _priorityService;
    private readonly ProcessService _processService;
    private readonly RecoveryStateService _recoveryStateService;
    private readonly TimerResolutionService _timerResolutionService;
    private readonly WindowsBoostRegistryService _registryBoostService;
    private readonly PeriodicTimer _timer;
    private readonly CancellationTokenSource _timerCancellation = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ActiveBoostSession? _activeSession;
    private DateTimeOffset _lastMaintenancePassAt = DateTimeOffset.MinValue;

    // Tweak flags — set by ConfigureTweaks() from MainForm after settings load
    private bool _enableTimerResolution;
    private bool _enableMmcss;
    private bool _disableGameDvr;

    public BoostCoordinator(
        AntiCheatCompatibilityService antiCheatCompatibilityService,
        Logger logger,
        PowerPlanService powerPlanService,
        PriorityService priorityService,
        ProcessService processService,
        RecoveryStateService recoveryStateService,
        TimerResolutionService timerResolutionService,
        WindowsBoostRegistryService registryBoostService)
    {
        _antiCheatCompatibilityService = antiCheatCompatibilityService;
        _logger = logger;
        _powerPlanService = powerPlanService;
        _priorityService = priorityService;
        _processService = processService;
        _recoveryStateService = recoveryStateService;
        _timerResolutionService = timerResolutionService;
        _registryBoostService = registryBoostService;
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        _ = MonitorAsync();
    }

    /// <summary>
    /// Called by MainForm after loading AppSettings so the coordinator knows
    /// which advanced tweaks to apply/restore without importing AppSettings directly.
    /// </summary>
    public void ConfigureTweaks(bool enableTimerResolution, bool enableMmcss, bool disableGameDvr)
    {
        _enableTimerResolution = enableTimerResolution;
        _enableMmcss           = enableMmcss;
        _disableGameDvr        = disableGameDvr;
    }

    public event EventHandler<ActiveBoostSession?>? ActiveSessionChanged;

    public ActiveBoostSession? ActiveSession => _activeSession;

    public async Task RecoverPendingSessionAsync(IReadOnlyList<GameProfile> profiles)
    {
        var state = _recoveryStateService.Load();
        if (state is null)
        {
            return;
        }

        var profile = profiles.FirstOrDefault(candidate => candidate.Id == state.ProfileId);
        _logger.Log($"Found an unfinished boost session for PID {state.GameProcessId}. Attempting recovery.");
        await RestoreStateAsync(state, profile, "Recovered unfinished session").ConfigureAwait(false);
    }

    public async Task<BoostOutcome> ApplyPreLaunchBoostAsync(GameProfile profile)
    {
        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            if (_activeSession is not null)
            {
                if (_activeSession.RecoveryState.IsPreLaunchBoost)
                {
                    return new BoostOutcome { Success = true, Message = "Universal boost is already active." };
                }

                return new BoostOutcome { Success = false, Message = "A game-specific boost is already active. Restore it before starting universal boost." };
            }

            var state = new BoostRecoveryState
            {
                ProfileId = profile.Id,
                GameProcessId = 0,
                GameProcessName = "Universal boost",
                IsPreLaunchBoost = true,
                StartedAt = DateTimeOffset.Now
            };

            await ApplyPowerPlanAsync(profile, state).ConfigureAwait(false);
            ApplySystemTweaks();

            ApplyBackgroundActions(profile, state, null);

            _recoveryStateService.Save(state);
                _activeSession = new ActiveBoostSession
                {
                    Profile = profile,
                    RecoveryState = state,
                    AntiCheatStatus = AntiCheatStatus.None
                };
            _lastMaintenancePassAt = DateTimeOffset.Now;

            _logger.Log($"Universal boost is active using the {profile.BoostPreset} preset.");
            _logger.Log("Universal boost is active. Safe pre-launch tuning will stay on until you restore it or boost a profiled game.");
            ActiveSessionChanged?.Invoke(this, _activeSession);
            return new BoostOutcome { Success = true, Message = "Universal boost is now active." };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BoostOutcome> ApplyBoostAsync(GameProfile profile, int processId, bool autoTriggered)
    {
        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            if (_activeSession is not null)
            {
                if (_activeSession.RecoveryState.GameProcessId == processId)
                {
                    return new BoostOutcome { Success = true, Message = "This game is already boosted." };
                }

                if (!_activeSession.RecoveryState.IsPreLaunchBoost)
                {
                    return new BoostOutcome { Success = false, Message = "Another boosted game is already active. Restore it before boosting a second game." };
                }
            }

            Process process;
            try
            {
                process = Process.GetProcessById(processId);
            }
            catch
            {
                return new BoostOutcome { Success = false, Message = "The target process is no longer running." };
            }

            using (process)
            {
                _processService.TryGetExecutablePath(process, out var executablePath);
                var antiCheatStatus = _antiCheatCompatibilityService.Evaluate(executablePath);

                var reusingPreLaunchBoost = _activeSession?.RecoveryState.IsPreLaunchBoost == true;
                var state = reusingPreLaunchBoost
                    ? _activeSession!.RecoveryState
                    : new BoostRecoveryState
                    {
                        ProfileId = profile.Id,
                        StartedAt = DateTimeOffset.Now
                    };

                state.ProfileId = profile.Id;
                state.GameProcessId = processId;
                state.GameProcessName = process.ProcessName;
                state.IsPreLaunchBoost = false;
                state.CompatibilityModeEnabled = antiCheatStatus.UseCompatibilityMode;
                state.AntiCheatVendor = antiCheatStatus.IsDetected ? antiCheatStatus.DisplayName : null;
                state.AntiCheatReason = antiCheatStatus.IsDetected ? antiCheatStatus.Reason : null;

                await ApplyPowerPlanAsync(profile, state).ConfigureAwait(false);
                if (!reusingPreLaunchBoost) ApplySystemTweaks(); // already applied in pre-launch path

                if (!antiCheatStatus.UseCompatibilityMode
                    && profile.BoostGamePriority
                    && !state.Restores.Any(item => item.ProcessId == process.Id && item.IsGameProcess)
                    && _priorityService.TryGetPriority(process, out var originalGamePriority))
                {
                    state.Restores.Add(new ProcessPriorityRestoreItem
                    {
                        ProcessId = process.Id,
                        ProcessName = process.ProcessName,
                        OriginalPriorityClass = originalGamePriority,
                        HasOriginalPriorityClass = true,
                        IsGameProcess = true
                    });

                    var targetPriority = profile.ToProcessPriorityClass();
                    string? gamePriorityError = null;
                    if (originalGamePriority != targetPriority &&
                        _priorityService.TrySetPriority(process, targetPriority, out gamePriorityError))
                    {
                        state.GamePriorityRaised = true;
                        _logger.Log($"Raised '{profile.Name}' to {targetPriority} priority.");
                    }
                    else if (!string.IsNullOrWhiteSpace(gamePriorityError))
                    {
                        _logger.Log($"Could not adjust game priority for '{profile.Name}': {gamePriorityError}");
                    }
                }

                if (!antiCheatStatus.UseCompatibilityMode)
                {
                    ApplyBackgroundActions(profile, state, process.Id);
                }

                if (antiCheatStatus.UseCompatibilityMode)
                {
                    _logger.Log($"Anti-cheat compatibility mode engaged for '{profile.Name}' via {antiCheatStatus.DisplayName}. Process priority changes were skipped.");
                }

                _recoveryStateService.Save(state);
                _activeSession = new ActiveBoostSession
                {
                    Profile = profile,
                    RecoveryState = state,
                    AntiCheatStatus = antiCheatStatus
                };
                _lastMaintenancePassAt = DateTimeOffset.Now;

                var triggerLabel = autoTriggered ? "Auto-boosted" : reusingPreLaunchBoost ? "Boosted from universal mode" : "Boosted";
                _logger.Log($"{triggerLabel} '{profile.Name}' (PID {process.Id}).");
                _logger.Log(BuildBoostSummary(profile, state, antiCheatStatus));
                ActiveSessionChanged?.Invoke(this, _activeSession);
                var message = antiCheatStatus.UseCompatibilityMode
                    ? $"{profile.Name} is now boosted in anti-cheat compatibility mode."
                    : reusingPreLaunchBoost
                        ? $"{profile.Name} is now boosted, carrying over the active universal boost."
                        : $"{profile.Name} is now boosted.";
                return new BoostOutcome { Success = true, Message = message };
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string BuildBoostSummary(GameProfile profile, BoostRecoveryState state, AntiCheatStatus antiCheatStatus)
    {
        var parts = new List<string>
        {
            $"Preset: {profile.BoostPreset}",
            $"Power plan: {BuildPowerPlanSummary(profile, state)}",
            $"Game priority: {BuildGamePrioritySummary(profile, state)}",
            $"Background priority: {state.BackgroundPriorityTunedCount}",
            $"Memory priority: {state.BackgroundMemoryPriorityCount}",
            $"EcoQoS: {state.BackgroundEcoQosCount}",
            $"Maintenance: {(profile.ShouldRunRecurringMaintenance() ? profile.GetMaintenanceInterval().TotalSeconds.ToString("0") + "s loop" : "single pass")}",
            $"Memory trims: {state.TrimmedProcessCount}",
            $"Graceful closes: {state.GracefullyClosedProcessCount}"
        };

        if (antiCheatStatus.UseCompatibilityMode)
        {
            parts.Add($"Compatibility: {antiCheatStatus.DisplayName}");
        }

        return "Boost summary: " + string.Join(" | ", parts);
    }

    public async Task RestoreCurrentAsync(string reason)
    {
        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            if (_activeSession is null)
            {
                return;
            }

            var state = _activeSession.RecoveryState;
            var profile = _activeSession.Profile;
            await RestoreStateAsync(state, profile, reason).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BoostOutcome> RunMaintenancePassNowAsync()
    {
        if (_activeSession is null)
        {
            return new BoostOutcome { Success = false, Message = "No active boost session is running yet." };
        }

        if (_activeSession.AntiCheatStatus.UseCompatibilityMode)
        {
            return new BoostOutcome { Success = false, Message = "Maintenance is limited while anti-cheat compatibility mode is active." };
        }

        await RunMaintenancePassAsync().ConfigureAwait(false);
        return new BoostOutcome
        {
            Success = true,
            Message = _activeSession.RecoveryState.IsPreLaunchBoost
                ? "Re-ran the universal boost maintenance pass."
                : $"Re-ran the live maintenance pass for '{_activeSession.Profile.Name}'."
        };
    }

    private async Task MonitorAsync()
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(_timerCancellation.Token).ConfigureAwait(false))
            {
                if (_activeSession is null)
                {
                    continue;
                }

                if (_activeSession.RecoveryState.IsPreLaunchBoost)
                {
                    if (_activeSession.Profile.ShouldRunRecurringMaintenance()
                        && DateTimeOffset.Now - _lastMaintenancePassAt >= _activeSession.Profile.GetMaintenanceInterval())
                    {
                        await RunMaintenancePassAsync().ConfigureAwait(false);
                    }

                    continue;
                }

                if (!ProcessService.IsProcessAlive(_activeSession.RecoveryState.GameProcessId))
                {
                    await RestoreCurrentAsync("Game exited, restoring system state.").ConfigureAwait(false);
                    continue;
                }

                if (!_activeSession.AntiCheatStatus.UseCompatibilityMode
                    && _activeSession.Profile.ShouldRunRecurringMaintenance()
                    && DateTimeOffset.Now - _lastMaintenancePassAt >= _activeSession.Profile.GetMaintenanceInterval())
                {
                    await RunMaintenancePassAsync().ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RunMaintenancePassAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            if (_activeSession is null)
            {
                return;
            }

            var beforePriority = _activeSession.RecoveryState.BackgroundPriorityTunedCount;
            var beforeMemoryPriority = _activeSession.RecoveryState.BackgroundMemoryPriorityCount;
            var beforeEcoQos = _activeSession.RecoveryState.BackgroundEcoQosCount;
            var beforeTrims = _activeSession.RecoveryState.TrimmedProcessCount;
            var beforeCloses = _activeSession.RecoveryState.GracefullyClosedProcessCount;

            ApplyBackgroundActions(
                _activeSession.Profile,
                _activeSession.RecoveryState,
                _activeSession.RecoveryState.IsPreLaunchBoost ? null : _activeSession.RecoveryState.GameProcessId,
                logActions: false);

            var priorityDelta = _activeSession.RecoveryState.BackgroundPriorityTunedCount - beforePriority;
            var memoryPriorityDelta = _activeSession.RecoveryState.BackgroundMemoryPriorityCount - beforeMemoryPriority;
            var ecoQosDelta = _activeSession.RecoveryState.BackgroundEcoQosCount - beforeEcoQos;
            var trimDelta = _activeSession.RecoveryState.TrimmedProcessCount - beforeTrims;
            var closeDelta = _activeSession.RecoveryState.GracefullyClosedProcessCount - beforeCloses;

            if (priorityDelta > 0 || memoryPriorityDelta > 0 || ecoQosDelta > 0 || trimDelta > 0 || closeDelta > 0)
            {
                var changes = new List<string>();
                if (priorityDelta > 0)
                {
                    changes.Add($"{priorityDelta} priority tune(s)");
                }

                if (memoryPriorityDelta > 0)
                {
                    changes.Add($"{memoryPriorityDelta} memory-priority change(s)");
                }

                if (ecoQosDelta > 0)
                {
                    changes.Add($"{ecoQosDelta} EcoQoS change(s)");
                }

                if (trimDelta > 0)
                {
                    changes.Add($"{trimDelta} memory trim(s)");
                }

                if (closeDelta > 0)
                {
                    changes.Add($"{closeDelta} graceful close(s)");
                }

                _logger.Log($"Maintenance pass applied {string.Join(", ", changes)} for '{_activeSession.Profile.Name}'.");
                _recoveryStateService.Save(_activeSession.RecoveryState);
                ActiveSessionChanged?.Invoke(this, _activeSession);
            }

            _lastMaintenancePassAt = DateTimeOffset.Now;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ApplyPowerPlanAsync(GameProfile profile, BoostRecoveryState state)
    {
        if (!profile.SwitchPowerPlan || string.IsNullOrWhiteSpace(profile.PreferredPowerPlanGuid))
        {
            return;
        }

        var activePlan = await _powerPlanService.GetActivePlanAsync().ConfigureAwait(false);
        if (activePlan is null || string.Equals(activePlan.Guid, profile.PreferredPowerPlanGuid, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(state.PreviousPowerPlanGuid))
        {
            state.PreviousPowerPlanGuid = activePlan.Guid;
            state.PreviousPowerPlanName = activePlan.Name;
        }

        if (await _powerPlanService.SetActivePlanAsync(profile.PreferredPowerPlanGuid!).ConfigureAwait(false))
        {
            state.PowerPlanChanged = true;
            _logger.Log($"Switched power plan from '{activePlan.Name}' to '{profile.PreferredPowerPlanName ?? profile.PreferredPowerPlanGuid}'.");
        }
    }

    private void ApplyBackgroundActions(GameProfile profile, BoostRecoveryState state, int? gameProcessId, bool logActions = true)
    {
        foreach (var processName in profile.GetBackgroundProcessNames())
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    var existingRestore = state.Restores.FirstOrDefault(item => item.ProcessId == process.Id && !item.IsGameProcess);
                    if ((gameProcessId.HasValue && process.Id == gameProcessId.Value)
                        || process.HasExited
                        || _processService.IsProtectedProcess(process)
                        || process.ProcessName.Equals("CloudFrame", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string? closeError = null;
                    if (profile.CloseBackgroundAppsGracefully
                        && _processService.TryCloseGracefully(process, TimeSpan.FromSeconds(2), out closeError))
                    {
                        state.GracefullyClosedProcessCount++;
                        if (logActions)
                        {
                            _logger.Log($"Gracefully closed '{process.ProcessName}' (PID {process.Id}) to free resources.");
                        }

                        continue;
                    }

                    if (profile.CloseBackgroundAppsGracefully && !string.IsNullOrWhiteSpace(closeError) && logActions)
                    {
                        _logger.Log($"Could not gracefully close '{process.ProcessName}' (PID {process.Id}): {closeError}");
                    }

                    if (profile.ShouldLowerBackgroundMemoryPriority()
                        && _processService.TryGetMemoryPriority(process, out var originalMemoryPriority, out _))
                    {
                        string? memoryPriorityError = null;
                        if (existingRestore is null)
                        {
                            existingRestore = CreateRestoreItem(process);
                            state.Restores.Add(existingRestore);
                        }

                        if (!existingRestore.OriginalMemoryPriority.HasValue)
                        {
                            existingRestore.OriginalMemoryPriority = originalMemoryPriority;
                        }

                        var targetMemoryPriority = profile.GetBackgroundMemoryPriority();
                        if (originalMemoryPriority > targetMemoryPriority
                            && _processService.TrySetMemoryPriority(process, targetMemoryPriority, out memoryPriorityError))
                        {
                            state.BackgroundMemoryPriorityCount++;
                            if (logActions)
                            {
                                _logger.Log($"Lowered memory priority for '{process.ProcessName}' (PID {process.Id}) to {targetMemoryPriority}.");
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(memoryPriorityError) && logActions)
                        {
                            _logger.Log($"Could not lower memory priority for '{process.ProcessName}' (PID {process.Id}): {memoryPriorityError}");
                        }
                    }

                    if (profile.ShouldApplyBackgroundEcoQos()
                        && _processService.TryGetPowerThrottling(process, out var originalPowerState, out _))
                    {
                        string? ecoQosError = null;
                        if (existingRestore is null)
                        {
                            existingRestore = CreateRestoreItem(process);
                            state.Restores.Add(existingRestore);
                        }

                        if (!existingRestore.HasOriginalPowerThrottlingState)
                        {
                            existingRestore.HasOriginalPowerThrottlingState = true;
                            existingRestore.OriginalPowerThrottlingControlMask = originalPowerState.ControlMask;
                            existingRestore.OriginalPowerThrottlingStateMask = originalPowerState.StateMask;
                        }

                        var executionSpeedEnabled =
                            (originalPowerState.ControlMask & 0x1) == 0x1 &&
                            (originalPowerState.StateMask & 0x1) == 0x1;

                        if (!executionSpeedEnabled
                            && _processService.TrySetExecutionSpeedThrottling(process, true, out ecoQosError))
                        {
                            state.BackgroundEcoQosCount++;
                            if (logActions)
                            {
                                _logger.Log($"Enabled EcoQoS throttling for '{process.ProcessName}' (PID {process.Id}).");
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(ecoQosError) && logActions)
                        {
                            _logger.Log($"Could not enable EcoQoS throttling for '{process.ProcessName}' (PID {process.Id}): {ecoQosError}");
                        }
                    }

                    if (profile.TrimBackgroundMemory)
                    {
                        if (_processService.TryTrimWorkingSet(process, out var trimError))
                        {
                            state.TrimmedProcessCount++;
                            if (logActions)
                            {
                                _logger.Log($"Trimmed working set for '{process.ProcessName}' (PID {process.Id}).");
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(trimError) && logActions)
                        {
                            _logger.Log($"Could not trim working set for '{process.ProcessName}' (PID {process.Id}): {trimError}");
                        }
                    }

                    if (!profile.LowerBackgroundProcesses || !_priorityService.TryGetPriority(process, out var originalPriority))
                    {
                        continue;
                    }

                    var targetPriorityClass = profile.ToBackgroundPriorityClass();
                    if (originalPriority == targetPriorityClass
                        || (targetPriorityClass == ProcessPriorityClass.BelowNormal
                            && originalPriority is ProcessPriorityClass.Idle or ProcessPriorityClass.BelowNormal))
                    {
                        continue;
                    }

                    if (!_priorityService.TrySetPriority(process, targetPriorityClass, out var error))
                    {
                        if (!string.IsNullOrWhiteSpace(error) && logActions)
                        {
                            _logger.Log($"Could not lower priority for '{process.ProcessName}' (PID {process.Id}): {error}");
                        }

                        continue;
                    }

                    state.BackgroundPriorityTunedCount++;
                    if (existingRestore is null)
                    {
                        existingRestore = CreateRestoreItem(process);
                        state.Restores.Add(existingRestore);
                    }

                    if (!existingRestore.HasOriginalPriorityClass)
                    {
                        existingRestore.OriginalPriorityClass = originalPriority;
                        existingRestore.HasOriginalPriorityClass = true;
                    }

                    if (logActions)
                    {
                        _logger.Log($"Lowered '{process.ProcessName}' (PID {process.Id}) to {targetPriorityClass} priority.");
                    }
                }
            }
        }
    }

    private static ProcessPriorityRestoreItem CreateRestoreItem(Process process)
    {
        return new ProcessPriorityRestoreItem
        {
            ProcessId = process.Id,
            ProcessName = process.ProcessName,
            IsGameProcess = false
        };
    }

    private static string BuildPowerPlanSummary(GameProfile profile, BoostRecoveryState state)
    {
        if (!profile.SwitchPowerPlan)
        {
            return "off";
        }

        return state.PowerPlanChanged
            ? profile.PreferredPowerPlanName ?? "changed"
            : "kept current";
    }

    private static string BuildGamePrioritySummary(GameProfile profile, BoostRecoveryState state)
    {
        if (!profile.BoostGamePriority)
        {
            return "off";
        }

        return state.GamePriorityRaised ? profile.GamePriority.ToString() : "unchanged";
    }

    private async Task RestoreStateAsync(BoostRecoveryState state, GameProfile? profile, string reason)
    {
        foreach (var restore in state.Restores.OrderByDescending(static item => item.IsGameProcess))
        {
            try
            {
                using var process = Process.GetProcessById(restore.ProcessId);
                if (restore.HasOriginalPriorityClass)
                {
                    _priorityService.TrySetPriority(process, restore.OriginalPriorityClass, out _);
                }

                if (restore.OriginalMemoryPriority.HasValue)
                {
                    _processService.TrySetMemoryPriority(process, restore.OriginalMemoryPriority.Value, out _);
                }

                if (restore.HasOriginalPowerThrottlingState)
                {
                    var executionSpeedEnabled =
                        (restore.OriginalPowerThrottlingControlMask & 0x1) == 0x1 &&
                        (restore.OriginalPowerThrottlingStateMask & 0x1) == 0x1;
                    _processService.TrySetExecutionSpeedThrottling(process, executionSpeedEnabled, out _);
                }
            }
            catch
            {
            }
        }

        if (!string.IsNullOrWhiteSpace(state.PreviousPowerPlanGuid))
        {
            var success = await _powerPlanService.SetActivePlanAsync(state.PreviousPowerPlanGuid).ConfigureAwait(false);
            if (success)
            {
                _logger.Log($"Restored power plan to '{state.PreviousPowerPlanName ?? state.PreviousPowerPlanGuid}'.");
            }
        }

        _timerResolutionService.RestoreResolution();
        _registryBoostService.RestoreTweaks();
        _recoveryStateService.Delete();
        _logger.Log(reason);
        _activeSession = null;
        _lastMaintenancePassAt = DateTimeOffset.MinValue;
        ActiveSessionChanged?.Invoke(this, null);

        if (profile is not null)
        {
            _logger.Log($"Finished restore for '{profile.Name}'.");
        }
    }

    private void ApplySystemTweaks()
    {
        if (_enableTimerResolution)
            _timerResolutionService.SetHighResolution();
        if (_enableMmcss || _disableGameDvr)
            _registryBoostService.ApplyTweaks(_enableMmcss, _disableGameDvr);
    }

    public void Dispose()
    {
        _timerCancellation.Cancel();
        _timer.Dispose();
        _timerCancellation.Dispose();
        _gate.Dispose();
    }
}
