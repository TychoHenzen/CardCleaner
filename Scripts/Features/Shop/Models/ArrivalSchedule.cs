using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     Builds the minutes at which customers arrive during a shop day. Arrivals fall in
///     [open, close - LastEntryMarginMinutes), so nobody arrives in the last half hour.
/// </summary>
internal static class ArrivalSchedule
{
    /// <summary>Minutes before closing after which no customer arrives.</summary>
    public const int LastEntryMarginMinutes = 30;

    /// <summary>Draws the arrival minutes of <paramref name="customerCount"/> customers, sorted ascending.</summary>
    public static IReadOnlyList<int> Build(int openMinute, int closeMinute, int customerCount, IRandom rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (closeMinute - openMinute <= LastEntryMarginMinutes)
            throw new ArgumentOutOfRangeException(nameof(closeMinute), closeMinute,
                "The close must be more than the last-entry margin after the open.");
        if (customerCount < 0)
            throw new ArgumentOutOfRangeException(nameof(customerCount), customerCount,
                "Customer count cannot be negative.");

        var lastArrival = closeMinute - LastEntryMarginMinutes - 1;
        var arrivals = new int[customerCount];
        for (var i = 0; i < customerCount; i++)
            arrivals[i] = rng.RandiRange(openMinute, lastArrival);

        Array.Sort(arrivals);
        return arrivals;
    }
}
