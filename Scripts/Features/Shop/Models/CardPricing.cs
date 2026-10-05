using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     What a card is worth at the register. Every card currently sells for the same fixed price;
///     special-card pricing is a non-goal. The signature is the input so that later pricing rules can
///     slot in here without changing the sale flow.
/// </summary>
public static class CardPricing
{
    /// <summary>Fixed price of one card. A tuning constant, not a requirement.</summary>
    public const int FixedCardPrice = 10;

    /// <summary>The price of a card, or 0 when there is no card data to price.</summary>
    public static int GetPrice(CardSignature? signature)
    {
        return signature == null ? 0 : FixedCardPrice;
    }
}
