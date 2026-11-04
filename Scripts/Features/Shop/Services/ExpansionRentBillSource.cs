using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     Charges one rent bill line per owned expansion lot, named "Rent: [lot name]". Reads the plan when asked, so a
///     lot bought today is charged from the next close. Pure rules, no engine types.
/// </summary>
[Service(typeof(ExpansionRentBillSource))]
public sealed class ExpansionRentBillSource(IShopExpansion expansion) : IDailyBillSource
{
    /// <summary>The rent lines for the owned lots. The day does not change the rent.</summary>
    public IEnumerable<BillLine> GetBillLines(int day)
    {
        var plan = expansion.Plan;
        return plan.Lots
            .Where(lot => plan.Owned.Contains(lot.Id))
            .Select(lot => new BillLine($"Rent: {lot.DisplayName}", lot.DailyRent))
            .ToList();
    }
}
