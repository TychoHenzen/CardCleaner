using System;
using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     The shop's asking prices for sealed products, one per kind (booster, pack), saved with the slot. A box is never
///     sold and has no price. Cards are priced one by one, not here.
/// </summary>
public interface IPriceBook
{
    /// <summary>The kind's asking price: the price the player set, else the market price.</summary>
    int PriceFor(CardContainerKind kind);

    /// <summary>Sets the kind's price for every product of that kind, then raises <see cref="Changed" />.</summary>
    void Set(CardContainerKind kind, int price);

    /// <summary>What one unit of the kind costs to order, or null when the order catalog does not sell it.</summary>
    int? WholesaleFor(CardContainerKind kind);

    event Action? Changed;
}
