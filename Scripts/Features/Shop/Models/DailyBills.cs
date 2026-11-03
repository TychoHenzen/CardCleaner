using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     The settlement rule for a day's bills. It takes the lines it is given; collecting them from sources is
///     <c>DailyBillBook</c>'s job.
/// </summary>
public static class DailyBills
{
    /// <summary>
    ///     Charges the carried debt and the day's lines against the balance. Lines with a non-positive amount are
    ///     dropped. The debt is charged first, so <see cref="BillSettlement.Paid" /> covers the carried debt before the
    ///     lines; the split is only reported, and one paid amount is charged.
    /// </summary>
    public static BillSettlement Settle(IReadOnlyList<BillLine> lines, int balance, int carriedDebt = 0)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var carriedIn = Math.Max(carriedDebt, 0);
        var kept = lines.Where(line => line.Amount > 0).ToList();
        var total = carriedIn + kept.Sum(line => line.Amount);
        var paid = Math.Min(total, Math.Max(balance, 0));
        return new BillSettlement(carriedIn, total, paid, total - paid, kept);
    }
}
