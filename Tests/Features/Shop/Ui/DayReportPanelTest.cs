using System.Collections.Generic;
using CardCleaner.Scripts.Features.Pause.Models;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Ui;

[TestSuite]
[RequireGodotRuntime]
public class DayReportPanelTest
{
    private static DayReport SampleReport() => new(
        3,
        137,
        new BillSettlement(41, 203, 150, 53, new[] { new BillLine("Rent", 153), new BillLine("Power", 50) }),
        new[] { ("Too slow", 6) });

    [TestCase]
    [TestCategory("Unit")]
    public void ShowsReportLines()
    {
        var panel = new DayReportPanel { Cursor = new FakeCursorOwner() };
        AddNode(panel);

        panel.ShowReport(SampleReport());

        var text = panel.ReportLabel!.Text;
        AssertThat(text.Contains("Day 3")).IsTrue();
        AssertThat(text.Contains("Takings: 137")).IsTrue();
        AssertThat(text.Contains("Debt carried in: 41")).IsTrue();
        AssertThat(text.Contains("Rent: 153")).IsTrue();
        AssertThat(text.Contains("Power: 50")).IsTrue();
        AssertThat(text.Contains("Unpaid (new debt): 53")).IsTrue();
        AssertThat(text.Contains("Complaint: Too slow x6")).IsTrue();
        AssertThat(panel.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ContinueHidesPanelAndReleasesCursor()
    {
        var cursor = new FakeCursorOwner();
        var panel = new DayReportPanel { Cursor = cursor };
        AddNode(panel);

        panel.ShowReport(SampleReport());
        AssertThat(cursor.Claims.Count).IsEqual(1);
        AssertThat(ReferenceEquals(cursor.Claims[0], panel)).IsTrue();

        panel.ContinueButton!.EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(cursor.Releases.Count).IsEqual(1);
        AssertThat(ReferenceEquals(cursor.Releases[0], panel)).IsTrue();
        AssertThat(panel.Visible).IsFalse();
    }

    /// <summary>Records which owners claimed and released the cursor.</summary>
    private sealed class FakeCursorOwner : ICursorOwner
    {
        public List<object> Claims { get; } = new();
        public List<object> Releases { get; } = new();

        public void Claim(object owner) => Claims.Add(owner);

        public void Release(object owner) => Releases.Add(owner);

        public bool HasClaimOtherThan(object owner) => false;
    }
}
