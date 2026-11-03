using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Services;

[TestSuite]
[RequireGodotRuntime]
public class ShopDayServiceTest
{
    private const int RentAmount = 40;

    [TestCase]
    [TestCategory("Unit")]
    public void ClockBuildsFromExportsWithoutATree()
    {
        var service = AutoFree(new ShopDayService { OpeningMinutes = 600, ClosingMinutes = 660, DayLengthSeconds = 60 });

        service.OpenShop();
        AssertThat(service.MinutesOfDay).IsEqual(600);

        service.Tick(30);
        AssertThat(service.MinutesOfDay).IsEqual(630);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CloseChargesRentOnceAndAdvancesDay()
    {
        var service = NewService(500);
        var closed = 0;
        service.Closed += _ => closed++;

        service.OpenShop();
        service.CloseShop();
        service.CloseShop();

        AssertThat(service.Money!.Balance).IsEqual(500 - RentAmount);
        AssertThat(service.Day).IsEqual(2);
        AssertThat(closed).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SecondDayChargesRentAgain()
    {
        var service = NewService(500);
        var closed = 0;
        service.Closed += _ => closed++;

        service.OpenShop();
        service.CloseShop();
        service.OpenShop();
        service.CloseShop();

        AssertThat(service.Money!.Balance).IsEqual(500 - 2 * RentAmount);
        AssertThat(service.Day).IsEqual(3);
        AssertThat(closed).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TakingsCountOnlyIncreasesWhileOpen()
    {
        var service = NewService(500);

        service.OpenShop();
        service.Money!.Add(30);
        service.Money.TrySpend(10);
        AssertThat(service.Takings).IsEqual(30);

        service.CloseShop();
        AssertThat(service.Takings).IsEqual(0);

        service.Money.Add(25);
        AssertThat(service.Takings).IsEqual(0);

        service.OpenShop();
        AssertThat(service.Takings).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ShortBalanceNeverGoesNegative()
    {
        var service = NewService(10);
        DayReport? report = null;
        service.Closed += r => report = r;

        service.OpenShop();
        service.CloseShop();

        AssertThat(service.Money!.Balance).IsEqual(0);
        AssertThat(report!.Settlement.Unpaid).IsEqual(30);
        AssertThat(service.Debt).IsEqual(30);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DebtIsChargedFirstNextDay()
    {
        var service = NewService(10);
        DayReport? report = null;
        service.Closed += r => report = r;

        service.OpenShop();
        service.CloseShop();

        service.Money!.Add(100);
        service.OpenShop();
        service.CloseShop();

        AssertThat(service.Money.Balance).IsEqual(30);
        AssertThat(service.Debt).IsEqual(0);
        AssertThat(report!.Settlement.CarriedIn).IsEqual(30);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DebtKeepsCarryingWhileUnpaid()
    {
        var service = NewService(0);

        service.OpenShop();
        service.CloseShop();
        AssertThat(service.Debt).IsEqual(RentAmount);

        service.OpenShop();
        service.CloseShop();
        AssertThat(service.Debt).IsEqual(2 * RentAmount);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TickClosesAtClosingTime()
    {
        var service = NewService(500);
        var closed = 0;
        service.Closed += _ => closed++;

        service.OpenShop();
        service.Tick(600);

        AssertThat(closed).IsEqual(1);
        AssertThat(service.IsOpen).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ClosingTimeWhileReportOpenChargesOnce()
    {
        var service = NewService(500);
        var closed = 0;
        service.Closed += _ => closed++;

        service.OpenShop();
        service.Tick(600);
        service.CloseShop();

        AssertThat(closed).IsEqual(1);
        AssertThat(service.Money!.Balance).IsEqual(500 - RentAmount);
        AssertThat(service.Day).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ManualCloseThenClockTickChargesOnce()
    {
        var service = NewService(500);
        var closed = 0;
        service.Closed += _ => closed++;

        service.OpenShop();
        service.CloseShop();
        service.Tick(600);

        AssertThat(closed).IsEqual(1);
        AssertThat(service.Money!.Balance).IsEqual(500 - RentAmount);
        AssertThat(service.Day).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ComplaintsAppearInReportAndResetNextDay()
    {
        var service = NewService(500);
        var reports = new List<DayReport>();
        service.Closed += reports.Add;

        service.OpenShop();
        service.Add("no stock");
        service.Add("no stock");
        service.CloseShop();

        AssertThat(reports[0].Complaints.Count).IsEqual(1);
        AssertThat(reports[0].Complaints[0].Reason).IsEqual("no stock");
        AssertThat(reports[0].Complaints[0].Count).IsEqual(2);

        service.OpenShop();
        service.CloseShop();

        AssertThat(reports[1].Complaints.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ClockDoesNotAdvanceWhilePaused()
    {
        var service = NewService(500);
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(service);
        try
        {
            service.OpenShop();
            tree.Paused = true;

            AssertThat(service.CanProcess()).IsFalse();
        }
        finally
        {
            tree.Paused = false;
            service.QueueFree();
        }
    }

    private ShopDayService NewService(int startingBalance) =>
        AutoFree(new ShopDayService
        {
            Money = AutoFree(new MoneyService { StartingBalance = startingBalance }),
            Bills = new DailyBillBook(new RentBillSource()),
        });
}
