using System;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;

namespace CardCleaner.Tests.Features.Card.Models;

/// <summary>
///     The comparison view labels each card with a rarity and a tier, so the signature behind a cell has to produce
///     exactly that rarity and that tier through the same rules the card generator uses.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardEffectComparisonGridTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void EveryCellSignatureHasTheRarityItIsLabelledWith()
    {
        foreach (var rarity in Enum.GetValues<CardRarity>())
        foreach (var tier in Enum.GetValues<IntensityTier>())
        {
            var signature = CardEffectComparisonGrid.SignatureFor(rarity, tier);

            AssertThat(SignatureCardHelper.DetermineRarity(new[] { signature }))
                .OverrideFailureMessage($"{rarity} / {tier} rolls the wrong rarity")
                .IsEqual(rarity);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryCellSignatureHasTheTierItIsLabelledWith()
    {
        foreach (var rarity in Enum.GetValues<CardRarity>())
        foreach (var tier in Enum.GetValues<IntensityTier>())
        {
            var signature = CardEffectComparisonGrid.SignatureFor(rarity, tier);
            var intensity = CardEffectMapping.IntensityOf(signature.Elements);

            AssertThat(CardEffectMapping.TierFor(intensity))
                .OverrideFailureMessage($"{rarity} / {tier} has the wrong tier at intensity {intensity}")
                .IsEqual(tier);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheCommonDormantCellIsTheOrdinaryPackCard()
    {
        var signature = CardEffectComparisonGrid.SignatureFor(CardRarity.Common, IntensityTier.Dormant);

        AssertThat(signature.Elements.All(element => element == 0f)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NoTwoCellsShareASignature()
    {
        var keys = (from rarity in Enum.GetValues<CardRarity>()
            from tier in Enum.GetValues<IntensityTier>()
            select string.Join(",", CardEffectComparisonGrid.SignatureFor(rarity, tier).Elements)).ToArray();

        AssertThat(keys.Distinct().Count()).IsEqual(keys.Length);
    }
}
