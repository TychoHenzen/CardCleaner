using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
///     The shared shop scene, services and helpers of the restocker suites: a hired restocker with the shop open
///     in the real shop scene. Every wait polls for its effect and fails with a message after a wall-clock bound.
/// </summary>
[RequireGodotRuntime]
public abstract class RestockerTestBase
{
    private protected const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private protected const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private protected const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private protected const int IdleFrames = 120;
    private protected const int LongIdleFrames = 600;
    private protected const int LevelThreePayment = 1500;
    private protected const int PacksInABox = 8;
    private protected const int ProductSlots = 16;
    private protected const string ProductShelfScene = "res://Scenes/Shop/Items/ProductShelf.tscn";
    private protected const float ShelfSpacing = 3f;
    private protected const double LongWalkSeconds = 90;

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
    private protected ShopNavigationRegion _region = null!;
    private FastClock? _clock;

    [BeforeTest]
    public async Task Setup()
    {
        // The restocker walks the real shop on game time; a fast clock runs those walks in a fraction of the wall time.
        _clock = FastClock.Start();
        _shop = ShopOrderingRig.LoadShopWithServices();
        _restocker = _shop.GetNode<Restocker>("World/RightStorage/Restocker");
        _staff = _shop.GetNode<StaffService>("Services/Staff");
        _day = _shop.GetNode<ShopDayService>("Services/ShopDayService");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        _progression = _shop.GetNode<ShopProgression>("Services/ShopProgression");
        _rack = _shop.GetNode<CardShelf>("World/Furniture/StorageRack/Content");
        _product = _shop.GetNode<CardShelf>("World/Furniture/ProductShelf/Content");
        var region = _region = _shop.GetNode<ShopNavigationRegion>("World/Navigation");

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
        _clock?.Dispose();
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

    // Puts a container on a board through the board, so it goes into the board's next seat at the board's price.
    private protected CardContainer StockOn(string scenePath, ShelfLevel level)
    {
        var container = GD.Load<PackedScene>(scenePath).Instantiate<CardContainer>();
        _shop.GetNode("World").AddChild(container);
        AssertBool(level.TryLock(container)).IsTrue();
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

    // One more product shelf beside the scene's one, in line along its width and turned the same way, so sixteen
    // product slots stand and the real box threshold (8) can be met. It is checked to be reachable from the rack
    // before a test relies on it.
    private protected List<CardShelf> AddProductShelves()
    {
        var basis = _product.GlobalTransform.Basis;
        var shelf = GD.Load<PackedScene>(ProductShelfScene).Instantiate<CardShelf>();
        _product.GetParent().AddChild(shelf);
        shelf.GlobalTransform = new Transform3D(basis, _product.GlobalPosition + basis.X.Normalized() * ShelfSpacing);
        AssertBool(Reaches(_rack, shelf)).IsTrue();
        return [_product, shelf];
    }

    // Stocks a pack on the first product slots until only the given number of them is free; returns how many it
    // stocked.
    private protected int StockAllBut(List<CardShelf> shelves, int free)
    {
        var slots = shelves.SelectMany(shelf => shelf.Slots).ToList();
        var stocked = slots.Count - free;
        AssertInt(stocked).IsGreaterEqual(0);
        foreach (var slot in slots.Take(stocked))
            Stock(PackScene, slot);
        return stocked;
    }

    // Whether a path from one shelf's spot on the baked mesh reaches the other's.
    private protected bool Reaches(CardShelf from, CardShelf to)
    {
        var map = _region.GetNavigationMap();
        return _region.Reaches(
            NavigationServer3D.MapGetClosestPoint(map, from.GlobalPosition),
            NavigationServer3D.MapGetClosestPoint(map, to.GlobalPosition));
    }

    private protected static bool IsCarried(CardContainer container) =>
        container.GetParent()?.Name.ToString() == "Hand";

    // Packs that are in the world and unopened: locked on a shelf or lying loose.
    private protected int PackCount() => _tree.GetNodesInGroup("card_containers").OfType<CardContainer>()
        .Count(c => c.Kind == CardContainerKind.Pack && !c.IsOpened);

    // Waits for a condition through BoundedWait and fails the case with its message, plus the restocker state, when
    // the wall-clock bound passes (issue 408: the runner's stall watchdog killed an unbounded wait after 180 s).
    private protected async Task<bool> Poll(
        Func<bool> done, double maxSeconds = BoundedWait.DefaultMaxSeconds,
        [CallerArgumentExpression(nameof(done))] string? what = null) =>
        Passed(await BoundedWait.Until(done, what ?? "a condition", maxSeconds));

    // Waits for count physics ticks to pass, with the same wall-clock bound as Poll.
    private protected async Task Frames(int count) => Passed(await BoundedWait.PhysicsFrames(count));

    private bool Passed(string? timeout)
    {
        if (timeout is null)
        {
            return true;
        }

        AssertBool(false).OverrideFailureMessage(
            $"{timeout} Restocker phase {_restocker.Phase}, paused {_tree.Paused}.").IsTrue();
        return false;
    }
}
