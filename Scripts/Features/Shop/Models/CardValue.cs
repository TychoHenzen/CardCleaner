using System;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models.Effects;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     What one card is worth as market value: its rarity sets the base, the signature's intensity tier scales it,
///     and a special card multiplies the result. Pure rules, no engine types.
/// </summary>
public static class CardValue
{
    /// <summary>No card is worth less than this, even one with a zero signature.</summary>
    public const int MinimumCardPrice = 2;

    /// <summary>Extra factor applied to a special card.</summary>
    public const int SpecialMultiplier = 10;

    public static int For(CardRarity rarity, float intensity, bool isSpecial)
    {
        var raw = BaseFor(rarity) * TierFactorFor(CardEffectMapping.TierFor(intensity));
        if (isSpecial) raw *= SpecialMultiplier;
        return Math.Max(MinimumCardPrice, (int)Math.Round(raw, MidpointRounding.AwayFromZero));
    }

    private static int BaseFor(CardRarity rarity)
    {
        return rarity switch
        {
            CardRarity.Common => 2,
            CardRarity.Uncommon => 5,
            CardRarity.Rare => 12,
            CardRarity.Epic => 30,
            CardRarity.Legendary => 80,
            _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, "Unknown card rarity")
        };
    }

    private static float TierFactorFor(IntensityTier tier)
    {
        return tier switch
        {
            IntensityTier.Dormant => 1.0f,
            IntensityTier.Active => 1.5f,
            IntensityTier.Intense => 2.5f,
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown intensity tier")
        };
    }
}
