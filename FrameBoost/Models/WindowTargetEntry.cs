namespace FrameBoost.Models;

internal sealed class WindowTargetEntry
{
    public required IntPtr Handle { get; init; }

    public required int ProcessId { get; init; }

    public required string ProcessName { get; init; }

    public required string WindowTitle { get; init; }

    public string ExecutablePath { get; init; } = string.Empty;

    public override string ToString()
    {
        var title = string.IsNullOrWhiteSpace(WindowTitle) ? "(untitled window)" : WindowTitle;
        return $"{title} [{ProcessName} • {ProcessId}]";
    }
}
