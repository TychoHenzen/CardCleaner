namespace CardCleaner.Scripts.Features.Shop.Furniture;

/// <summary>
///     One piece of furniture in a save slot: catalog id, floor position, quarter turns, whether it is an unopened
///     box, and a placed piece's node name (null for a box, and in saves written before names were kept).
/// </summary>
public sealed record FurnitureRecord(string Id, float X, float Z, int QuarterTurns, bool IsBox, string? Name = null);
