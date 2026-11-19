namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     One shelf slot with room for more stock: its id, the shelf it belongs to, that shelf's kind, and the stock it
///     already holds (null while it is empty). A slot holding part of a stack, such as a product-shelf slot a customer
///     took boosters from, takes only more of that stock.
/// </summary>
public sealed record FreeSlot(int Id, int ShelfId, ShelfKind Shelf, StockKind? Holds = null)
{
    public bool IsEmpty => Holds is null;

    /// <summary>
    ///     True when this slot takes one more <paramref name="stock" />: its shelf takes it and it holds no other.
    /// </summary>
    public bool Takes(StockKind stock) => ShelfPlacementRule.Accepts(Shelf, stock) && (Holds is null || Holds == stock);
}
