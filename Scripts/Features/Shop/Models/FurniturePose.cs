namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     Where a piece of furniture sits on the floor: its centre in metres and its turn in quarter turns
///     (0 to 3, a quarter turn each). Pure data.
/// </summary>
public readonly record struct FurniturePose(float X, float Z, int QuarterTurns);
