using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Services;

[TestSuite]
[RequireGodotRuntime]
public class ShopExpansionServiceTest
{
    private const int StartingBalance = 1000;
    private const string ShippedLotsPath = "res://Data/Shop/ExpansionLots.tres";

    private Node3D _world = null!;
    private MoneyService _money = null!;
    private ShopExpansionService _expansion = null!;

    [BeforeTest]
    public void Setup()
    {
        _world = new Node3D();
        AddNode(_world);

        _money = new MoneyService { StartingBalance = StartingBalance };
        _expansion = new ShopExpansionService
        {
            Catalog = new ExpansionLotCatalog
            {
                Lots =
                [
                    new ExpansionLotData
                    {
                        Id = "lot_a", Order = 1, Price = 100, UnlockId = "unlock_a", DailyRent = 40,
                        ExtraCustomers = 6, DisplayName = "Lot A", StorefrontMinX = -8f,
                    },
                    new ExpansionLotData
                    {
                        Id = "lot_b", Order = 2, Price = 200, UnlockId = "unlock_b", DailyRent = 60,
                        ExtraCustomers = 9, DisplayName = "Lot B", StorefrontMinX = -12f,
                    },
                ],
            },
            Money = _money,
            Progression = new OpenProgression(),
        };
        _world.AddChild(_money);
        _world.AddChild(_expansion);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TryBuyChargesThePriceOnceAndRaisesOpenedOnce()
    {
        var opened = new List<string>();
        _expansion.Opened += opened.Add;

        AssertThat(_expansion.TryBuy("lot_a")).IsEqual(PurchaseOutcome.Bought);
        AssertThat(_money.Balance).IsEqual(StartingBalance - 100);
        AssertThat(opened).IsEqual(new List<string> { "lot_a" });

        AssertThat(_expansion.TryBuy("lot_a")).IsEqual(PurchaseOutcome.AlreadyOwned);
        AssertThat(_money.Balance).IsEqual(StartingBalance - 100);
        AssertThat(opened.Count).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RefusedPurchasesChargeNothingAndRaiseNothing()
    {
        var opened = new List<string>();
        _expansion.Opened += opened.Add;

        _expansion.Progression = new OpenProgression("unlock_a");
        AssertThat(_expansion.TryBuy("lot_a")).IsEqual(PurchaseOutcome.Locked);

        _expansion.Progression = new OpenProgression();
        AssertThat(_expansion.TryBuy("lot_b")).IsEqual(PurchaseOutcome.OutOfOrder);
        AssertThat(_expansion.TryBuy("nope")).IsEqual(PurchaseOutcome.Unknown);

        AssertThat(_money.Balance).IsEqual(StartingBalance);
        AssertThat(opened.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void UnaffordableLotChargesNothing()
    {
        var opened = new List<string>();
        _expansion.Opened += opened.Add;
        AssertBool(_money.TrySpend(StartingBalance - 99)).IsTrue();

        AssertThat(_expansion.TryBuy("lot_a")).IsEqual(PurchaseOutcome.Unaffordable);

        AssertThat(_money.Balance).IsEqual(99);
        AssertThat(opened.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WithoutAProgressionServiceEveryPurchaseIsLocked()
    {
        _expansion.Progression = null;

        AssertThat(_expansion.TryBuy("lot_a")).IsEqual(PurchaseOutcome.Locked);

        AssertThat(_money.Balance).IsEqual(StartingBalance);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RentSourceHasNoLinesWhenNothingIsOwned()
    {
        var source = new ExpansionRentBillSource(_expansion);

        AssertThat(source.GetBillLines(1).Count()).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RentSourceHasOneNamedLinePerOwnedLot()
    {
        var source = new ExpansionRentBillSource(_expansion);
        _expansion.TryBuy("lot_a");

        var lines = source.GetBillLines(1).ToList();

        AssertThat(lines.Count).IsEqual(1);
        AssertThat(lines[0].Name).IsEqual("Rent: Lot A");
        AssertThat(lines[0].Amount).IsEqual(40);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ExtraCustomersPerDayIsTheOwnedSum()
    {
        _expansion.TryBuy("lot_a");
        _expansion.TryBuy("lot_b");

        AssertThat(_expansion.ExtraCustomersPerDay).IsEqual(15);
        AssertThat(_expansion.DailyRent).IsEqual(100);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ShippedLotsFileHoldsTheSideWingLot()
    {
        var catalog = GD.Load<ExpansionLotCatalog>(ShippedLotsPath);

        AssertThat(catalog).IsNotNull();
        AssertThat(catalog!.Lots.Length).IsEqual(1);
        AssertThat(catalog.Lots[0].Id).IsEqual("side_wing_1");
        AssertThat(catalog.Lots[0].Price).IsEqual(1500);
    }

}
