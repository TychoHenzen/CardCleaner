namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     One piece of furniture as it is written to a save slot: the catalog id, the position on the floor, the
///     quarter turns and whether it is an unopened box rather than a placed piece.
/// </summary>
public sealed record FurnitureRecord(string Id, float X, float Z, int QuarterTurns, bool IsBox);
