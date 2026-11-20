using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;

namespace CardCleaner.Scripts.Features.Card.Models.Effects;

/// <summary>
///     One signature for every rarity and intensity tier a card can have, for the comparison view. Rarity is the
///     number of pure elements (<c>SignatureCardHelper.DetermineRarity</c>), the tier the mean element magnitude
///     (<see cref="CardEffectMapping.IntensityOf" />), so each cell sets as many elements to <see cref="PureValue" />
///     as its rarity needs and fills the rest to bring the mean to its tier. Epic needs three pure elements, a mean
///     of at least 0.34, so Epic and Legendary have no Dormant cell. A test keeps every cell on its label.
/// </summary>
public static class CardEffectComparisonGrid
{
    private const float PureValue = 0.95f;

    // Below the pure threshold (0.9), so a filler element never adds to the rarity.
    private const float MostFiller = 0.85f;

    /// <summary>Every cell a card can reach, rarities in order and tiers in order within each.</summary>
    public static IReadOnlyList<(CardRarity Rarity, IntensityTier Tier)> Cells { get; } =
        (from rarity in Enum.GetValues<CardRarity>()
            from tier in Enum.GetValues<IntensityTier>()
            where Build(rarity, tier) != null
            select (rarity, tier)).ToArray();

    public static bool Has(CardRarity rarity, IntensityTier tier) => Cells.Contains((rarity, tier));

    public static CardSignature SignatureFor(CardRarity rarity, IntensityTier tier)
    {
        if (!Enum.IsDefined(rarity))
            throw new ArgumentOutOfRangeException(nameof(rarity), rarity, "Unknown card rarity");
        if (!Enum.IsDefined(tier))
            throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown intensity tier");

        return Build(rarity, tier)
               ?? throw new ArgumentException($"No card is {rarity} at {tier} intensity", nameof(tier));
    }

    private static CardSignature? Build(CardRarity rarity, IntensityTier tier)
    {
        var pure = PureCount(rarity);
        var filler = Math.Clamp((TargetMean(tier) * 8f - pure * PureValue) / (8 - pure), 0f, MostFiller);
        var elements = new float[8];
        for (var index = 0; index < elements.Length; index++)
            elements[index] = index < pure ? PureValue : filler;

        // The Common and Dormant cell is all zero: the signature of an ordinary pack card.
        var reached = CardEffectMapping.TierFor(CardEffectMapping.IntensityOf(elements));
        return reached == tier ? new CardSignature(elements) : null;
    }

    private static int PureCount(CardRarity rarity) => rarity switch
    {
        CardRarity.Common => 0,
        CardRarity.Uncommon => 1,
        CardRarity.Rare => 2,
        CardRarity.Epic => 3,
        _ => 4
    };

    private static float TargetMean(IntensityTier tier) => tier switch
    {
        IntensityTier.Dormant => 0f,
        IntensityTier.Active => 0.5f,
        _ => MostFiller
    };
}
