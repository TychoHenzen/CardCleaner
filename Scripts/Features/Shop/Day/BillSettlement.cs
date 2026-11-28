using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Shop.Day;

/// <summary>
///     The result of settling a day's bills. <see cref="Unpaid" /> is the debt carried into the next day.
/// </summary>
public sealed record BillSettlement(
    int CarriedIn,
    int Total,
    int Paid,
    int Unpaid,
    IReadOnlyList<BillLine> Lines);
