using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Customers in the shop scene: a customer walks in from the street, takes the stocked item at its market price,
/// pays at the counter and leaves. Customers that cannot reach their target or get stuck give up and free their claim.
/// Arrivals follow the shop day: opening the shop brings customers in, closing it stops new ones.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopCustomerSceneTest
{
    private const float FastSpeed = 10f;
    private const float TestStuckSeconds = 0.5f;
    private const int FrameBudget = 900;
    private const int DayTickBudget = 570;
    private const float TakeLeaveTolerance = 1.0f;
    private const float SpawnTolerance = 0.01f;
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const string CardShelfScene = "res://Scenes/Shop/Items/Shelf.tscn";
    private const string ProductSlotPath = "World/Storefront/ProductShelf/Slot1";
    private const string ProductPricePath = "World/Storefront/ProductShelf/Price1";

    private Node3D _shop = null!;
    private MoneyService _money = null!;
    private ShopDayService _day = null!;
    private CustomerSpawner _spawner = null!;
    private ShopNavigationRegion _region = null!;
    private RecordingComplaintLog _complaints = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = ShopOrderingRig.LoadShopWithServices();
        ShopScenePlayer.Sideline(_shop);
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        _day = _shop.GetNode<ShopDayService>("Services/ShopDayService");
        _spawner = _shop.GetNode<CustomerSpawner>("Services/CustomerSpawner");
        _region = _shop.GetNode<ShopNavigationRegion>("World/Navigation");

        _complaints = new RecordingComplaintLog();
        TestServices.Container.RegisterSingleton(RandomSources.Create(400UL));
        TestServices.Container.RegisterSingleton<IShopDayService>(_day);
        TestServices.Container.RegisterSingleton<IComplaintLog>(_complaints);
        TestServices.Container.RegisterSingleton<IDailyBillBook>(new DailyBillBook(new RentBillSource()));
        TestServices.Container.RegisterSingleton<ICheckoutHandoff>(new InstantCheckout(_money));
        _day.SaveCommand = new FakeSaveGame();
        _spawner.CustomerSpeed = FastSpeed;
        _spawner.CustomerStuckSeconds = TestStuckSeconds;

        // The first bake runs inside _Ready during AddNode, so read the region's iteration id before adding the scene.
        uint regionBefore = NavigationServer3D.RegionGetIterationId(_region.GetRid());
        AddNode(_shop);
        await ShopNavigationRig.WaitForBakeSync(_region, regionBefore);
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
        var slot = StockBooster();
        var balanceBefore = _money.Balance;

        var customer = _spawner.SpawnNow();
        AssertThat(customer).IsNotNull();
        AssertBool(XzDistance(customer!.GlobalPosition, Marker("CustomerSpawn")) <= SpawnTolerance).IsTrue();

        var last = customer.GlobalPosition;
        for (var i = 0; i < FrameBudget && !Gone(customer); i++)
        {
            last = customer.GlobalPosition;
            await ISceneRunner.SyncPhysicsFrame;
        }

        AssertBool(Gone(customer)).IsTrue();
        AssertThat(_money.Balance).IsEqual(balanceBefore + 4);
        AssertBool(slot.HasCards).IsFalse();
        AssertThat(_complaints.Reasons.Count).IsEqual(0);
        AssertBool(XzDistance(last, Marker("CustomerDespawn")) <= TakeLeaveTolerance).IsTrue();
        AssertThat(_spawner.ClaimedCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CustomerRefusesAnOverpricedItem()
    {
        var slot = StockBooster();
        _shop.GetNode<SlotPricing>(ProductPricePath).SetMarkup(50);
        var balanceBefore = _money.Balance;

        var customer = _spawner.SpawnNow()!;
        await WaitUntil(() => Gone(customer));

        AssertBool(slot.HasCards).IsTrue();
        AssertThat(_money.Balance).IsEqual(balanceBefore);
        AssertThat(string.Join("|", _complaints.Reasons)).IsEqual("price too high");
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task TwoCustomersNeverTakeOneItem()
    {
        StockBooster();
        var balanceBefore = _money.Balance;

        var first = _spawner.SpawnNow()!;
        var second = _spawner.SpawnNow()!;
        await WaitUntil(() => Gone(first) && Gone(second));

        AssertThat(_money.Balance).IsEqual(balanceBefore + 4);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task UnreachableTargetMakesTheCustomerLeaveAndFreesTheClaim()
    {
        var slot = StockBooster();
        var frontBlocker = ShopNavigationRig.AddDoorBlocker(_shop, "FrontDoorBlocker", ShopNavigationRig.FrontDoorX);
        var roadBlocker = ShopNavigationRig.AddDoorBlocker(_shop, "RoadDoorBlocker", ShopNavigationRig.RoadDoorX);
        await RebakeAndSync();
        var balanceBefore = _money.Balance;

        var blocked = _spawner.SpawnNow()!;
        await WaitUntil(() => Gone(blocked));

        AssertBool(slot.HasCards).IsTrue();
        AssertThat(_money.Balance).IsEqual(balanceBefore);
        AssertThat(_complaints.Reasons.Count).IsEqual(0);
        AssertThat(_spawner.ClaimedCount).IsEqual(0);

        ShopNavigationRig.RemoveBlocker(frontBlocker);
        ShopNavigationRig.RemoveBlocker(roadBlocker);
        await RebakeAndSync();

        var next = _spawner.SpawnNow()!;
        await WaitUntil(() => Gone(next));

        AssertThat(_money.Balance).IsEqual(balanceBefore + 4);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task StuckCustomerGivesUpAndLeaves()
    {
        var slot = StockBooster();
        var balanceBefore = _money.Balance;

        var customer = _spawner.SpawnNow()!;
        customer.Speed = 0f;
        await WaitUntil(() => Gone(customer));

        AssertBool(slot.HasCards).IsTrue();
        AssertThat(_money.Balance).IsEqual(balanceBefore);
        AssertThat(_spawner.ClaimedCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CustomerThatGivesUpAfterTakingReturnsTheItem()
    {
        var slot = StockBooster();
        var balanceBefore = _money.Balance;

        var customer = _spawner.SpawnNow()!;
        await WaitUntil(() => Gone(customer) || customer.CarriedCount == 1);
        AssertBool(Gone(customer)).IsFalse();
        AssertBool(slot.HasCards).IsFalse();
        customer.GiveUp();
        await WaitUntil(() => Gone(customer));

        AssertBool(slot.HasCards).IsTrue();
        AssertThat(_money.Balance).IsEqual(balanceBefore);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CustomerMovesAwayFromTheSpawnWithinHalfASecond()
    {
        StockBooster();
        var customer = _spawner.SpawnNow()!;
        for (var i = 0; i < 30; i++)
        {
            await ISceneRunner.SyncPhysicsFrame;
        }

        AssertBool(Gone(customer)).IsFalse();
        AssertBool(XzDistance(customer.GlobalPosition, Marker("CustomerSpawn")) >= 2.0f).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ClosingStopsArrivalsButACustomerInsideFinishes()
    {
        StockBooster();
        _spawner.CustomersPerDay = 6;
        _spawner.Rng = new Pcg32Random(400UL);
        _day.OpenShop();
        for (var i = 0; i < DayTickBudget && _spawner.SpawnedCount == 0; i++)
        {
            _day.Tick(1.0);
        }

        AssertBool(_spawner.SpawnedCount >= 1).IsTrue();
        AssertThat(_spawner.SpawnedCount + _spawner.PendingArrivals).IsEqual(6);
        var inside = _shop.GetNode("World").GetChildren().OfType<Customer>().ToList();

        _day.CloseShop();
        AssertThat(_spawner.PendingArrivals).IsEqual(0);
        var balanceAtClose = _money.Balance;

        await WaitUntil(() => inside.All(Gone));

        AssertThat(_money.Balance).IsEqual(balanceAtClose + 4);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PausedTreeFreezesCustomers()
    {
        StockBooster();
        var customer = _spawner.SpawnNow()!;
        await ISceneRunner.SyncPhysicsFrame;

        _shop.GetTree().Paused = true;
        try
        {
            AssertBool(customer.CanProcess()).IsFalse();
            AssertBool(_spawner.CanProcess()).IsFalse();
        }
        finally
        {
            _shop.GetTree().Paused = false;
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task OpeningAtTheSignBringsACustomerWhoBuys()
    {
        StockBooster();
        _spawner.CustomersPerDay = 1;
        _spawner.Rng = new Pcg32Random(400UL);
        var sign = _shop.GetNode<ShopSign>("World/Storefront/ShopSign");
        var balanceBefore = _money.Balance;

        sign.Interact();
        AssertBool(_day.IsOpen).IsTrue();
        AssertThat(_spawner.PendingArrivals).IsEqual(1);

        for (var i = 0; i < DayTickBudget && _spawner.SpawnedCount == 0; i++)
        {
            _day.Tick(1.0);
        }

        AssertThat(_spawner.SpawnedCount).IsEqual(1);
        var customer = _shop.GetNode("World").GetChildren().OfType<Customer>().Single();
        await WaitUntil(() => Gone(customer));

        AssertThat(_money.Balance).IsEqual(balanceBefore + 4);
        AssertThat(_day.Takings).IsEqual(4);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ZeroCustomersPerDaySpawnsNobody()
    {
        _spawner.CustomersPerDay = 0;
        var sign = _shop.GetNode<ShopSign>("World/Storefront/ShopSign");

        sign.Interact();
        AssertThat(_spawner.PendingArrivals).IsEqual(0);

        for (var i = 0; i < DayTickBudget; i++)
        {
            _day.Tick(1.0);
        }

        AssertThat(_spawner.SpawnedCount).IsEqual(0);
        AssertBool(_shop.GetNode("World").GetChildren().OfType<Customer>().Any()).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CustomerBuysASingleCardFromACardShelf()
    {
        var shelf = GD.Load<PackedScene>(CardShelfScene).Instantiate<CardShelf>();
        shelf.Position = new Vector3(3f, 0f, 9f);
        _shop.GetNode("World/Storefront").AddChild(shelf);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;

        var card = AddNode(ShopTestCards.Create(new CardSignature()));
        var slot = shelf.Slots.First();
        AssertBool(slot.TryLock(card)).IsTrue();
        var balanceBefore = _money.Balance;

        var customer = _spawner.SpawnNow()!;
        await WaitUntil(() => Gone(customer));

        AssertThat(_money.Balance).IsEqual(balanceBefore + CardValue.MinimumCardPrice);
        AssertBool(slot.HasCards).IsFalse();
    }

    private DeckSlot StockBooster()
    {
        var slot = _shop.GetNode<DeckSlot>(ProductSlotPath);
        var booster = AddNode(GD.Load<PackedScene>(BoosterScene).Instantiate<CardContainer>());
        AssertBool(slot.TryLock(booster)).IsTrue();
        return slot;
    }

    private static async Task WaitUntil(Func<bool> done)
    {
        for (var i = 0; i < FrameBudget && !done(); i++)
        {
            await ISceneRunner.SyncPhysicsFrame;
        }
    }

    private async Task RebakeAndSync()
    {
        uint regionBefore = NavigationServer3D.RegionGetIterationId(_region.GetRid());
        _region.Rebake();
        await ShopNavigationRig.WaitForBakeSync(_region, regionBefore);
    }

    private Vector3 Marker(string name) => _shop.GetNode<Marker3D>($"World/Markers/{name}").GlobalPosition;

    private static bool Gone(Customer customer) => !GodotObject.IsInstanceValid(customer);

    private static float XzDistance(Vector3 a, Vector3 b) => new Vector2(a.X, a.Z).DistanceTo(new Vector2(b.X, b.Z));

    // Records the complaints the day raises, so the scene test can read them back.
    private sealed class RecordingComplaintLog : IComplaintLog
    {
        public List<string> Reasons { get; } = new();

        public void Add(string reason)
        {
            Reasons.Add(reason);
        }
    }

    // Stands in for the slot writer so the day close writes no file during the scene test.
    private sealed class FakeSaveGame : ISaveGameCommand
    {
        public bool ManualSave() => true;
    }
}
