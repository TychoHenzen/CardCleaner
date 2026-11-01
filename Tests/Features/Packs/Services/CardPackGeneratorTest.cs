using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using CardCleaner.Scripts.Core.Randomness;
using Godot;

namespace CardCleaner.Tests.Features.Packs.Services;

[TestSuite]
[RequireGodotRuntime]
public class CardPackGeneratorTest
{
    private const ulong Seed = 20240531;
    private const int BoxesForRateCheck = 400;
    private const int CardsPerBox = 512;

    /// <summary>Roughly 4 standard deviations of the special count over <see cref="BoxesForRateCheck" /> boxes.</summary>
    private const double RateTolerance = 0.20;

    /// <summary>Share of 512-card boxes holding at least one special card: 1 - (199/200)^512, about 0.923.</summary>
    private const double ExpectedBoxShare = 0.923;

    /// <summary>Tolerance around <see cref="ExpectedBoxShare" />: about 4.5 standard deviations over 400 boxes.</summary>
    private const double BoxShareTolerance = 0.06;

    /// <summary>The user's lower target for the share of boxes holding at least one special card.</summary>
    private const double MinimumBoxShare = 0.90;

    private static CardPackGenerator Seeded(ulong seed) => new(RandomSources.Create(seed));

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
    public static void SpecialOddsAreOneInTwoHundred()
    {
        AssertThat(CardPackGenerator.SpecialCardOdds).IsEqual(200);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SeededSpecialRateIsWithinToleranceOfOneInTwoHundred()
    {
        var generator = Seeded(Seed);
        var specials = 0;

        for (var i = 0; i < BoxesForRateCheck; i++)
            specials += generator.OpenBox().Count(c => c.HasMagicalPotential());

        // 512 cards at 1 in 200 per box is 2.56 specials per box, so 1024 are expected over 400 boxes.
        var expected = BoxesForRateCheck * CardsPerBox / CardPackGenerator.SpecialCardOdds;
        AssertThat(specials).IsBetween(
            (int)(expected * (1 - RateTolerance)),
            (int)(expected * (1 + RateTolerance)));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AnalyticBoxShareWithSpecialCardIsWithinUserTarget()
    {
        var analytic = 1.0 - System.Math.Pow(1.0 - 1.0 / CardPackGenerator.SpecialCardOdds, CardsPerBox);
        AssertThat(analytic).IsBetween(MinimumBoxShare, 0.95);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SeededBoxShareWithSpecialCardIsNearAnalyticShare()
    {
        var generator = Seeded(Seed);
        var boxesWithSpecial = 0;

        for (var i = 0; i < BoxesForRateCheck; i++)
        {
            if (generator.OpenBox().Any(c => c.HasMagicalPotential()))
                boxesWithSpecial++;
        }

        // No floor at the target here: a 400-box sample falls below 0.90 by chance about 2 percent of the time.
        var share = (double)boxesWithSpecial / BoxesForRateCheck;
        AssertThat(share).IsBetween(ExpectedBoxShare - BoxShareTolerance, ExpectedBoxShare + BoxShareTolerance);
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
            AssertThat(new CardSignature().With(i, 0.4f).HasMagicalPotential()).IsTrue();
    }
}
