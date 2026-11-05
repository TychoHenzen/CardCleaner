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
///     The restocker in the real shop scene: a hired restocker with the shop open carries a rack pack to the product
///     shelf, unpacks a box there once enough slots are free, and does nothing when it is not hired, the shop is
///     closed, the tree is paused or there is nothing to move. Every wait polls for its effect over a bounded number
///     of physics frames.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RestockerTest
{
    private const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const int MaxFrames = 2400;
    private const int IdleFrames = 120;
    private const int LongIdleFrames = 600;
    private const int LevelThreePayment = 1500;
    private const int PacksInABox = 8;
    private const int ProductSlots = 3;

    private readonly SceneTree _tree = (SceneTree)Engine.GetMainLoop();
    private Node3D _shop = null!;
    private Node? _previousScene;
    private Restocker _restocker = null!;
    private StaffService _staff = null!;
    private ShopDayService _day = null!;
    private MoneyService _money = null!;
    private ShopProgression _progression = null!;
    private CardShelf _rack = null!;
    private CardShelf _product = null!;

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

    [TestCase]
    [TestCategory("Unit")]
    public async Task HiredRestockerMovesARackPackToTheProductShelf()
    {
        HireAndOpen();
        var pack = Stock(PackScene, _rack.Slots.First());

        var moved = await Poll(() => _product.Slots.Any(slot => slot.Cards.Contains(pack)));

        AssertBool(moved).IsTrue();
        AssertBool(_rack.Slots.First().HasCards).IsFalse();
        AssertBool(_restocker.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task BoxIsUnpackedAtTheShelfAndAllEightPacksEndUpCounted()
    {
        HireAndOpen();
        _restocker.BoxUnpackThreshold = ProductSlots;
        var box = Stock(BoxScene, _rack.Slots.First());

        var unpacked = await Poll(() => !GodotObject.IsInstanceValid(box) && PackCount() == PacksInABox);

        AssertBool(unpacked).IsTrue();
        AssertInt(_product.StockedCount).IsEqual(ProductSlots);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void HomeReachesTheRackAndTheRackReachesTheProductShelf()
    {
        var region = _shop.GetNode<ShopNavigationRegion>("World/Navigation");
        var map = region.GetNavigationMap();
        var rack = NavigationServer3D.MapGetClosestPoint(map, _rack.GlobalPosition);
        var shelf = NavigationServer3D.MapGetClosestPoint(map, _product.GlobalPosition);

        AssertBool(region.Reaches(_restocker.GlobalPosition, rack)).IsTrue();
        AssertBool(region.Reaches(rack, shelf)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task BoxStaysOnTheRackWhenTooFewProductSlotsAreFree()
    {
        HireAndOpen();
        _restocker.BoxUnpackThreshold = ProductSlots;
        foreach (var slot in _product.Slots.Take(2))
            Stock(PackScene, slot);
        var box = Stock(BoxScene, _rack.Slots.First());

        await Frames(LongIdleFrames);

        AssertBool(GodotObject.IsInstanceValid(box) && _rack.Slots.First().Cards.Contains(box)).IsTrue();
        AssertInt(PackCount()).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task DefaultThresholdLeavesABoxOnTheRackWithOneThreeSlotShelf()
    {
        HireAndOpen();
        var box = Stock(BoxScene, _rack.Slots.First());

        await Frames(LongIdleFrames);

        AssertBool(_rack.Slots.First().Cards.Contains(box)).IsTrue();
        AssertThat(_restocker.Phase).IsEqual(RestockerPhase.Idle);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ClosedShopLeavesTheRestockerWhereItStands()
    {
        Hire();
        var pack = Stock(PackScene, _rack.Slots.First());
        await AssertStaysPut(pack);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PausedTreeLeavesTheRestockerWhereItStands()
    {
        HireAndOpen();
        var pack = Stock(PackScene, _rack.Slots.First());
        _tree.Paused = true;

        await AssertStaysPut(pack);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task UnhiredRestockerNeverMovesAndIsHidden()
    {
        _day.OpenShop();
        var pack = Stock(PackScene, _rack.Slots.First());

        AssertBool(_restocker.Visible).IsFalse();
        await AssertStaysPut(pack);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task NothingToRestockStaysIdleAtHome()
    {
        HireAndOpen();
        var home = _restocker.GlobalPosition;

        await Frames(IdleFrames);

        AssertThat(_restocker.Phase).IsEqual(RestockerPhase.Idle);
        AssertThat(_restocker.GlobalPosition).IsEqual(home);
    }

    private async Task AssertStaysPut(CardContainer pack)
    {
        var home = _restocker.GlobalPosition;

        await Frames(IdleFrames);

        AssertThat(_restocker.GlobalPosition).IsEqual(home);
        AssertBool(_rack.Slots.First().Cards.Contains(pack)).IsTrue();
        AssertThat(_restocker.Phase).IsEqual(RestockerPhase.Idle);
    }

    private void Hire()
    {
        _money.Add(StaffDefaults.RestockerHireFee);
        _progression.AwardCheckout(LevelThreePayment);
        AssertThat(_staff.Hire(StaffDefaults.RestockerId)).IsEqual(StaffResult.Hired);
    }

    private void HireAndOpen()
    {
        Hire();
        _day.OpenShop();
    }

    private CardContainer Stock(string scenePath, DeckSlot slot)
    {
        var container = GD.Load<PackedScene>(scenePath).Instantiate<CardContainer>();
        _shop.GetNode("World").AddChild(container);
        AssertBool(slot.TryLock(container)).IsTrue();
        return container;
    }

    // Packs that are in the world and unopened: locked on a shelf or lying loose.
    private int PackCount() => _tree.GetNodesInGroup("card_containers").OfType<CardContainer>()
        .Count(c => c.Kind == CardContainerKind.Pack && !c.IsOpened);

    private async Task<bool> Poll(Func<bool> done)
    {
        for (var frame = 0; frame < MaxFrames; frame++)
        {
            if (done())
                return true;
            await ISceneRunner.SyncPhysicsFrame;
        }

        return done();
    }

    private static async Task Frames(int count)
    {
        for (var frame = 0; frame < count; frame++)
            await ISceneRunner.SyncPhysicsFrame;
    }
}
