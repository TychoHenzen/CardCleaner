using System;
using CardCleaner.Scripts.Features.Shop.Day;
using CardCleaner.Scripts.Features.Signage;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Day;

[TestSuite]
[RequireGodotRuntime]
public class ShopSignTest
{
    private const string OpenText = "OPEN";
    private const string ClosedText = "CLOSED";

    private FakeShopDayService _days = null!;
    private SignBoard _board = null!;
    private ShopSign _sign = null!;

    [BeforeTest]
    public void Setup()
    {
        _days = new FakeShopDayService();
        _board = new SignBoard();
        _sign = new ShopSign { Board = _board, DayService = _days };
        _sign.AddChild(_board);
        var world = new Node3D();
        world.AddChild(_sign);
        AddNode(world);
    }

    [TestCase]
    public void InteractOpensClosedShop()
    {
        _days.IsOpen = false;

        _sign.Interact();

        AssertThat(_days.OpenCalls).IsEqual(1);
        AssertThat(_days.CloseCalls).IsEqual(0);
    }

    [TestCase]
    public void InteractClosesOpenShop()
    {
        _days.IsOpen = true;

        _sign.Interact();

        AssertThat(_days.CloseCalls).IsEqual(1);
        AssertThat(_days.OpenCalls).IsEqual(0);
    }

    [TestCase]
    public void LabelFollowsState()
    {
        AssertThat(_board.Text).IsEqual(ClosedText);

        var closedColour = _board.TextColor;

        _days.IsOpen = true;
        _days.RaiseOpened();
        AssertThat(_board.Text).IsEqual(OpenText);
        // Open and closed read apart at a glance by colour as well as by word.
        AssertThat(_board.TextColor).IsNotEqual(closedColour);

        _days.IsOpen = false;
        _days.RaiseClosed();
        AssertThat(_board.Text).IsEqual(ClosedText);
        AssertThat(_board.TextColor).IsEqual(closedColour);
    }

    [TestCase]
    public void RestoredShowsClosed()
    {
        _days.IsOpen = true;
        _days.RaiseOpened();

        _days.IsOpen = false;
        _days.RaiseRestored();

        AssertThat(_board.Text).IsEqual(ClosedText);
    }

    /// <summary>Records the sign's calls and raises the service events on demand.</summary>
    private sealed class FakeShopDayService : IShopDayService
    {
        public bool IsOpen { get; set; }
        public int Day { get; set; } = 1;
        public int Debt { get; set; }
        public int Takings { get; set; }
        public int MinutesOfDay { get; set; } = 480;
        public int OpeningMinute { get; set; } = 480;
        public int ClosingMinute { get; set; } = 1080;
        public int OpenCalls { get; private set; }
        public int CloseCalls { get; private set; }

        public event Action? Opened;
        public event Action<DayReport>? Closed;
        public event Action? Restored;
        // The sign never listens for the time, so the fake has nothing to raise.
        public event Action<int, int>? TimeChanged { add { } remove { } }

        public void OpenShop() => OpenCalls++;

        public void CloseShop() => CloseCalls++;

        public void RaiseOpened() => Opened?.Invoke();

        public void RaiseClosed() =>
            Closed?.Invoke(new DayReport(Day, Takings,
                new BillSettlement(0, 0, 0, 0, Array.Empty<BillLine>()),
                Array.Empty<(string Reason, int Count)>()));

        public void RaiseRestored() => Restored?.Invoke();
    }
}
