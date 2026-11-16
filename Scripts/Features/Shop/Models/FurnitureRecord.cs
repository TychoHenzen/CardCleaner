namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     One piece of furniture as it is written to a save slot: the catalog id, the position on the floor, the
///     quarter turns, whether it is an unopened box rather than a placed piece, and the node name of a placed piece
///     (null for a box, and in saves written before names were kept).
/// </summary>
public sealed record FurnitureRecord(string Id, float X, float Z, int QuarterTurns, bool IsBox, string? Name = null);
