using System;

namespace CardCleaner.Scripts.Features.Shop.Pricing;

/// <summary>
///     One card on a shelf, as the pricing screen prices it: the price is the card's own and travels with it. Cards
///     come out of opened stock, so nothing was paid for one and it has no unit cost.
/// </summary>
/// <param name="ItemName">What the screen calls the card, such as "Rare card".</param>
/// <param name="MarketValue">The card's market value, which it asks until a price is set.</param>
/// <param name="SetPriceOnCard">Reads the price set on the card, null while none is.</param>
/// <param name="StoreOnCard">Stores a new price on the card, already inside the <see cref="ShopPrice" /> range.</param>
internal sealed record CardStockPrice(
    string ItemName,
    int MarketValue,
    Func<int?> SetPriceOnCard,
    Action<int> StoreOnCard) : IPricedStock
{
    public string Scope => "The price stays with this card wherever it is shelved.";

    public int? UnitCost => null;

    public int Price => SetPriceOnCard() ?? MarketValue;

    public void SetPrice(int price) => StoreOnCard(ShopPrice.Clamp(price));
}
