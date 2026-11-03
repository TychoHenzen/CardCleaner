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

    /// <summary>The ladder entry after <paramref name="markupPercent" />, wrapping around at the top.</summary>
    public static int NextMarkup(int markupPercent)
    {
        var index = IndexOf(markupPercent);
        if (index < 0) return DefaultMarkupPercent;
        return MarkupLadder[(index + 1) % MarkupLadder.Count];
    }

    /// <summary>The markup itself when it is on the ladder, otherwise the default.</summary>
    public static int Sanitise(int markupPercent)
    {
        return IndexOf(markupPercent) >= 0 ? markupPercent : DefaultMarkupPercent;
    }

    public static string Readout(int marketValue, int markupPercent)
    {
        var sign = markupPercent >= 0 ? "+" : "";
        return $"Ask {For(marketValue, markupPercent)} (market {marketValue}, {sign}{markupPercent}%)";
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
