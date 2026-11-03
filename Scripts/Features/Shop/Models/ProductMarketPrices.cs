using System;
using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     Market price of a sealed product sold at the register. A box is wholesale stock, so it has no market price.
/// </summary>
public static class ProductMarketPrices
{
    public const int BoosterPrice = 4;
    public const int PackPrice = 30;

    public static bool HasMarketPrice(CardContainerKind kind)
    {
        return kind != CardContainerKind.Box;
    }

    public static int For(CardContainerKind kind)
    {
        return kind switch
        {
            CardContainerKind.Booster => BoosterPrice,
            CardContainerKind.Pack => PackPrice,
            CardContainerKind.Box => throw new ArgumentOutOfRangeException(
                nameof(kind), kind, "A box is wholesale stock and has no market price"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown container kind")
        };
    }
}
