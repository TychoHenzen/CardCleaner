using CardCleaner.Scripts.Features.Shop.Shelves;

namespace CardCleaner.Scripts.Features.Shop.Restocking;

/// <summary>
///     Stock the restocker could carry, with how many items in it a product shelf takes (1 for a booster box or
///     booster pack, what a cardboard box still holds of them for a box).
/// </summary>
public sealed record StockSource(int Id, StockKind Kind, bool OnRack, float DistanceFromRestocker, int Products);
