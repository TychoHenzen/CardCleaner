using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Shop.Customers;

/// <summary>
///     Builds the minutes at which customers arrive during a shop day. Arrivals fall in
///     [open, close - LastEntryMarginMinutes), so nobody arrives in the last half hour. The window is cut into one
///     equal slice per customer and each customer arrives at a random minute of its own slice, so the flow is
///     steady: the first customer comes within the first slice after opening and no two gaps bunch up.
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

        var span = (long)(closeMinute - LastEntryMarginMinutes - openMinute);
        var arrivals = new int[customerCount];
        for (var i = 0; i < customerCount; i++)
        {
            var first = openMinute + (int)(span * i / customerCount);
            var last = Math.Max(first, openMinute + (int)(span * (i + 1) / customerCount) - 1);
            arrivals[i] = rng.RandiRange(first, last);
        }

        return arrivals;
    }
}
