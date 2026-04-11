namespace FrameBoost.Models;

internal sealed class PowerPlanInfo
{
    public required string Guid { get; init; }

    public required string Name { get; init; }

    public bool IsActive { get; init; }

    public override string ToString() => IsActive ? $"{Name} (Active)" : Name;
}
