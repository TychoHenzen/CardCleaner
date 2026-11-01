using System;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class BuyDecisionTest
{
    [TestCase(1.0, 1.0, 0.0)]
    [TestCase(1.0, 1.0, 0.999)]
    [TestCase(0.5, 1.0, 0.0)]
    [TestCase(0.5, 1.0, 0.999)]
    [TestCategory("Unit")]
    public static void AskingAtOrBelowMarketAlwaysBuys(double asking, double market, double roll)
    {
        AssertThat(BuyDecision.Decide(asking, market, roll).Buys).IsTrue();
    }

    [TestCase(1.25, 1.0, 0.49, true)]
    [TestCase(1.25, 1.0, 0.5, false)]
    [TestCategory("Unit")]
    public static void MidRatioBuysOnlyBelowItsChance(double asking, double market, double roll, bool expected)
    {
        AssertThat(BuyDecision.Decide(asking, market, roll).Buys).IsEqual(expected);
    }

    [TestCase(1.5, 1.0)]
    [TestCase(2.0, 1.0)]
    [TestCategory("Unit")]
    public static void AskingFromOneAndAHalfTimesMarketNeverBuys(double asking, double market)
    {
        var outcome = BuyDecision.Decide(asking, market, 0.0);

        AssertThat(outcome.Buys).IsFalse();
        AssertThat(outcome.Reason).IsEqual(RefusalReason.PriceTooHigh);
    }

    [TestCase(0.0, 0.0, 0.5)]
    [TestCase(0.0, -1.0, 0.5)]
    [TestCase(-1.0, 1.0, 0.5)]
    [TestCase(1.0, 1.0, -0.1)]
    [TestCase(1.0, 1.0, 1.0)]
    [TestCategory("Unit")]
    public static void InvalidInputsAreRejected(double asking, double market, double roll)
    {
        AssertThrown(() => BuyDecision.Decide(asking, market, roll))
            .IsInstanceOf<ArgumentOutOfRangeException>();
    }
}
