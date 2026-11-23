using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Customers;

namespace CardCleaner.Scripts.Features.Shop.Pricing;

/// <summary>
///     The figures the pricing screen shows for a price the player is weighing: the market price, the wholesale cost
///     and the profit on each sale when the stock was bought in, and the share of customers who buy at that price.
/// </summary>
public static class PricingSheet
{
    /// <summary>One line per figure, in the order the screen shows them.</summary>
    public static IReadOnlyList<string> Lines(int market, int? unitCost, int price)
    {
        var lines = new List<string> { $"Market price: {market}" };
        if (unitCost is { } cost)
        {
            lines.Add($"Wholesale cost: {cost}");
            lines.Add($"Profit per sale: {price - cost}");
        }

        lines.Add($"Customers who buy at this price: {BuyPercent(market, price)}%");
        return lines;
    }

    /// <summary>The chance a customer buys at this price, as a whole percentage rounded down.</summary>
    public static int BuyPercent(int market, int price) =>
        (int)Math.Floor(BuyDecision.Chance(price, market) * 100.0);
}
