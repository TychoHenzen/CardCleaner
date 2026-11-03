using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>Pure payment arithmetic for the checkout: cash tendered, change and exact-total checks.</summary>
public static class PaymentRules
{
    /// <summary>Note values a customer can hand over.</summary>
    public static readonly IReadOnlyList<int> Notes = new[] { 5, 10, 20, 50, 100 };

    /// <summary>Coin and note values the cashier can hand back as change.</summary>
    public static readonly IReadOnlyList<int> ChangeDenominations = new[] { 1, 5, 10, 20, 50 };

    /// <summary>
    /// The smallest sum of notes that covers the total. Every note is a multiple of 5, and every
    /// multiple of 5 is a sum of 5-notes, so this is the total rounded up to the next multiple of 5.
    /// </summary>
    public static int CashTendered(int total)
    {
        ValidateTotal(total);
        return (total + 4) / 5 * 5;
    }

    public static int Change(int tendered, int total)
    {
        ValidateTotal(total);
        if (tendered < total)
        {
            throw new ArgumentOutOfRangeException(nameof(tendered), tendered, "Tendered is below the total.");
        }

        return tendered - total;
    }

    public static bool CardTotalMatches(int typed, int total)
    {
        ValidateTotal(total);
        return typed == total;
    }

    /// <summary>True when every picked value is a change denomination and the picks sum to the change due.</summary>
    public static bool ChangeMatches(IReadOnlyCollection<int> picked, int tendered, int total)
    {
        ArgumentNullException.ThrowIfNull(picked);
        var due = Change(tendered, total);
        var sum = 0;
        foreach (var value in picked)
        {
            if (!ChangeDenominations.Contains(value))
            {
                return false;
            }

            sum += value;
        }

        return sum == due;
    }

    private static void ValidateTotal(int total)
    {
        if (total <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(total), total, "A checkout total must be above zero.");
        }
    }
}
