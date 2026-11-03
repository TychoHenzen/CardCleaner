using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     Turns the offers on the sales shelves into the ordered list of offers one customer tries. Offers claimed by
///     another customer, with no market value or with a negative asking price are never planned.
/// </summary>
internal static class ShoppingPlanner
{
    public const int MinWish = 1;
    public const int MaxWish = 3;
    private const double RollScale = 4294967296.0;

    public static int DrawWish(IRandom rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return rng.RandiRange(MinWish, MaxWish);
    }

    public static IReadOnlyList<PlannedPick> Plan(
        IReadOnlyList<ShelfOffer> offers, IReadOnlySet<string> claimed, int wish, IRandom rng)
    {
        ArgumentNullException.ThrowIfNull(offers);
        ArgumentNullException.ThrowIfNull(claimed);
        ArgumentNullException.ThrowIfNull(rng);
        if (wish < 0)
            throw new ArgumentOutOfRangeException(nameof(wish), wish, "Wish count cannot be negative.");

        var candidates = offers
            .Where(offer => offer.MarketValue > 0 && offer.AskingPrice >= 0 && !claimed.Contains(offer.OfferId))
            .DistinctBy(offer => offer.OfferId)
            .ToList();
        for (var i = candidates.Count - 1; i > 0; i--)
        {
            var j = rng.RandiRange(0, i);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        return candidates
            .Take(wish)
            .Select(offer => new PlannedPick(
                offer, BuyDecision.Decide(offer.AskingPrice, offer.MarketValue, rng.Randi() / RollScale)))
            .ToList();
    }
}
