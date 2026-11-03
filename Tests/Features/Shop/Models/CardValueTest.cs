using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class CardValueTest
{
    private static readonly CardRarity[] AllRarities =
    {
        CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare, CardRarity.Epic, CardRarity.Legendary
    };

    private static readonly float[] Intensities = { 0f, 0.29f, 0.3f, 0.69f, 0.7f, 1f };

    [TestCase]
    [TestCategory("Unit")]
    public static void ZeroSignatureCardIsWorthTheMinimum()
    {
        AssertThat(CardValue.For(CardRarity.Common, 0f, false)).IsEqual(CardValue.MinimumCardPrice);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ValueRisesWithRarity()
    {
        var previous = int.MinValue;

        foreach (var rarity in AllRarities)
        {
            var value = CardValue.For(rarity, 0.5f, false);
            AssertThat(value > previous).IsTrue();
            previous = value;
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ValueRisesWithTier()
    {
        var dormant = CardValue.For(CardRarity.Rare, 0.1f, false);
        var active = CardValue.For(CardRarity.Rare, 0.5f, false);
        var intense = CardValue.For(CardRarity.Rare, 0.9f, false);

        AssertThat(active > dormant).IsTrue();
        AssertThat(intense > active).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SpecialCardIsWorthAtLeastTheMultiplier()
    {
        AssertThat(CardValue.For(CardRarity.Common, 0f, true)
            >= CardValue.SpecialMultiplier * CardValue.MinimumCardPrice).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NeverBelowTheMinimum()
    {
        foreach (var rarity in AllRarities)
        foreach (var intensity in Intensities)
        foreach (var isSpecial in new[] { false, true })
        {
            AssertThat(CardValue.For(rarity, intensity, isSpecial) >= CardValue.MinimumCardPrice).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void IsDeterministic()
    {
        AssertThat(CardValue.For(CardRarity.Epic, 0.8f, true))
            .IsEqual(CardValue.For(CardRarity.Epic, 0.8f, true));
    }
}
