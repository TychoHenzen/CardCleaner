using CardCleaner.Scripts.Features.Shop.Shelves;

namespace CardCleaner.Scripts.Features.Shop.Restocking;

/// <summary>
///     One shelf level with room for more stock: its id, the shelf it belongs to, that shelf's kind, the stock it
///     already holds (null while it is empty) and how many of its seats hold nothing. A level holds one kind of stock,
///     so one that is partly filled, such as a level a customer took boosters from, takes only more of that stock.
/// </summary>
public sealed record FreeSlot(int Id, int ShelfId, ShelfKind Shelf, StockKind? Holds = null, int EmptySeats = 1)
{
    public bool IsEmpty => Holds is null;

    /// <summary>
    ///     True when this level takes one more <paramref name="stock" />: its shelf takes it and it holds no other.
    /// </summary>
    public bool Takes(StockKind stock) => ShelfPlacementRule.LevelTakes(Shelf, Holds, stock);
}
