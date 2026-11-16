using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     The end-of-day report as the lines a reader sees: a title, a column of figures (label and signed amount) and
///     one line per complaint reason. Money in is shown with a plus, bills with a minus, and the debt lines only
///     when there is debt.
/// </summary>
public static class DayReportSheet
{
    public const string NoComplaints = "No complaints";

    public static string Title(DayReport report) => $"Day {report.Day} closed";

    /// <summary>
    ///     The figures in reading order: takings, debt carried in, each bill, unpaid debt, the day's net.
    /// </summary>
    public static IReadOnlyList<(string Label, string Value)> Figures(DayReport report)
    {
        var settlement = report.Settlement;
        var rows = new List<(string, string)> { ("Takings", Signed(report.Takings)) };
        if (settlement.CarriedIn > 0)
            rows.Add(("Debt from yesterday", Signed(-settlement.CarriedIn)));
        rows.AddRange(settlement.Lines.Select(line => (line.Name, Signed(-line.Amount))));
        if (settlement.Unpaid > 0)
            rows.Add(("Unpaid, owed tomorrow", settlement.Unpaid.ToString()));
        rows.Add(("Net for the day", Signed(report.Takings - settlement.Paid)));
        return rows;
    }

    /// <summary>
    ///     One line per complaint reason with its count, or <see cref="NoComplaints" /> when there were none.
    /// </summary>
    public static IReadOnlyList<string> Complaints(DayReport report) =>
        report.Complaints.Count == 0
            ? new[] { NoComplaints }
            : report.Complaints.Select(complaint => $"{complaint.Reason} x{complaint.Count}").ToArray();

    private static string Signed(int amount) => amount > 0 ? $"+{amount}" : amount.ToString();
}
