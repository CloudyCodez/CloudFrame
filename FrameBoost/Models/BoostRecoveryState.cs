using System.Diagnostics;

namespace FrameBoost.Models;

internal sealed class BoostRecoveryState
{
    public string? ProfileId { get; set; }

    public int GameProcessId { get; set; }

    public string GameProcessName { get; set; } = string.Empty;

    public bool IsPreLaunchBoost { get; set; }

    public string? PreviousPowerPlanGuid { get; set; }

    public string? PreviousPowerPlanName { get; set; }

    public bool CompatibilityModeEnabled { get; set; }

    public string? AntiCheatVendor { get; set; }

    public string? AntiCheatReason { get; set; }

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;

    public bool PowerPlanChanged { get; set; }

    public bool GamePriorityRaised { get; set; }

    public int BackgroundPriorityTunedCount { get; set; }

    public int BackgroundMemoryPriorityCount { get; set; }

    public int BackgroundEcoQosCount { get; set; }

    public int TrimmedProcessCount { get; set; }

    public int GracefullyClosedProcessCount { get; set; }

    public List<ProcessPriorityRestoreItem> Restores { get; set; } = [];
}

internal sealed class ProcessPriorityRestoreItem
{
    public int ProcessId { get; set; }

    public string ProcessName { get; set; } = string.Empty;

    public ProcessPriorityClass OriginalPriorityClass { get; set; }

    public bool HasOriginalPriorityClass { get; set; }

    public uint? OriginalMemoryPriority { get; set; }

    public bool HasOriginalPowerThrottlingState { get; set; }

    public uint OriginalPowerThrottlingControlMask { get; set; }

    public uint OriginalPowerThrottlingStateMask { get; set; }

    public bool IsGameProcess { get; set; }
}

internal sealed class ActiveBoostSession
{
    public required GameProfile Profile { get; init; }

    public required BoostRecoveryState RecoveryState { get; init; }

    public AntiCheatStatus AntiCheatStatus { get; init; } = AntiCheatStatus.None;
}

internal sealed class BoostOutcome
{
    public required bool Success { get; init; }

    public required string Message { get; init; }
}
