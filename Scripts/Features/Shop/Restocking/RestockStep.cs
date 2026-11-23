namespace CardCleaner.Scripts.Features.Shop.Restocking;

/// <summary>
///     One planned restocking action: carry the source to the target, a slot for a move or a shelf for an unpack.
///     <paramref name="ShelfId" /> is the shelf the target belongs to (the target itself for an unpack), so a shelf
///     the restocker cannot reach is given up as a whole. <c>Idle</c> carries ids of -1.
/// </summary>
public sealed record RestockStep(RestockStepKind Kind, int SourceId, int TargetId, int ShelfId = -1)
{
    public static RestockStep Idle { get; } = new(RestockStepKind.Idle, -1, -1);
}
