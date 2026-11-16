using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     The price the shop asks for a product: market value adjusted by a markup percentage picked from a fixed ladder.
/// </summary>
public static class AskingPrice
{
    public static readonly IReadOnlyList<int> MarkupLadder = new[] { -20, -10, 0, 10, 20, 30, 40, 50 };

    public const int DefaultMarkupPercent = 0;

    /// <summary>The asking price for a market value and markup, never below 1.</summary>
    public static int For(int marketValue, int markupPercent)
    {
        return Math.Max(1, (int)Math.Round(marketValue * (100 + markupPercent) / 100.0, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    ///     The first ladder entry after <paramref name="markupPercent" /> (wrapping around at the top) that asks a
    ///     different price for <paramref name="marketValue" />. Prices are whole numbers, so on a cheap item several
    ///     steps round to the same price (a booster at 4 asks 4 at -10%, 0% and +10%); those are skipped, so every
    ///     click on a price tag changes the price. When no step changes it, the plain next entry is returned.
    /// </summary>
    public static int NextMarkup(int marketValue, int markupPercent)
    {
        var index = IndexOf(markupPercent);
        if (index < 0) return DefaultMarkupPercent;

        var now = For(marketValue, markupPercent);
        for (var step = 1; step < MarkupLadder.Count; step++)
        {
            var next = MarkupLadder[(index + step) % MarkupLadder.Count];
            if (For(marketValue, next) != now) return next;
        }

        return MarkupLadder[(index + 1) % MarkupLadder.Count];
    }

    /// <summary>The markup itself when it is on the ladder, otherwise the default.</summary>
    public static int Sanitise(int markupPercent)
    {
        return IndexOf(markupPercent) >= 0 ? markupPercent : DefaultMarkupPercent;
    }

    /// <summary>The asking price as printed large on a price tag.</summary>
    public static string Readout(int marketValue, int markupPercent) => $"{For(marketValue, markupPercent)}";

    /// <summary>
    ///     The small print under the price on a tag: the market value and the markup that sets the price.
    /// </summary>
    public static string Detail(int marketValue, int markupPercent)
    {
        var sign = markupPercent >= 0 ? "+" : "";
        return $"market {marketValue} {sign}{markupPercent}%";
    }

    /// <summary>
    ///     What a click on a price tag does: names the price the next price-changing ladder step sets and the price
    ///     now. At the top of the ladder the next step wraps to the bottom, so the prompt then names a lower price.
    /// </summary>
    public static string StepPrompt(int marketValue, int markupPercent)
    {
        var next = NextMarkup(marketValue, markupPercent);
        var sign = next >= 0 ? "+" : "";
        return $"Set price to {For(marketValue, next)} ({sign}{next}%), now {For(marketValue, markupPercent)}";
    }

    private static int IndexOf(int markupPercent)
    {
        for (var i = 0; i < MarkupLadder.Count; i++)
        {
            if (MarkupLadder[i] == markupPercent) return i;
        }

        return -1;
    }
}
