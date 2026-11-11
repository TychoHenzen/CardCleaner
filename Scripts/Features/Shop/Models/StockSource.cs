namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     One piece of stock the restocker could carry: its id, kind, whether it sits on a rack and its distance.
/// </summary>
public sealed record StockSource(int Id, StockKind Kind, bool OnRack, float DistanceFromRestocker);
