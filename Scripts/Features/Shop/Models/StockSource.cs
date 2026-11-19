namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     One piece of stock the restocker could carry: its id, kind, whether it sits on a rack, its distance, and how
///     many items in it a product shelf takes (1 for a booster box or booster, what a cardboard box still holds of
///     them for a box).
/// </summary>
public sealed record StockSource(int Id, StockKind Kind, bool OnRack, float DistanceFromRestocker, int Products);
