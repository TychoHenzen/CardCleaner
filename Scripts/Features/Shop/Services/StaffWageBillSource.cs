using System.Collections.Generic;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     Charges one wage line per hired role each day. It is not registered as <see cref="IDailyBillSource" />: the
///     <see cref="DailyBillBook" /> takes it as its own constructor parameter.
/// </summary>
[Service(typeof(StaffWageBillSource))]
public sealed class StaffWageBillSource(IStaffService staff) : IDailyBillSource
{
    /// <summary>The wage line of every hired role for <paramref name="day"/>, in hire order.</summary>
    public IEnumerable<BillLine> GetBillLines(int day)
    {
        foreach (var role in staff.HiredRoles)
            yield return new BillLine($"{role.DisplayName} wage", role.DailyWage);
    }
}
