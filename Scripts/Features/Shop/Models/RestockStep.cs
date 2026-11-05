namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>One planned restocking action. <c>Idle</c> carries ids of -1.</summary>
public sealed record RestockStep(RestockStepKind Kind, int SourceId, int TargetId)
{
    public static RestockStep Idle { get; } = new(RestockStepKind.Idle, -1, -1);
}
