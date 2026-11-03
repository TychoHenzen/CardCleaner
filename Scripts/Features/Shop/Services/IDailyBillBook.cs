using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>The one place a day's bill lines are collected from every source.</summary>
public interface IDailyBillBook
{
    /// <summary>All bill lines charged for <paramref name="day"/>.</summary>
    IReadOnlyList<BillLine> LinesFor(int day);

    /// <summary>The sum of the bill lines charged for <paramref name="day"/>.</summary>
    int TotalFor(int day);
}
