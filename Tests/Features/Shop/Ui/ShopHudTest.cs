using System;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Ui;

[TestSuite]
[RequireGodotRuntime]
public class ShopHudTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void ShowsMoneyDayAndTime()
    {
        var days = new FakeShopDayService { Day = 4, MinutesOfDay = 480 };
        var money = AutoFree(new MoneyService { StartingBalance = 500 });
        var hud = new ShopHud { DayService = days, Money = money };
        AddNode(hud);

        AssertThat(hud.TimeLabel!.Text).IsEqual("08:00");
        AssertThat(hud.DayLabel!.Text).IsEqual("Day 4");
        AssertThat(hud.MoneyLabel!.Text).IsEqual("Money: 500");

        money.Add(25);

        AssertThat(hud.MoneyLabel!.Text).IsEqual("Money: 525");

        days.MinutesOfDay = 495;
        days.RaiseTimeChanged(4, 495);

        AssertThat(hud.TimeLabel!.Text).IsEqual("08:15");
    }

    /// <summary>A day service that reports fixed figures and raises no events.</summary>
    private sealed class FakeShopDayService : IShopDayService
    {
        public bool IsOpen { get; set; }
        public int Day { get; set; } = 1;
        public int Debt { get; set; }
        public int Takings { get; set; }
        public int MinutesOfDay { get; set; } = 480;
        public int OpeningMinute { get; set; } = 480;
        public int ClosingMinute { get; set; } = 1080;

        public event Action? Opened;
        public event Action<DayReport>? Closed;
        public event Action? Restored;
        public event Action<int, int>? TimeChanged;

        public void OpenShop() => Opened?.Invoke();

        public void CloseShop() => Closed?.Invoke(new DayReport(Day, Takings,
            new BillSettlement(0, 0, 0, 0, Array.Empty<BillLine>()),
            Array.Empty<(string Reason, int Count)>()));

        public void RaiseRestored() => Restored?.Invoke();

        public void RaiseTimeChanged(int day, int minutesOfDay) => TimeChanged?.Invoke(day, minutesOfDay);
    }
}
