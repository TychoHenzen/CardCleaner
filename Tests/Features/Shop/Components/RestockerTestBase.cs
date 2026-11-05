using System;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Scenes;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
///     The shared shop scene, services and helpers of the restocker suites: a hired restocker with the shop open
///     in the real shop scene. Every wait polls for its effect over a bounded number of physics frames.
/// </summary>
[RequireGodotRuntime]
public abstract class RestockerTestBase
{
    private protected const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private protected const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private protected const int MaxFrames = 2400;
    private protected const int IdleFrames = 120;
    private protected const int LongIdleFrames = 600;
    private protected const int LevelThreePayment = 1500;
    private protected const int PacksInABox = 8;
    private protected const int ProductSlots = 3;

    private protected readonly SceneTree _tree = (SceneTree)Engine.GetMainLoop();
    private protected Node3D _shop = null!;
    private protected Node? _previousScene;
    private protected Restocker _restocker = null!;
    private protected StaffService _staff = null!;
    private protected ShopDayService _day = null!;
    private protected MoneyService _money = null!;
    private protected ShopProgression _progression = null!;
    private protected CardShelf _rack = null!;
    private protected CardShelf _product = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = ShopOrderingRig.LoadShopWithServices();
        _restocker = _shop.GetNode<Restocker>("World/RightStorage/Restocker");
        _staff = _shop.GetNode<StaffService>("Services/Staff");
        _day = _shop.GetNode<ShopDayService>("Services/ShopDayService");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        _progression = _shop.GetNode<ShopProgression>("Services/ShopProgression");
        _rack = _shop.GetNode<CardShelf>("World/RightStorage/StorageRack");
        _product = _shop.GetNode<CardShelf>("World/Storefront/ProductShelf");
        var region = _shop.GetNode<ShopNavigationRegion>("World/Navigation");

        var bills = new DailyBillBook(new IDailyBillSource[] { new RentBillSource(), new StaffWageBillSource(_staff) });
        TestServices.Container.RegisterSingleton<IShopDayService>(_day);
        TestServices.Container.RegisterSingleton<IDailyBillBook>(bills);
        var spawner = _shop.GetNode<CustomerSpawner>("Services/CustomerSpawner");
        spawner.Rng = RandomSources.Create(408UL);
        spawner.ProcessMode = Node.ProcessModeEnum.Disabled;
        _day.SaveCommand = new NoOpSaveGame();
        _day.Money = _money;
        _day.Bills = bills;
        TestServices.Inject(_day);
        TestServices.Inject(_restocker);

        uint before = NavigationServer3D.RegionGetIterationId(region.GetRid());
        _previousScene = _tree.CurrentScene;
        _tree.Root.AddChild(_shop);
        _tree.CurrentScene = _shop;
        await ShopNavigationRig.WaitForBakeSync(region, before);
    }

    [AfterTest]
    public void Teardown()
    {
        _tree.CurrentScene = _previousScene;
        _shop.Free();
        ShopPauseRig.Teardown();
    }

    private protected async Task AssertStaysPut(CardContainer pack)
    {
        var home = _restocker.GlobalPosition;

        await Frames(IdleFrames);

        AssertThat(_restocker.GlobalPosition).IsEqual(home);
        AssertBool(_rack.Slots.First().Cards.Contains(pack)).IsTrue();
        AssertThat(_restocker.Phase).IsEqual(RestockerPhase.Idle);
    }

    private protected void Hire()
    {
        _money.Add(StaffDefaults.RestockerHireFee);
        _progression.AwardCheckout(LevelThreePayment);
        AssertThat(_staff.Hire(StaffDefaults.RestockerId)).IsEqual(StaffResult.Hired);
    }

    private protected void HireAndOpen()
    {
        Hire();
        _day.OpenShop();
    }

    private protected CardContainer Stock(string scenePath, DeckSlot slot)
    {
        var container = GD.Load<PackedScene>(scenePath).Instantiate<CardContainer>();
        _shop.GetNode("World").AddChild(container);
        AssertBool(slot.TryLock(container)).IsTrue();
        return container;
    }

    // Two packs on the rack; returns them nearest-to-the-restocker first once it is walking to the first.
    private protected async Task<(CardContainer Near, CardContainer Far)> TwoRackPacksWhileWalkingToTheNearOne()
    {
        var slots = _rack.Slots.Take(2).ToList();
        var packs = slots.Select(slot => Stock(PackScene, slot)).ToList();
        await Poll(() => _restocker.Phase == RestockerPhase.ToSource);
        var ordered = packs.OrderBy(p => p.GlobalPosition.DistanceTo(_restocker.GlobalPosition)).ToList();
        return (ordered[0], ordered[1]);
    }

    private protected static bool IsCarried(CardContainer container) =>
        container.GetParent()?.Name.ToString() == "Hand";

    // Packs that are in the world and unopened: locked on a shelf or lying loose.
    private protected int PackCount() => _tree.GetNodesInGroup("card_containers").OfType<CardContainer>()
        .Count(c => c.Kind == CardContainerKind.Pack && !c.IsOpened);

    private protected async Task<bool> Poll(Func<bool> done)
    {
        for (var frame = 0; frame < MaxFrames; frame++)
        {
            if (done())
                return true;
            await ISceneRunner.SyncPhysicsFrame;
        }

        return done();
    }

    private protected static async Task Frames(int count)
    {
        for (var frame = 0; frame < count; frame++)
            await ISceneRunner.SyncPhysicsFrame;
    }
}
