using System;
using System.Collections.Generic;
using System.Linq;
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
    public void ShowsTheSheetAsATitleAFigureTableAndComplaints()
    {
        var panel = new DayReportPanel { Cursor = new FakeCursorOwner() };
        AddNode(panel);
        var report = SampleReport();

        panel.ShowReport(report);

        AssertThat(panel.TitleLabel!.Text).IsEqual(DayReportSheet.Title(report));
        var cells = panel.FiguresGrid!.GetChildren().OfType<Label>().Select(label => label.Text).ToArray();
        var expected = DayReportSheet.Figures(report).SelectMany(row => new[] { row.Label, row.Value }).ToArray();
        AssertThat(cells.SequenceEqual(expected)).IsTrue();
        var complaints = panel.ComplaintsBox!.GetChildren().OfType<Label>().Select(label => label.Text);
        AssertThat(complaints.SequenceEqual(DayReportSheet.Complaints(report))).IsTrue();
        AssertThat(panel.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ASecondReportReplacesTheRowsOfTheFirst()
    {
        var panel = new DayReportPanel { Cursor = new FakeCursorOwner() };
        AddNode(panel);
        var quiet = new DayReport(4, 0, new BillSettlement(0, 0, 0, 0, Array.Empty<BillLine>()), []);

        panel.ShowReport(SampleReport());
        panel.ShowReport(quiet);

        AssertThat(panel.FiguresGrid!.GetChildren().Count).IsEqual(DayReportSheet.Figures(quiet).Count * 2);
        AssertThat(panel.ComplaintsBox!.GetChildren().Count).IsEqual(1);
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
