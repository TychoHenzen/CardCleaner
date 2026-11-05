using System;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.TestUtilities;
using Godot;
using Saveable;

namespace CardCleaner.Tests.Features.Shop.Services;

[TestSuite]
[RequireGodotRuntime]
public class ShopDayServiceSaveTest
{
    private const int RentAmount = 40;

    [TestCase]
    [TestCategory("Unit")]
    public void SaveRunsAfterTheDayAdvances()
    {
        var service = NewService(10);
        var seenDay = 0;
        var seenDebt = -1;
        var seenBalance = -1;
        var save = new FakeSaveGame(() =>
        {
            seenDay = service.Day;
            seenDebt = service.Debt;
            seenBalance = service.Money!.Balance;
            return true;
        });
        service.SaveCommand = save;

        service.OpenShop();
        service.CloseShop();

        AssertThat(save.Calls).IsEqual(1);
        AssertThat(seenDay).IsEqual(2);
        AssertThat(seenDebt).IsEqual(30);
        AssertThat(seenBalance).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SaveFailureStillRaisesClosed()
    {
        var service = NewService(500);
        var closed = 0;
        service.Closed += _ => closed++;
        var save = new FakeSaveGame(() => false);
        service.SaveCommand = save;

        service.OpenShop();
        service.CloseShop();

        AssertThat(save.Calls).IsEqual(1);
        AssertThat(closed).IsEqual(1);
        AssertThat(service.Day).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NoSaveCommandStillCloses()
    {
        var service = NewService(500);
        var closed = 0;
        service.Closed += _ => closed++;

        service.OpenShop();
        service.CloseShop();

        AssertThat(closed).IsEqual(1);
        AssertThat(service.Day).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SaveLoadKeepsDay()
    {
        var first = NewService(500);
        first.OpenShop();
        first.CloseShop();
        first.OpenShop();
        first.CloseShop();
        var save = new NodeSave();
        first.Save(save);

        var second = NewService(500);
        second.Load(save);

        AssertThat(second.Day).IsEqual(3);
        AssertBool(second.IsOpen).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SaveLoadKeepsDebt()
    {
        var first = NewService(10);
        first.OpenShop();
        first.CloseShop();
        var save = new NodeSave();
        first.Save(save);

        var second = NewService(10);
        second.Load(save);

        AssertThat(second.Debt).IsEqual(30);
        AssertThat(second.Day).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TreeJsonRoundTrip()
    {
        var firstRoot = AutoFree(new Node());
        var first = NewService(10);
        firstRoot.AddChild(first);
        first.OpenShop();
        first.CloseShop();
        first.OpenShop();
        first.CloseShop();
        var json = SaveSystem.SerializeTree(firstRoot);

        var secondRoot = AutoFree(new Node());
        var second = NewService(10);
        secondRoot.AddChild(second);
        SaveSystem.LoadTreeFromJson(json, secondRoot);

        AssertThat(second.Day).IsEqual(first.Day);
        AssertThat(second.Debt).IsEqual(first.Debt);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MidDaySaveRestoresClosedAtOpening()
    {
        var first = NewService(500);
        first.OpenShop();
        first.Tick(100);
        var save = new NodeSave();
        first.Save(save);

        var second = NewService(500);
        var closed = 0;
        var restored = 0;
        second.Closed += _ => closed++;
        second.Restored += () => restored++;
        second.Load(save);

        AssertBool(second.IsOpen).IsFalse();
        AssertThat(second.MinutesOfDay).IsEqual((int)second.OpeningMinutes);
        AssertThat(closed).IsEqual(0);
        AssertThat(restored).IsEqual(1);
        AssertThat(second.Money!.Balance).IsEqual(500);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MissingKeysLoadDayOne()
    {
        var service = NewService(500);

        service.Load(new NodeSave());

        AssertThat(service.Day).IsEqual(1);
        AssertThat(service.Debt).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MalformedStateLoadsDayOne()
    {
        var service = NewService(500);
        var save = new NodeSave();
        save.SetOrAddProperty("day", "two");
        save.SetOrAddProperty("debt", "many");

        service.Load(save);

        AssertThat(service.Day).IsEqual(1);
        AssertThat(service.Debt).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OutOfRangeLoadsDayOne()
    {
        var service = NewService(500);
        var save = new NodeSave();
        save.SetOrAddProperty("day", long.MaxValue);
        save.SetOrAddProperty("debt", long.MaxValue);

        service.Load(save);

        AssertThat(service.Day).IsEqual(1);
        AssertThat(service.Debt).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NegativeDayClampsToOne()
    {
        var service = NewService(500);
        var save = new NodeSave();
        save.SetOrAddProperty("day", -5L);
        save.SetOrAddProperty("debt", 0L);

        service.Load(save);

        AssertThat(service.Day).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NegativeDebtClampsToZero()
    {
        var service = NewService(500);
        var save = new NodeSave();
        save.SetOrAddProperty("day", 4L);
        save.SetOrAddProperty("debt", -3L);

        service.Load(save);

        AssertThat(service.Day).IsEqual(4);
        AssertThat(service.Debt).IsEqual(0);
    }

    private static ShopDayService NewService(int startingBalance) =>
        AutoFree(new ShopDayService
        {
            Money = AutoFree(new MoneyService { StartingBalance = startingBalance }),
            Bills = new DailyBillBook(new IDailyBillSource[] { new RentBillSource() }),
        });

    // Records each day-close save and runs the given check inside the save, as the slot writer would.
    private sealed class FakeSaveGame(Func<bool> onSave) : ISaveGameCommand
    {
        public int Calls { get; private set; }

        public bool ManualSave()
        {
            Calls++;
            return onSave();
        }
    }
}
