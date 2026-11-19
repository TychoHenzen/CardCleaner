using System;
using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     Which stock each shelf kind takes, and how much of it one slot stacks. A card shelf takes cards, a product
///     shelf boosters and packs, and a storage rack boxes, packs and boosters. A slot holds one kind of stock at a
///     time. On a product shelf a pack fills its slot alone and boosters lie in a stack of
///     <see cref="BoostersPerProductSlot" />, so its 16 slots hold 16 packs or 64 boosters. Pure rules, so the table is
///     tested without a scene.
/// </summary>
public static class ShelfPlacementRule
{
    /// <summary>How many boosters one product-shelf slot stacks.</summary>
    public const int BoostersPerProductSlot = 4;

    /// <summary>
    ///     The most bodies of <paramref name="stock" /> one slot of a <paramref name="shelf" /> stacks, or null when
    ///     only the slot's own capacity limits it.
    /// </summary>
    public static int? StackLimit(ShelfKind shelf, StockKind stock)
    {
        return (shelf, stock) switch
        {
            (ShelfKind.ProductShelf, StockKind.Pack) => 1,
            (ShelfKind.ProductShelf, StockKind.Booster) => BoostersPerProductSlot,
            _ => null
        };
    }

    /// <summary>True when a shelf of this kind takes this stock.</summary>
    public static bool Accepts(ShelfKind shelf, StockKind stock)
    {
        return shelf switch
        {
            ShelfKind.CardShelf => stock == StockKind.Card,
            ShelfKind.ProductShelf => stock is StockKind.Booster or StockKind.Pack,
            ShelfKind.StorageRack => stock is StockKind.Box or StockKind.Pack or StockKind.Booster,
            _ => throw new ArgumentOutOfRangeException(nameof(shelf), shelf, "Unknown shelf kind.")
        };
    }

    /// <summary>The stock an item kept in a cardboard box is, for placement.</summary>
    public static StockKind From(StoredItem item)
    {
        return item switch
        {
            StoredCard => StockKind.Card,
            StoredContainer container => From(container.Kind),
            _ => throw new ArgumentOutOfRangeException(nameof(item), item, "Unknown stored item.")
        };
    }

    /// <summary>The stock a container of this kind is, for placement.</summary>
    public static StockKind From(CardContainerKind kind)
    {
        return kind switch
        {
            CardContainerKind.Box => StockKind.Box,
            CardContainerKind.Pack => StockKind.Pack,
            CardContainerKind.Booster => StockKind.Booster,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown container kind.")
        };
    }
}
