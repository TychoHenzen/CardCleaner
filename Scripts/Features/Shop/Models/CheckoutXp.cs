using System;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     The XP a checkout earns from the amount paid: one XP per 10 money, and every paid sale earns at least 1.
///     Pure rules, no engine types.
/// </summary>
public static class CheckoutXp
{
    public static int For(int amountPaid) => amountPaid <= 0 ? 0 : Math.Max(1, amountPaid / 10);
}
