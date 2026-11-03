using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class AskingPriceTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void DefaultMarkupAsksTheMarketValue()
    {
        AssertThat(AskingPrice.For(10, 0)).IsEqual(10);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void MarkupRaisesAndLowersThePrice()
    {
        AssertThat(AskingPrice.For(10, 50)).IsEqual(15);
        AssertThat(AskingPrice.For(10, -20)).IsEqual(8);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NeverBelowOne()
    {
        AssertThat(AskingPrice.For(1, -20)).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void LadderStepsUpAndWraps()
    {
        AssertThat(AskingPrice.NextMarkup(0)).IsEqual(10);
        AssertThat(AskingPrice.NextMarkup(50)).IsEqual(-20);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OffLadderMarkupResetsToDefault()
    {
        AssertThat(AskingPrice.NextMarkup(7)).IsEqual(AskingPrice.DefaultMarkupPercent);
        AssertThat(AskingPrice.Sanitise(7)).IsEqual(AskingPrice.DefaultMarkupPercent);
        AssertThat(AskingPrice.Sanitise(30)).IsEqual(30);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ReadoutNamesAskMarketAndMarkup()
    {
        AssertThat(AskingPrice.Readout(10, 20)).IsEqual("Ask 12 (market 10, +20%)");
        AssertThat(AskingPrice.Readout(10, -10)).IsEqual("Ask 9 (market 10, -10%)");
        AssertThat(AskingPrice.Readout(10, 0)).IsEqual("Ask 10 (market 10, +0%)");
    }
}
