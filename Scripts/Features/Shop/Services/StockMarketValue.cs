using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Models.Effects;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     Market value of one stocked body: a card from its signature, a booster or pack from the product prices.
///     Zero for a box, any other body, or nothing. Read on demand, never cached.
/// </summary>
public static class StockMarketValue
{
    public static int ForCard(CardController? card)
    {
        if (card is null || !card.HasSignature) return 0;

        var signature = card.Signature;
        var rarity = SignatureCardHelper.DetermineRarity(new[] { signature });
        var intensity = CardEffectMapping.IntensityOf(signature.Elements);
        return CardValue.For(rarity, intensity, signature.HasMagicalPotential());
    }

    public static int For(RigidBody3D? body)
    {
        return body switch
        {
            CardController card => ForCard(card),
            CardContainer container when ProductMarketPrices.HasMarketPrice(container.Kind) =>
                ProductMarketPrices.For(container.Kind),
            _ => 0
        };
    }
}
