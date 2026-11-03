using System.Collections.Generic;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>Charges the shop's rent once per day.</summary>
[Service(typeof(RentBillSource))]
public sealed class RentBillSource : IDailyBillSource
{
    /// <summary>The rent charged each day when nothing overrides it.</summary>
    public const int DefaultRentPerDay = 40;

    /// <summary>
    ///     ASSUMPTION (Low): rent is a settable property, not a node export, because the [Service] scan builds
    ///     this class and cannot reach a node's export. It is registered under its own type only, never under
    ///     IDailyBillSource: that type is a single container slot and a later source (wages, #407) would replace it.
    /// </summary>
    public int RentPerDay { get; set; } = DefaultRentPerDay;

    /// <summary>The one rent line for <paramref name="day"/>.</summary>
    public IEnumerable<BillLine> GetBillLines(int day)
    {
        yield return new BillLine("Rent", RentPerDay);
    }
}
