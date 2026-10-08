using System;
using CardCleaner.Scripts.Core.Enumeration;

namespace CardCleaner.Scripts.Features.Card.Models.Effects;

/// <summary>
///     Which art-region effects a card gets: rarity picks the shine, the signature's intensity tier picks the
///     wear or shimmer. Ported from Axiom2d <c>ShaderVariant::from_rarity</c> and <c>ConditionEffect::from_tier</c>;
///     only the tier threshold values come from <c>SignatureProfile::without_archetype</c>.
/// </summary>
public static class CardEffectMapping
{
    // Intensity below this is Dormant.
    private const float ActiveThreshold = 0.3f;

    // Intensity at or above this is Intense.
    private const float IntenseThreshold = 0.7f;

    public static RarityEffect RarityEffectFor(CardRarity rarity)
    {
        return rarity switch
        {
            CardRarity.Common => RarityEffect.None,
            CardRarity.Uncommon => RarityEffect.Embossed,
            CardRarity.Rare => RarityEffect.Glow,
            CardRarity.Epic => RarityEffect.Glossy,
            CardRarity.Legendary => RarityEffect.Foil,
            _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, "Unknown card rarity")
        };
    }

    public static IntensityTier TierFor(float intensity)
    {
        if (intensity >= IntenseThreshold) return IntensityTier.Intense;
        return intensity >= ActiveThreshold ? IntensityTier.Active : IntensityTier.Dormant;
    }

    public static ConditionEffect ConditionEffectFor(IntensityTier tier)
    {
        return tier switch
        {
            IntensityTier.Dormant => ConditionEffect.Worn,
            IntensityTier.Active => ConditionEffect.None,
            IntensityTier.Intense => ConditionEffect.Shiny,
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown intensity tier")
        };
    }

    public static ConditionEffect ConditionEffectFor(float intensity)
    {
        return ConditionEffectFor(TierFor(intensity));
    }

    /// <summary>
    ///     The card's overall intensity: the mean magnitude of its signature elements, in [0, 1].
    ///     <see cref="TierFor" /> makes it Dormant below 0.3, Intense at 0.7 or above, and Active in between.
    /// </summary>
    public static float IntensityOf(float[] elements)
    {
        if (elements.Length == 0) return 0f;

        var sum = 0f;
        foreach (var element in elements)
            sum += MathF.Abs(element);

        return sum / elements.Length;
    }
}
