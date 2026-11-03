using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Shared harness for the shop customer suites: loads the shop scene with its services, registers the stand-ins a
/// customer visit needs (seeded random source, instant checkout, recording complaint log, fake save) and waits for
/// the navigation bake, so each suite only states its scenario.
/// </summary>
public sealed class ShopCustomerRig
{
    public const int FrameBudget = 900;
    public const int DayTickBudget = 570;

    private const float FastSpeed = 10f;
    private const float TestStuckSeconds = 0.5f;
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const string ProductSlotPath = "World/Storefront/ProductShelf/Slot1";

    private ShopCustomerRig(Node3D shop)
    {
        Shop = shop;
        Money = shop.GetNode<MoneyService>("Services/MoneyService");
        Day = shop.GetNode<ShopDayService>("Services/ShopDayService");
        Spawner = shop.GetNode<CustomerSpawner>("Services/CustomerSpawner");
        Region = shop.GetNode<ShopNavigationRegion>("World/Navigation");
    }

    public Node3D Shop { get; }
    public MoneyService Money { get; }
    public ShopDayService Day { get; }
    public CustomerSpawner Spawner { get; }
    public ShopNavigationRegion Region { get; }
    public RecordingComplaintLog Complaints { get; } = new();

    public static async Task<ShopCustomerRig> Create()
    {
        var shop = ShopOrderingRig.LoadShopWithServices();
        ShopScenePlayer.Sideline(shop);
        var rig = new ShopCustomerRig(shop);

        TestServices.Container.RegisterSingleton(RandomSources.Create(400UL));
        TestServices.Container.RegisterSingleton<IShopDayService>(rig.Day);
        TestServices.Container.RegisterSingleton<IComplaintLog>(rig.Complaints);
        TestServices.Container.RegisterSingleton<IDailyBillBook>(new DailyBillBook(new RentBillSource()));
        TestServices.Container.RegisterSingleton<ICheckoutHandoff>(new InstantCheckout(rig.Money));
        rig.Day.SaveCommand = new FakeSaveGame();
        rig.Spawner.CustomerSpeed = FastSpeed;
        rig.Spawner.CustomerStuckSeconds = TestStuckSeconds;

        // The first bake runs inside _Ready during AddNode, so read the region's iteration id before adding the scene.
        uint regionBefore = NavigationServer3D.RegionGetIterationId(rig.Region.GetRid());
        AddNode(shop);
        await ShopNavigationRig.WaitForBakeSync(rig.Region, regionBefore);
        return rig;
    }

    public static async Task WaitUntil(Func<bool> done)
    {
        for (var i = 0; i < FrameBudget && !done(); i++)
        {
            await ISceneRunner.SyncPhysicsFrame;
        }
    }

    public static bool Gone(Customer customer) => !GodotObject.IsInstanceValid(customer);

    public static float XzDistance(Vector3 a, Vector3 b) => new Vector2(a.X, a.Z).DistanceTo(new Vector2(b.X, b.Z));

    public DeckSlot StockBooster()
    {
        var slot = Shop.GetNode<DeckSlot>(ProductSlotPath);
        var booster = AddNode(GD.Load<PackedScene>(BoosterScene).Instantiate<CardContainer>());
        AssertBool(slot.TryLock(booster)).IsTrue();
        return slot;
    }

    public async Task RebakeAndSync()
    {
        uint regionBefore = NavigationServer3D.RegionGetIterationId(Region.GetRid());
        Region.Rebake();
        await ShopNavigationRig.WaitForBakeSync(Region, regionBefore);
    }

    public Vector3 Marker(string name) => Shop.GetNode<Marker3D>($"World/Markers/{name}").GlobalPosition;

    // Records the complaints the day raises, so the scene test can read them back.
    public sealed class RecordingComplaintLog : IComplaintLog
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
