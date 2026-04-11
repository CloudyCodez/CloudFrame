namespace FrameBoost.Models;

internal sealed class AntiCheatStatus
{
    public static AntiCheatStatus None { get; } = new()
    {
        IsDetected = false,
        UseCompatibilityMode = false,
        DisplayName = "None",
        Reason = "No known anti-cheat markers were detected."
    };

    public bool IsDetected { get; init; }

    public bool UseCompatibilityMode { get; init; }

    public string DisplayName { get; init; } = "None";

    public string Reason { get; init; } = string.Empty;
}
