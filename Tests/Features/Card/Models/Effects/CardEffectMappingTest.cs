using System;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models.Effects;

namespace CardCleaner.Tests.Features.Card.Models.Effects;

[TestSuite]
[RequireGodotRuntime]
public class CardEffectMappingTest
{
    [TestCase(CardRarity.Common, RarityEffect.None)]
    [TestCase(CardRarity.Uncommon, RarityEffect.Embossed)]
    [TestCase(CardRarity.Rare, RarityEffect.Glow)]
    [TestCase(CardRarity.Epic, RarityEffect.Glossy)]
    [TestCase(CardRarity.Legendary, RarityEffect.Foil)]
    [TestCategory("Unit")]
    public static void EveryRarityMapsToItsAxiom2dEffect(CardRarity rarity, RarityEffect expected)
    {
        AssertThat(CardEffectMapping.RarityEffectFor(rarity)).IsEqual(expected);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AnUndefinedRarityIsRejected()
    {
        AssertThrown(() => CardEffectMapping.RarityEffectFor((CardRarity)99))
            .IsInstanceOf<ArgumentOutOfRangeException>();
    }

    [TestCase(0f, IntensityTier.Dormant)]
    [TestCase(0.29f, IntensityTier.Dormant)]
    [TestCase(0.3f, IntensityTier.Active)]
    [TestCase(0.5f, IntensityTier.Active)]
    [TestCase(0.69f, IntensityTier.Active)]
    [TestCase(0.7f, IntensityTier.Intense)]
    [TestCase(1f, IntensityTier.Intense)]
    [TestCategory("Unit")]
    public static void IntensityFallsIntoTheAxiom2dTier(float intensity, IntensityTier expected)
    {
        AssertThat(CardEffectMapping.TierFor(intensity)).IsEqual(expected);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheFloatJustBelowEachThresholdStaysInTheLowerTier()
    {
        AssertThat(CardEffectMapping.TierFor(MathF.BitDecrement(0.3f))).IsEqual(IntensityTier.Dormant);
        AssertThat(CardEffectMapping.TierFor(MathF.BitDecrement(0.7f))).IsEqual(IntensityTier.Active);
    }

    [TestCase(IntensityTier.Dormant, ConditionEffect.Worn)]
    [TestCase(IntensityTier.Active, ConditionEffect.None)]
    [TestCase(IntensityTier.Intense, ConditionEffect.Shiny)]
    [TestCategory("Unit")]
    public static void EveryTierMapsToItsAxiom2dConditionEffect(IntensityTier tier, ConditionEffect expected)
    {
        AssertThat(CardEffectMapping.ConditionEffectFor(tier)).IsEqual(expected);
    }

    [TestCase(0.29f, ConditionEffect.Worn)]
    [TestCase(0.3f, ConditionEffect.None)]
    [TestCase(0.69f, ConditionEffect.None)]
    [TestCase(0.7f, ConditionEffect.Shiny)]
    [TestCategory("Unit")]
    public static void IntensityMapsStraightToTheConditionEffect(float intensity, ConditionEffect expected)
    {
        AssertThat(CardEffectMapping.ConditionEffectFor(intensity)).IsEqual(expected);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AnUndefinedTierIsRejected()
    {
        AssertThrown(() => CardEffectMapping.ConditionEffectFor((IntensityTier)99))
            .IsInstanceOf<ArgumentOutOfRangeException>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AnAllZeroSignatureHasNoIntensity()
    {
        AssertThat(CardEffectMapping.IntensityOf(new float[8])).IsEqual(0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void IntensityIsTheMeanMagnitudeSoNegativeElementsCountAsMuchAsPositiveOnes()
    {
        var elements = new[] { 0.5f, -0.5f, 0.5f, -0.5f, 0.5f, -0.5f, 0.5f, -0.5f };

        AssertThat(CardEffectMapping.IntensityOf(elements)).IsEqual(0.5f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OneStrongElementAmongQuietOnesDoesNotMakeTheCardIntense()
    {
        var elements = new[] { 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };

        AssertThat(CardEffectMapping.IntensityOf(elements)).IsEqual(0.125f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NoElementsHaveNoIntensity()
    {
        AssertThat(CardEffectMapping.IntensityOf(Array.Empty<float>())).IsEqual(0f);
    }
}
