using System;
using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Scripts.Features.Shop.Pricing;

/// <summary>
///     The sealed products on one product-shelf level (packs, or boosters), as the pricing screen prices them. The
///     price is each product's own (<c>CardContainer.ShopPrice</c>) and travels with it; setting it here sets it on
///     every product on the level, never on products elsewhere in the shop.
/// </summary>
/// <param name="Kind">The kind priced: a booster or a pack.</param>
/// <param name="Count">How many products the level holds, all of <paramref name="Kind" />.</param>
/// <param name="MarketValue">The market price of one, which it asks until a price is set.</param>
/// <param name="UnitCost">What one costs to order, or null when the order catalog does not sell it.</param>
/// <param name="SetPriceOnTop">Reads the price set on the product a customer takes next, null while none is.</param>
/// <param name="StoreOnEach">Stores a new price on every product on the level, already inside the range.</param>
internal sealed record ContainerStockPrice(
    CardContainerKind Kind,
    int Count,
    int MarketValue,
    int? UnitCost,
    Func<int?> SetPriceOnTop,
    Action<int> StoreOnEach) : IPricedStock
{
    public string ItemName
    {
        get
        {
            var name = CardContainerKindText.NameOf(Kind);
            return char.ToUpperInvariant(name[0]) + name[1..];
        }
    }

    public string Scope => Count > 1
        ? $"The price applies to the {Count} {CardContainerKindText.PluralOf(Kind)} here and stays with each one."
        : $"The price stays with this {CardContainerKindText.NameOf(Kind)} wherever it goes.";

    public int Price => SetPriceOnTop() ?? MarketValue;

    public void SetPrice(int price) => StoreOnEach(ShopPrice.Clamp(price));
}
