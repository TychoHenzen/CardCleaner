using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Ui;

/// <summary>
/// The Bills app's rows, total and empty state, and when it refreshes: on the shop's day events and when it is shown.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BillsAppTest
{
    private Node _world = null!;
    private FakeBillBook _book = null!;
    private FakeDayService _days = null!;
    private BillsApp _app = null!;

    [BeforeTest]
    public void Setup()
    {
        _world = AddNode(new Node());
        _book = new FakeBillBook();
        _days = new FakeDayService();
        TestServices.Reset();
        TestServices.Container.RegisterSingleton<IDailyBillBook>(_book);
        TestServices.Container.RegisterSingleton<IShopDayService>(_days);
        _app = new BillsApp();
        TestServices.Inject(_app);
        _world.AddChild(_app);
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ListsEachCostLineWithTotal()
    {
        _book.Set(1, new BillLine("Rent", 40), new BillLine("Power", 12));

        _app.Refresh();

        AssertThat(_app.DayText).IsEqual("Day 1");
        AssertThat(_app.RowCount).IsEqual(2);
        AssertThat(_app.TotalText).IsEqual("Total: 52");
        AssertThat(_app.EmptyText).IsEqual("");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EmptyStateWhenNoLines()
    {
        _app.Refresh();

        AssertThat(_app.RowCount).IsEqual(0);
        AssertThat(_app.EmptyText).IsEqual("No bills today.");
        AssertThat(_app.TotalText).IsEqual("");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RefreshesWhenTheDayClosesAndShowsTheNewDay()
    {
        _book.Set(1, new BillLine("Rent", 40));
        _app.Refresh();
        AssertThat(_app.DayText).IsEqual("Day 1");

        _book.Set(2, new BillLine("Rent", 40), new BillLine("Power", 9));
        _days.Day = 2;
        _days.RaiseClosed(ReportFor(1));

        AssertThat(_app.DayText).IsEqual("Day 2");
        AssertThat(_app.RowCount).IsEqual(2);
        AssertThat(_app.TotalText).IsEqual("Total: 49");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RefreshesWhenShownAgain()
    {
        _book.Set(1, new BillLine("Rent", 40));
        AssertBool(_app.Visible).IsFalse();

        _app.Visible = true;
        AssertThat(_app.RowCount).IsEqual(1);

        _book.Set(1, new BillLine("Rent", 40), new BillLine("Fine", 15));
        _app.Visible = false;
        _app.Visible = true;

        AssertThat(_app.RowCount).IsEqual(2);
        AssertThat(_app.TotalText).IsEqual("Total: 55");
    }

    private static DayReport ReportFor(int day) =>
        new(day, 0, new BillSettlement(0, 0, 0, 0, Array.Empty<BillLine>()), Array.Empty<(string Reason, int Count)>());

    private sealed class FakeBillBook : IDailyBillBook
    {
        private readonly Dictionary<int, List<BillLine>> _lines = new();

        public void Set(int day, params BillLine[] lines) => _lines[day] = lines.ToList();

        public IReadOnlyList<BillLine> LinesFor(int day) =>
            _lines.TryGetValue(day, out var lines) ? lines : Array.Empty<BillLine>();

        public int TotalFor(int day) => LinesFor(day).Sum(line => line.Amount);
    }

    private sealed class FakeDayService : IShopDayService
    {
        public bool IsOpen => false;

        public int Day { get; set; } = 1;

        public int Debt => 0;

        public int Takings => 0;

        public int MinutesOfDay => 0;

        public int OpeningMinute => 0;

        public int ClosingMinute => 0;

        public event Action? Opened;

        public event Action<DayReport>? Closed;

        public event Action? Restored;

        public event Action<int, int>? TimeChanged;

        public void OpenShop() => Opened?.Invoke();

        public void CloseShop()
        {
        }

        public void RaiseClosed(DayReport report) => Closed?.Invoke(report);

        public void RaiseRestored() => Restored?.Invoke();

        public void RaiseTimeChanged(int day, int minute) => TimeChanged?.Invoke(day, minute);
    }
}
