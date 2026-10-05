using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     What a card is worth at the register. Only common cards (the all-zero signature) can be
///     sold for now; every other card is worth nothing here and is refused by the sale.
/// </summary>
public static class CardPricing
{
    /// <summary>Fixed price of one common card. A tuning constant, not a requirement.</summary>
    public const int CommonCardPrice = 10;

    /// <summary>The price of a card, or 0 when it cannot be sold.</summary>
    public static int GetPrice(CardSignature? signature)
    {
        return IsCommon(signature) ? CommonCardPrice : 0;
    }

    public static bool IsCommon(CardSignature? signature)
    {
        if (signature == null)
            return false;

        for (var i = 0; i < 8; i++)
            if (!Mathf.IsZeroApprox(signature[i]))
                return false;

        return true;
    }
}
