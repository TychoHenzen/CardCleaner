namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     Something on a shelf the player can price: one card, or every product of one kind. The pricing screen reads it
///     and sets its price.
/// </summary>
public interface IPricedStock
{
    /// <summary>What the player is pricing, as the screen's title shows it.</summary>
    string ItemName { get; }

    /// <summary>What a new price covers: this card only, or every product of the kind.</summary>
    string Scope { get; }

    int MarketValue { get; }

    /// <summary>What one unit cost the shop to buy in, or null when nothing was paid for it (a card).</summary>
    int? UnitCost { get; }

    int Price { get; }

    void SetPrice(int price);
}
