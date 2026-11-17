using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Every sealed product of one kind, as the pricing screen prices it: the price lives in the shop's
///     <see cref="IPriceBook" />, so a new price applies to every booster (or every pack) in the shop at once.
/// </summary>
/// <param name="Kind">The kind priced: a booster or a pack.</param>
/// <param name="Book">The shop's price book, which holds the price and the wholesale cost.</param>
internal sealed record ProductStockPrice(CardContainerKind Kind, IPriceBook Book) : IPricedStock
{
    public string ItemName
    {
        get
        {
            var name = CardContainerKindText.NameOf(Kind);
            return char.ToUpperInvariant(name[0]) + name[1..];
        }
    }

    public string Scope => $"The price applies to every {CardContainerKindText.NameOf(Kind)} in the shop.";

    public int MarketValue => ProductMarketPrices.For(Kind);

    public int? UnitCost => Book.WholesaleFor(Kind);

    public int Price => Book.PriceFor(Kind);

    public void SetPrice(int price) => Book.Set(Kind, price);
}
