using System;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Card.Models.Effects;

namespace CardCleaner.Tests.Features.Card.Models.Effects;

/// <summary>
///     The comparison view labels each card with a rarity and a tier, so the signature behind a cell has to produce
///     exactly that rarity and that tier through the same rules the card generator uses.
/// </summary>
[TestSuite]
public class CardEffectComparisonGridTest
{
    [TestCase]
    public static void EveryCellSignatureHasTheRarityItIsLabelledWith()
    {
        foreach (var (rarity, tier) in CardEffectComparisonGrid.Cells)
        {
            var signature = CardEffectComparisonGrid.SignatureFor(rarity, tier);

            AssertThat(SignatureCardHelper.DetermineRarity(new[] { signature }))
                .OverrideFailureMessage($"{rarity} / {tier} rolls the wrong rarity")
                .IsEqual(rarity);
        }
    }

    [TestCase]
    public static void EveryCellSignatureHasTheTierItIsLabelledWith()
    {
        foreach (var (rarity, tier) in CardEffectComparisonGrid.Cells)
        {
            var signature = CardEffectComparisonGrid.SignatureFor(rarity, tier);
            var intensity = CardEffectMapping.IntensityOf(signature.Elements);

            AssertThat(CardEffectMapping.TierFor(intensity))
                .OverrideFailureMessage($"{rarity} / {tier} has the wrong tier at intensity {intensity}")
                .IsEqual(tier);
        }
    }

    [TestCase]
    public static void OnlyEpicAndLegendaryAtDormantIntensityAreLeftEmpty()
    {
        var empty = (from rarity in Enum.GetValues<CardRarity>()
            from tier in Enum.GetValues<IntensityTier>()
            where !CardEffectComparisonGrid.Has(rarity, tier)
            select (rarity, tier)).ToArray();

        AssertThat(empty).IsEqual(new[]
        {
            (CardRarity.Epic, IntensityTier.Dormant),
            (CardRarity.Legendary, IntensityTier.Dormant)
        });
        AssertThat(CardEffectComparisonGrid.Cells.Count).IsEqual(13);
    }

    [TestCase]
    public static void AnEmptyCellHasNoSignature()
    {
        AssertThrown(() => CardEffectComparisonGrid.SignatureFor(CardRarity.Epic, IntensityTier.Dormant))
            .IsInstanceOf<ArgumentException>();
    }

    [TestCase]
    public static void TheCommonDormantCellIsTheOrdinaryPackCard()
    {
        var signature = CardEffectComparisonGrid.SignatureFor(CardRarity.Common, IntensityTier.Dormant);

        AssertThat(signature.Elements.All(element => element == 0f)).IsTrue();
    }

    [TestCase]
    public static void NoTwoCellsShareASignature()
    {
        var keys = CardEffectComparisonGrid.Cells
            .Select(cell => string.Join(",", CardEffectComparisonGrid.SignatureFor(cell.Rarity, cell.Tier).Elements))
            .ToArray();

        AssertThat(keys.Distinct().Count()).IsEqual(keys.Length);
    }
}
