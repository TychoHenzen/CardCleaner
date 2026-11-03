using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class DayReportTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void ComplaintsGroupByReasonWithCounts()
    {
        var tally = new ComplaintTally();
        tally.Add("price too high");
        tally.Add("no stock");
        tally.Add("price too high");

        var groups = tally.Groups();

        AssertThat(groups.Count).IsEqual(2);
        AssertThat(groups[0].Reason).IsEqual("price too high");
        AssertThat(groups[0].Count).IsEqual(2);
        AssertThat(groups[1].Reason).IsEqual("no stock");
        AssertThat(groups[1].Count).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void BlankComplaintIgnored()
    {
        var tally = new ComplaintTally();

        tally.Add("");
        tally.Add("   ");
        tally.Add(null!);

        AssertThat(tally.Groups().Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ClearEmptiesTally()
    {
        var tally = new ComplaintTally();
        tally.Add("no stock");

        tally.Clear();

        AssertThat(tally.Groups().Count).IsEqual(0);
    }
}
