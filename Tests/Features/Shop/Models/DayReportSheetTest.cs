using System;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class DayReportSheetTest
{
    private static DayReport Report(int takings, BillSettlement settlement, params (string, int)[] complaints) =>
        new(3, takings, settlement, complaints);

    [TestCase]
    [TestCategory("Unit")]
    public static void TitleNamesTheDayThatClosed()
    {
        var report = Report(0, new BillSettlement(0, 0, 0, 0, Array.Empty<BillLine>()));

        AssertThat(DayReportSheet.Title(report)).IsEqual("Day 3 closed");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void APaidDayListsTakingsBillsAndNetWithoutDebtRows()
    {
        var settlement = new BillSettlement(0, 40, 40, 0, new[] { new BillLine("Rent", 40) });

        var rows = DayReportSheet.Figures(Report(90, settlement));

        AssertThat(rows.Select(row => $"{row.Label}={row.Value}").ToArray())
            .ContainsExactly("Takings=+90", "Rent=-40", "Net for the day=+50");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ADayInDebtShowsTheDebtCarriedInAndWhatIsStillOwed()
    {
        var settlement = new BillSettlement(41, 203, 150, 53,
            new[] { new BillLine("Rent", 153), new BillLine("Power", 50) });

        var rows = DayReportSheet.Figures(Report(137, settlement));

        AssertThat(rows.Select(row => $"{row.Label}={row.Value}").ToArray()).ContainsExactly(
            "Takings=+137", "Debt from yesterday=-41", "Rent=-153", "Power=-50", "Unpaid, owed tomorrow=53",
            "Net for the day=-13");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ComplaintsListEachReasonWithItsCountOrSayNone()
    {
        var settlement = new BillSettlement(0, 0, 0, 0, Array.Empty<BillLine>());

        AssertThat(DayReportSheet.Complaints(Report(0, settlement, ("price too high", 2), ("nothing in stock", 1)))
            .ToArray()).ContainsExactly("price too high x2", "nothing in stock x1");
        AssertThat(DayReportSheet.Complaints(Report(0, settlement)).ToArray())
            .ContainsExactly(DayReportSheet.NoComplaints);
    }
}
