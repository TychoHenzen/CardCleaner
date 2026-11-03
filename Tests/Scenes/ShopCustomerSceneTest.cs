using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;
using Godot;
using static CardCleaner.Tests.Scenes.ShopCustomerRig;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Customers in the shop scene: a customer walks in from the street, takes the stocked item at its market price,
/// pays at the counter and leaves. Customers that cannot reach their target or get stuck give up and free their claim.
/// Arrivals that follow the shop day are covered by <see cref="ShopCustomerDaySceneTest"/>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopCustomerSceneTest
{
    private const float TakeLeaveTolerance = 1.0f;
    private const float SpawnTolerance = 0.01f;
    private const string CardShelfScene = "res://Scenes/Shop/Items/Shelf.tscn";
    private const string ProductPricePath = "World/Storefront/ProductShelf/Price1";

    private ShopCustomerRig _rig = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _rig = await ShopCustomerRig.Create();
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CustomerBuysTheStockedItemAtMarketPrice()
    {
        var slot = _rig.StockBooster();
        var balanceBefore = _rig.Money.Balance;

        var customer = _rig.Spawner.SpawnNow();
        AssertThat(customer).IsNotNull();
        AssertBool(XzDistance(customer!.GlobalPosition, _rig.Marker("CustomerSpawn")) <= SpawnTolerance).IsTrue();

        var last = customer.GlobalPosition;
        for (var i = 0; i < FrameBudget && !Gone(customer); i++)
        {
            last = customer.GlobalPosition;
            await ISceneRunner.SyncPhysicsFrame;
        }

        AssertBool(Gone(customer)).IsTrue();
        AssertThat(_rig.Money.Balance).IsEqual(balanceBefore + 4);
        AssertBool(slot.HasCards).IsFalse();
        AssertThat(_rig.Complaints.Reasons.Count).IsEqual(0);
        AssertBool(XzDistance(last, _rig.Marker("CustomerDespawn")) <= TakeLeaveTolerance).IsTrue();
        AssertThat(_rig.Spawner.ClaimedCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CustomerRefusesAnOverpricedItem()
    {
        var slot = _rig.StockBooster();
        _rig.Shop.GetNode<SlotPricing>(ProductPricePath).SetMarkup(50);
        var balanceBefore = _rig.Money.Balance;

        var customer = _rig.Spawner.SpawnNow()!;
        await WaitUntil(() => Gone(customer));

        AssertBool(slot.HasCards).IsTrue();
        AssertThat(_rig.Money.Balance).IsEqual(balanceBefore);
        AssertThat(string.Join("|", _rig.Complaints.Reasons)).IsEqual("price too high");
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task TwoCustomersNeverTakeOneItem()
    {
        _rig.StockBooster();
        var balanceBefore = _rig.Money.Balance;

        var first = _rig.Spawner.SpawnNow()!;
        var second = _rig.Spawner.SpawnNow()!;
        await WaitUntil(() => Gone(first) && Gone(second));

        AssertThat(_rig.Money.Balance).IsEqual(balanceBefore + 4);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task UnreachableTargetMakesTheCustomerLeaveAndFreesTheClaim()
    {
        var slot = _rig.StockBooster();
        var frontBlocker = ShopNavigationRig.AddDoorBlocker(_rig.Shop, "FrontDoorBlocker", ShopNavigationRig.FrontDoorX);
        var roadBlocker = ShopNavigationRig.AddDoorBlocker(_rig.Shop, "RoadDoorBlocker", ShopNavigationRig.RoadDoorX);
        await _rig.RebakeAndSync();
        var balanceBefore = _rig.Money.Balance;

        var blocked = _rig.Spawner.SpawnNow()!;
        await WaitUntil(() => Gone(blocked));

        AssertBool(slot.HasCards).IsTrue();
        AssertThat(_rig.Money.Balance).IsEqual(balanceBefore);
        AssertThat(_rig.Complaints.Reasons.Count).IsEqual(0);
        AssertThat(_rig.Spawner.ClaimedCount).IsEqual(0);

        ShopNavigationRig.RemoveBlocker(frontBlocker);
        ShopNavigationRig.RemoveBlocker(roadBlocker);
        await _rig.RebakeAndSync();

        var next = _rig.Spawner.SpawnNow()!;
        await WaitUntil(() => Gone(next));

        AssertThat(_rig.Money.Balance).IsEqual(balanceBefore + 4);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task StuckCustomerGivesUpAndLeaves()
    {
        var slot = _rig.StockBooster();
        var balanceBefore = _rig.Money.Balance;

        var customer = _rig.Spawner.SpawnNow()!;
        customer.Speed = 0f;
        await WaitUntil(() => Gone(customer));

        AssertBool(slot.HasCards).IsTrue();
        AssertThat(_rig.Money.Balance).IsEqual(balanceBefore);
        AssertThat(_rig.Spawner.ClaimedCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CustomerThatGivesUpAfterTakingReturnsTheItem()
    {
        var slot = _rig.StockBooster();
        var balanceBefore = _rig.Money.Balance;

        var customer = _rig.Spawner.SpawnNow()!;
        await WaitUntil(() => Gone(customer) || customer.CarriedCount == 1);
        AssertBool(Gone(customer)).IsFalse();
        AssertBool(slot.HasCards).IsFalse();
        customer.GiveUp();
        await WaitUntil(() => Gone(customer));

        AssertBool(slot.HasCards).IsTrue();
        AssertThat(_rig.Money.Balance).IsEqual(balanceBefore);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CustomerMovesAwayFromTheSpawnWithinHalfASecond()
    {
        _rig.StockBooster();
        var customer = _rig.Spawner.SpawnNow()!;
        for (var i = 0; i < 30; i++)
        {
            await ISceneRunner.SyncPhysicsFrame;
        }

        AssertBool(Gone(customer)).IsFalse();
        AssertBool(XzDistance(customer.GlobalPosition, _rig.Marker("CustomerSpawn")) >= 2.0f).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PausedTreeFreezesCustomers()
    {
        _rig.StockBooster();
        var customer = _rig.Spawner.SpawnNow()!;
        await ISceneRunner.SyncPhysicsFrame;

        _rig.Shop.GetTree().Paused = true;
        try
        {
            AssertBool(customer.CanProcess()).IsFalse();
            AssertBool(_rig.Spawner.CanProcess()).IsFalse();
        }
        finally
        {
            _rig.Shop.GetTree().Paused = false;
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CustomerBuysASingleCardFromACardShelf()
    {
        var shelf = GD.Load<PackedScene>(CardShelfScene).Instantiate<CardShelf>();
        shelf.Position = new Vector3(3f, 0f, 9f);
        _rig.Shop.GetNode("World/Storefront").AddChild(shelf);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;

        var card = AddNode(ShopTestCards.Create(new CardSignature()));
        var slot = shelf.Slots.First();
        AssertBool(slot.TryLock(card)).IsTrue();
        var balanceBefore = _rig.Money.Balance;

        var customer = _rig.Spawner.SpawnNow()!;
        await WaitUntil(() => Gone(customer));

        AssertThat(_rig.Money.Balance).IsEqual(balanceBefore + CardValue.MinimumCardPrice);
        AssertBool(slot.HasCards).IsFalse();
    }
}
