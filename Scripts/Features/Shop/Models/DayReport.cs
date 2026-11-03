using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>The end-of-day summary: takings, the bill settlement and the complaints grouped by reason.</summary>
public sealed record DayReport(
    int Day,
    int Takings,
    BillSettlement Settlement,
    IReadOnlyList<(string Reason, int Count)> Complaints);
