using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     The asking prices the player set for sealed products, one price per kind: every booster in the shop asks the
///     booster price, every pack the pack price. A kind with no set price asks its market price. A box is wholesale
///     stock that is never sold, so it has no price. The list is a value: setting a price returns a new list.
/// </summary>
public sealed record ProductPriceList
{
    private readonly ImmutableDictionary<CardContainerKind, int> _set;

    private ProductPriceList(ImmutableDictionary<CardContainerKind, int> set) => _set = set;

    /// <summary>No price set, so every kind asks its market price.</summary>
    public static ProductPriceList MarketPrices { get; } = new(ImmutableDictionary<CardContainerKind, int>.Empty);

    /// <summary>The kinds that have a price, in the order a save lists them.</summary>
    public static IReadOnlyList<CardContainerKind> PricedKinds { get; } =
        [CardContainerKind.Booster, CardContainerKind.Pack];

    /// <summary>The prices the player set, by kind; a kind that is not here asks its market price.</summary>
    public IReadOnlyDictionary<CardContainerKind, int> SetPrices => _set;

    /// <summary>The kind's asking price: the set price, else the market price.</summary>
    public int PriceFor(CardContainerKind kind)
    {
        RequirePriced(kind);
        return _set.TryGetValue(kind, out var price) ? price : ProductMarketPrices.For(kind);
    }

    /// <summary>This list with the kind's price set, moved into the range <see cref="ShopPrice" /> allows.</summary>
    public ProductPriceList With(CardContainerKind kind, long price)
    {
        RequirePriced(kind);
        return new ProductPriceList(_set.SetItem(kind, ShopPrice.Clamp(price)));
    }

    /// <summary>
    ///     The id of the kind: its item id in the order catalog (<c>BackofficeCatalog.tres</c>), and the key a save
    ///     stores its price under. Never the name the player reads, which may change.
    /// </summary>
    public static string IdOf(CardContainerKind kind) => kind switch
    {
        CardContainerKind.Box => "cardboard_box",
        CardContainerKind.Pack => "pack",
        CardContainerKind.Booster => "booster",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown container kind")
    };

    private static void RequirePriced(CardContainerKind kind)
    {
        if (!ProductMarketPrices.HasMarketPrice(kind))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "A box is never sold, so it has no price");
    }
}
