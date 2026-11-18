using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

/// <summary>
///     The pricing screen's figures: the market price, the wholesale cost and profit per sale when the stock was bought
///     in, and the share of customers who buy at the price, which follows <see cref="BuyDecision" />.
/// </summary>
[TestSuite]
public class PricingSheetTest
{
    [TestCase]
    public static void BoughtInStockShowsItsCostAndProfit()
    {
        var lines = PricingSheet.Lines(market: 4, unitCost: 2, price: 5);

        AssertThat(string.Join("|", lines)).IsEqual(
            "Market price: 4|Wholesale cost: 2|Profit per sale: 3|"
            + $"Customers who buy at this price: {PricingSheet.BuyPercent(4, 5)}%");
    }

    [TestCase]
    public static void StockWithNoCostShowsNoProfit()
    {
        var lines = PricingSheet.Lines(market: 10, unitCost: null, price: 10);

        AssertThat(string.Join("|", lines)).IsEqual("Market price: 10|Customers who buy at this price: 100%");
    }

    [TestCase]
    public static void ALossShowsAsANegativeProfit()
    {
        var lines = PricingSheet.Lines(market: 4, unitCost: 2, price: 1);

        AssertThat(lines[2]).IsEqual("Profit per sale: -1");
    }

    [TestCase]
    public static void TheBuyShareFollowsTheBuyDecision()
    {
        foreach (var price in new[] { 2, 4, 5, 6, 7, 40 })
        {
            var expected = (int)System.Math.Floor(BuyDecision.Chance(price, 4) * 100.0);
            AssertThat(PricingSheet.BuyPercent(4, price)).IsEqual(expected);
        }

        AssertThat(PricingSheet.BuyPercent(4, 4)).IsEqual(100);
        AssertThat(PricingSheet.BuyPercent(4, 40)).IsEqual(0);
    }
}
