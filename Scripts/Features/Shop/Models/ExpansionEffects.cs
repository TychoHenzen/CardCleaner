using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>What the owned expansion lots add to the shop. Pure rules, no engine types.</summary>
public static class ExpansionEffects
{
    /// <summary>
    ///     The extra customers per day of every owned lot. An owned id missing from the table adds nothing.
    /// </summary>
    public static int ExtraCustomers(ExpansionPlan plan) => OwnedLots(plan).Sum(lot => lot.ExtraCustomers);

    private static IEnumerable<ExpansionLot> OwnedLots(ExpansionPlan plan) =>
        plan.Lots.Where(lot => plan.Owned.Contains(lot.Id));
}

