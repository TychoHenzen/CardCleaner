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
        AssertThat(AskingPrice.NextMarkup(10, 0)).IsEqual(10);
        AssertThat(AskingPrice.NextMarkup(10, 50)).IsEqual(-20);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CheapItemSkipsStepsThatKeepThePrice()
    {
        // A booster at 4 asks 4 at -10%, 0% and +10%, so a click from 0% goes straight to +20% (5).
        AssertThat(AskingPrice.NextMarkup(4, 0)).IsEqual(20);
        AssertThat(AskingPrice.NextMarkup(4, -20)).IsEqual(-10);
        // At 1 only +50% asks more (2), and from there the ladder wraps back to 1.
        AssertThat(AskingPrice.NextMarkup(1, 0)).IsEqual(50);
        AssertThat(AskingPrice.NextMarkup(1, 50)).IsEqual(-20);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryStepChangesThePrice()
    {
        for (var value = 1; value <= 60; value++)
        {
            foreach (var markup in AskingPrice.MarkupLadder)
            {
                var next = AskingPrice.NextMarkup(value, markup);
                AssertBool(AskingPrice.For(value, next) != AskingPrice.For(value, markup))
                    .OverrideFailureMessage($"market {value}: {markup}% and {next}% ask the same price").IsTrue();
            }
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OffLadderMarkupResetsToDefault()
    {
        AssertThat(AskingPrice.NextMarkup(10, 7)).IsEqual(AskingPrice.DefaultMarkupPercent);
        AssertThat(AskingPrice.Sanitise(7)).IsEqual(AskingPrice.DefaultMarkupPercent);
        AssertThat(AskingPrice.Sanitise(30)).IsEqual(30);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ReadoutIsTheAskAndTheDetailNamesMarketAndMarkup()
    {
        AssertThat(AskingPrice.Readout(10, 20)).IsEqual("12");
        AssertThat(AskingPrice.Detail(10, 20)).IsEqual("market 10 +20%");
        AssertThat(AskingPrice.Readout(10, -10)).IsEqual("9");
        AssertThat(AskingPrice.Detail(10, -10)).IsEqual("market 10 -10%");
        AssertThat(AskingPrice.Detail(10, 0)).IsEqual("market 10 +0%");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void StepPromptNamesTheNextPriceAndWrapsToTheLowest()
    {
        AssertThat(AskingPrice.StepPrompt(10, 0)).IsEqual("Set price to 11 (+10%), now 10");
        AssertThat(AskingPrice.StepPrompt(10, 50)).IsEqual("Set price to 8 (-20%), now 15");
        AssertThat(AskingPrice.StepPrompt(4, 0)).IsEqual("Set price to 5 (+20%), now 4");
    }
}
