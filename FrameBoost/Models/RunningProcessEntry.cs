namespace FrameBoost.Models;

internal sealed class RunningProcessEntry
{
    public required int ProcessId { get; init; }

    public required string ProcessName { get; init; }

    public required string WindowTitle { get; init; }

    public required string ExecutablePath { get; init; }

    public bool HasVisibleWindow { get; init; }

    public override string ToString() => $"{ProcessName} ({ProcessId})";
}
