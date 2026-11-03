using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class ShoppingPlannerTest
{
    private const ulong Seed = 400UL;

    private static ShelfOffer Offer(string id, int asking, int market) => new(id, asking, market);

    [TestCase]
    [TestCategory("Unit")]
    public static void PlanWithNoOffersIsEmpty()
    {
        var picks = ShoppingPlanner.Plan(
            Array.Empty<ShelfOffer>(), new HashSet<string>(), 3, new Pcg32Random(Seed));

        AssertThat(picks.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void PlanWithWishZeroIsEmpty()
    {
        var offers = new[] { Offer("a", 4, 4), Offer("b", 4, 4) };

        var picks = ShoppingPlanner.Plan(offers, new HashSet<string>(), 0, new Pcg32Random(Seed));

        AssertThat(picks.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void PlanOfWish2Over5OffersHas2DistinctOfferIds()
    {
        var offers = new[]
        {
            Offer("a", 4, 4), Offer("b", 4, 4), Offer("c", 4, 4), Offer("d", 4, 4), Offer("e", 4, 4),
        };

        var picks = ShoppingPlanner.Plan(offers, new HashSet<string>(), 2, new Pcg32Random(Seed));

        AssertThat(picks.Count).IsEqual(2);
        AssertThat(picks.Select(pick => pick.Offer.OfferId).Distinct().Count()).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void PlanOfWish10Over3OffersHas3()
    {
        var offers = new[] { Offer("a", 4, 4), Offer("b", 4, 4), Offer("c", 4, 4) };

        var picks = ShoppingPlanner.Plan(offers, new HashSet<string>(), 10, new Pcg32Random(Seed));

        AssertThat(picks.Count).IsEqual(3);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ClaimedOfferIsNeverPlanned()
    {
        var offers = new[] { Offer("a", 4, 4), Offer("b", 4, 4), Offer("c", 4, 4) };

        var picks = ShoppingPlanner.Plan(offers, new HashSet<string> { "b" }, 3, new Pcg32Random(Seed));

        AssertThat(picks.Any(pick => pick.Offer.OfferId == "b")).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SameSeedGivesEqualPlans()
    {
        var offers = new[] { Offer("a", 4, 4), Offer("b", 6, 4), Offer("c", 5, 4), Offer("d", 4, 4) };

        var first = ShoppingPlanner.Plan(offers, new HashSet<string>(), 3, new Pcg32Random(Seed));
        var second = ShoppingPlanner.Plan(offers, new HashSet<string>(), 3, new Pcg32Random(Seed));

        AssertThat(first.Select(p => (p.Offer.OfferId, p.Outcome.Buys))
            .SequenceEqual(second.Select(p => (p.Offer.OfferId, p.Outcome.Buys)))).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OfferAtMarketPriceBuys()
    {
        var offers = new[] { Offer("a", 4, 4) };

        var picks = ShoppingPlanner.Plan(offers, new HashSet<string>(), 1, new Pcg32Random(Seed));

        AssertThat(picks[0].Outcome.Buys).IsTrue();
        AssertThat(picks[0].Outcome.Reason).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OfferAtTwiceMarketPriceIsRefused()
    {
        var offers = new[] { Offer("a", 8, 4) };

        var picks = ShoppingPlanner.Plan(offers, new HashSet<string>(), 1, new Pcg32Random(Seed));

        AssertThat(picks[0].Outcome.Buys).IsFalse();
        AssertThat(picks[0].Outcome.Reason).IsEqual(RefusalReason.PriceTooHigh);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OfferWithNoMarketValueIsDropped()
    {
        var offers = new[] { Offer("a", 4, 0), Offer("b", 4, -1) };

        var picks = ShoppingPlanner.Plan(offers, new HashSet<string>(), 2, new Pcg32Random(Seed));

        AssertThat(picks.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OfferWithNegativeAskingPriceIsDropped()
    {
        var offers = new[] { Offer("a", -1, 4) };

        var picks = ShoppingPlanner.Plan(offers, new HashSet<string>(), 1, new Pcg32Random(Seed));

        AssertThat(picks.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void DuplicateOfferIdIsPlannedOnce()
    {
        var offers = new[] { Offer("a", 4, 4), Offer("a", 4, 4) };

        var picks = ShoppingPlanner.Plan(offers, new HashSet<string>(), 2, new Pcg32Random(Seed));

        AssertThat(picks.Count).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NegativeWishThrows()
    {
        var offers = new[] { Offer("a", 4, 4) };

        AssertThrown(() => ShoppingPlanner.Plan(offers, new HashSet<string>(), -1, new Pcg32Random(Seed)))
            .IsInstanceOf<ArgumentOutOfRangeException>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void DrawWishStaysWithin1To3AndReachesBothEnds()
    {
        var rng = new Pcg32Random(Seed);
        var draws = Enumerable.Range(0, 200).Select(_ => ShoppingPlanner.DrawWish(rng)).ToList();

        AssertThat(draws.All(wish => wish >= ShoppingPlanner.MinWish && wish <= ShoppingPlanner.MaxWish)).IsTrue();
        AssertThat(draws.Contains(ShoppingPlanner.MinWish)).IsTrue();
        AssertThat(draws.Contains(ShoppingPlanner.MaxWish)).IsTrue();
    }
}
