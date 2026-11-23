using System;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Customers;
using CardCleaner.Scripts.Features.Shop.Pricing;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
public class BuyDecisionTest
{
    // The best price must pay noticeably more than the market price, or setting a price is not worth the trouble.
    private const double MinimumGainOverMarket = 1.1;

    [TestCase(1.0, 1.0, 0.0)]
    [TestCase(1.0, 1.0, 0.999)]
    [TestCase(0.5, 1.0, 0.0)]
    [TestCase(0.5, 1.0, 0.999)]
    public static void AskingAtOrBelowMarketAlwaysBuys(double asking, double market, double roll)
    {
        AssertThat(BuyDecision.Decide(asking, market, roll).Buys).IsTrue();
    }

    [TestCase(1.25, 1.0, 0.93, true)]
    [TestCase(1.25, 1.0, 0.94, false)]
    [TestCase(1.5, 1.0, 0.74, true)]
    [TestCase(1.5, 1.0, 0.75, false)]
    [TestCase(1.75, 1.0, 0.43, true)]
    [TestCase(1.75, 1.0, 0.44, false)]
    public static void AMarkupBuysOnlyBelowItsChance(double asking, double market, double roll, bool expected)
    {
        AssertThat(BuyDecision.Decide(asking, market, roll).Buys).IsEqual(expected);
    }

    [TestCase(2.0, 1.0)]
    [TestCase(3.0, 1.0)]
    public static void AskingTwiceTheMarketOrMoreNeverBuys(double asking, double market)
    {
        var outcome = BuyDecision.Decide(asking, market, 0.0);

        AssertThat(outcome.Buys).IsFalse();
        AssertThat(outcome.Reason).IsEqual(RefusalReason.PriceTooHigh);
    }

    /// <summary>
    ///     Expected profit of one customer looking at an item is (price - cost) times the chance they buy. A markup
    ///     must pay: the best whole price lies above the market price, below the price nobody pays, and earns at
    ///     least a tenth more than the market price. Cases: a booster and a pack at their wholesale cost, and cards,
    ///     which cost nothing on their own.
    /// </summary>
    [TestCase(ProductMarketPrices.BoosterPrice, 2)]
    [TestCase(ProductMarketPrices.PackPrice, 10)]
    [TestCase(CardValue.MinimumCardPrice, 0)]
    [TestCase(40, 0)]
    public static void TheMostProfitablePriceIsAMarkup(int market, int cost)
    {
        var ceiling = (int)(market * BuyDecision.ZeroChanceRatio);
        double Profit(int price) => (price - cost) * BuyDecision.Chance(price, market);

        var best = Enumerable.Range(market, ceiling - market + 1).MaxBy(Profit);

        AssertInt(best).IsGreater(market).IsLess(ceiling);
        AssertBool(Profit(best) >= Profit(market) * MinimumGainOverMarket)
            .OverrideFailureMessage($"best price {best} earns {Profit(best):F2}, market {market} earns "
                                    + $"{Profit(market):F2}")
            .IsTrue();
    }

    [TestCase(0.0, 0.0, 0.5)]
    [TestCase(0.0, -1.0, 0.5)]
    [TestCase(-1.0, 1.0, 0.5)]
    [TestCase(1.0, 1.0, -0.1)]
    [TestCase(1.0, 1.0, 1.0)]
    public static void InvalidInputsAreRejected(double asking, double market, double roll)
    {
        AssertThrown(() => BuyDecision.Decide(asking, market, roll))
            .IsInstanceOf<ArgumentOutOfRangeException>();
    }
}
