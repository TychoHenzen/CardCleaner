using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using Godot;

namespace CardCleaner.Tests.Features.Packs.Services;

[TestSuite]
[RequireGodotRuntime]
public class CardPackGeneratorTest
{
    private const ulong Seed = 20240531;
    private const int BoxesForRateCheck = 400;

    /// <summary>Roughly 4 standard deviations of the special count over <see cref="BoxesForRateCheck" /> boxes.</summary>
    private const double RateTolerance = 0.20;

    private static CardPackGenerator Seeded(ulong seed) => new(new RandomNumberGenerator { Seed = seed });

    [TestCase]
    [TestCategory("Unit")]
    public static void BoosterHoldsEightCards()
    {
        AssertThat(Seeded(Seed).OpenBooster().Length).IsEqual(8);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void BoxHoldsFiveHundredAndTwelveCards()
    {
        AssertThat(Seeded(Seed).OpenBox().Length).IsEqual(CardContainerLayout.CardsPerBox);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SpecialOddsAreOneInFiveHundredAndTwelve()
    {
        AssertThat(CardPackGenerator.SpecialCardOdds).IsEqual(512);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SeededSpecialRateIsWithinToleranceOfOneInFiveHundredAndTwelve()
    {
        var generator = Seeded(Seed);
        var specials = 0;

        for (var i = 0; i < BoxesForRateCheck; i++)
            specials += generator.OpenBox().Count(c => c.HasMagicalPotential());

        // One special per box on average, so BoxesForRateCheck specials are expected.
        AssertThat(specials).IsBetween(
            (int)(BoxesForRateCheck * (1 - RateTolerance)),
            (int)(BoxesForRateCheck * (1 + RateTolerance)));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CommonCardsHaveAnAllZeroSignature()
    {
        var common = Seeded(Seed).OpenBox().Where(c => !c.HasMagicalPotential()).ToArray();

        AssertThat(common.Length).IsGreater(0);
        foreach (var card in common)
            AssertThat(card.Elements).IsEqual(new float[8]);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SpecialCardsHaveANonZeroSignature()
    {
        var generator = Seeded(Seed);
        var special = Enumerable.Range(0, BoxesForRateCheck)
            .SelectMany(_ => generator.OpenBox())
            .Where(c => c.HasMagicalPotential())
            .ToArray();

        AssertThat(special.Length).IsGreater(0);
        foreach (var card in special)
            AssertThat(card.Elements.Any(e => e != 0f)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SameSeedReproducesTheSamePositionsOfSpecialCards()
    {
        static int[] SpecialPositions(CardPackGenerator generator) =>
            generator.OpenBox().Select((c, i) => (c, i)).Where(t => t.c.HasMagicalPotential()).Select(t => t.i)
                .ToArray();

        // Twenty boxes hold about twenty special cards, so the comparison is not vacuous.
        var first = Seeded(Seed);
        var second = Seeded(Seed);
        var specialSeen = 0;
        for (var box = 0; box < 20; box++)
        {
            var a = SpecialPositions(first);
            var b = SpecialPositions(second);
            AssertThat(a).IsEqual(b);
            specialSeen += a.Length;
        }

        AssertThat(specialSeen).IsGreater(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SeededRunsProduceIdenticalSignatures()
    {
        var a = Seeded(Seed).OpenBox().Select(c => c.Elements).ToArray();
        var b = Seeded(Seed).OpenBox().Select(c => c.Elements).ToArray();

        for (var i = 0; i < a.Length; i++)
            AssertThat(a[i]).IsEqual(b[i]);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CommonSignatureHasNoMagicalPotential()
    {
        AssertThat(CardPackGenerator.CommonSignature().HasMagicalPotential()).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SignatureWithAnyNonZeroElementHasMagicalPotential()
    {
        for (var i = 0; i < 8; i++)
            AssertThat(new CardSignature { [i] = 0.4f }.HasMagicalPotential()).IsTrue();
    }
}
