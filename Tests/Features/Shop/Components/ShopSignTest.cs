using System;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

[TestSuite]
[RequireGodotRuntime]
public class ShopSignTest
{
    private const string OpenText = "Open - interact to close";
    private const string ClosedText = "Closed - interact to open";

    private FakeShopDayService _days = null!;
    private Label3D _label = null!;
    private ShopSign _sign = null!;

    [BeforeTest]
    public void Setup()
    {
        _days = new FakeShopDayService();
        _label = new Label3D();
        _sign = new ShopSign { StatusLabel = _label, DayService = _days };
        _sign.AddChild(_label);
        var world = new Node3D();
        world.AddChild(_sign);
        AddNode(world);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void InteractOpensClosedShop()
    {
        _days.IsOpen = false;

        _sign.Interact();

        AssertThat(_days.OpenCalls).IsEqual(1);
        AssertThat(_days.CloseCalls).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void InteractClosesOpenShop()
    {
        _days.IsOpen = true;

        _sign.Interact();

        AssertThat(_days.CloseCalls).IsEqual(1);
        AssertThat(_days.OpenCalls).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LabelFollowsState()
    {
        AssertThat(_label.Text).IsEqual(ClosedText);

        _days.IsOpen = true;
        _days.RaiseOpened();
        AssertThat(_label.Text).IsEqual(OpenText);

        _days.IsOpen = false;
        _days.RaiseClosed();
        AssertThat(_label.Text).IsEqual(ClosedText);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RestoredShowsClosed()
    {
        _days.IsOpen = true;
        _days.RaiseOpened();

        _days.IsOpen = false;
        _days.RaiseRestored();

        AssertThat(_label.Text).IsEqual(ClosedText);
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
        public event Action<int, int>? TimeChanged;

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
