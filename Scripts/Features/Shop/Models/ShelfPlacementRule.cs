using System;
using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     Which stock each shelf kind takes. A card shelf takes cards, a product shelf boosters and packs, and a
///     storage rack boxes, packs and boosters. Pure rules, so the table is tested without a scene.
/// </summary>
public static class ShelfPlacementRule
{
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
