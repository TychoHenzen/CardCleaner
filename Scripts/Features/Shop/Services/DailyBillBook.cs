using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     The one place a day's bill lines are collected from every source. A later bill slice adds its source as one
///     more constructor parameter here, registered under its own type. The staff wages come from
///     <see cref="StaffWageBillSource" />. This class is built once, on its first resolve.
/// </summary>
[Service(typeof(IDailyBillBook))]
internal sealed class DailyBillBook : IDailyBillBook
{
    private readonly IReadOnlyList<IDailyBillSource> _sources;

    public DailyBillBook(RentBillSource rent, ExpansionRentBillSource expansionRent, StaffWageBillSource staffWage)
        : this(new IDailyBillSource[] { rent, expansionRent, staffWage })
    {
    }

    internal DailyBillBook(IReadOnlyList<IDailyBillSource> sources)
    {
        _sources = sources;
    }

    /// <summary>
    ///     The lines of every source for <paramref name="day"/>, in constructor order, non-positive lines dropped.
    /// </summary>
    public IReadOnlyList<BillLine> LinesFor(int day) =>
        _sources
            .SelectMany(source => source.GetBillLines(day))
            .Where(line => line.Amount > 0)
            .ToList();

    /// <summary>The sum of the bill lines charged for <paramref name="day"/>.</summary>
    public int TotalFor(int day) => LinesFor(day).Sum(line => line.Amount);
}
