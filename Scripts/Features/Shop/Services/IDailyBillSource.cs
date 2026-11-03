using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>Supplies the bill lines one source charges for a given day.</summary>
public interface IDailyBillSource
{
    /// <summary>The lines this source charges for <paramref name="day"/>.</summary>
    IEnumerable<BillLine> GetBillLines(int day);
}
