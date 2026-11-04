using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Tests.TestUtilities;
using Godot;
using static CardCleaner.Tests.Scenes.ShopFurnitureRig;

namespace CardCleaner.Tests.Scenes;

/// <summary>Furniture moves in the real shop scene: pick-up, confirm, cancel, stock refusal and the route.</summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopFurnitureMoveSceneTest
{
    private const string RegionPath = "World/Navigation";
    private const string ControllerPath = "Services/FurniturePlacement";
    private const string FurnitureRootPath = "World/Furniture";
    private const string BoxScenePath = "res://Scenes/Shop/Items/ShelfBox.tscn";

    private Node3D _shop = null!;
    private ShopNavigationRegion _region = null!;
    private FurniturePlacementController _controller = null!;
    private Node3D _furnitureRoot = null!;
    private Window _root = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = ShopOrderingRig.LoadShopWithServices();
        _region = _shop.GetNode<ShopNavigationRegion>(RegionPath);
        _controller = _shop.GetNode<FurniturePlacementController>(ControllerPath);
        // Inject before the shop enters the tree, so the controller's _Ready finds its resolver.
        TestServices.Inject(_controller);
        // The camera aim is not driven headless; the cases place the pose with AimAt.
        _controller.AimOverride = true;

        // The first bake runs inside _Ready during AddNode, so read the iteration id before adding the scene.
        uint regionBefore = NavigationServer3D.RegionGetIterationId(_region.GetRid());
        AddNode(_shop);
        _furnitureRoot = _shop.GetNode<Node3D>(FurnitureRootPath);
        _root = _shop.GetTree().Root;
        await ShopNavigationRig.WaitForBakeSync(_region, regionBefore);
    }

    [AfterTest]
    public void Teardown()
    {
        // A move in progress holds its piece outside the tree; cancelling puts it back so the shop frees it.
        _controller.Cancel();
        TestServices.Reset();
        _root.RemoveChild(_shop);
        _shop.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PickUpThenConfirmMovesTheSameShelf()
    {
        var placed = await PlaceShelf(8f, 6f);
        int count = _region.RebakeCount;

        AssertBool(_controller.BeginMove(placed)).IsTrue();
        AssertInt(_furnitureRoot.GetChildCount()).IsEqual(0);
        AssertBool(_controller.IsActive).IsTrue();
        AssertInt(_region.RebakeCount).IsEqual(count + 1);

        _controller.AimAt(4f, 6f);
        AssertBool(_controller.Confirm()).IsTrue();
        AssertBool(ReferenceEquals(_furnitureRoot.GetChild(0), placed)).IsTrue();
        AssertBool(placed.Position.IsEqualApprox(new Vector3(4f, 0f, 6f))).IsTrue();
        AssertInt(_region.RebakeCount).IsEqual(count + 2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CancelOfAMoveRestoresThePose()
    {
        _controller.Begin(NewBox());
        _controller.AimAt(8f, 6f);
        _controller.Rotate();
        AssertBool(_controller.Confirm()).IsTrue();
        await ISceneRunner.SyncPhysicsFrame;
        var placed = (PlacedFurniture)_furnitureRoot.GetChild(0);
        int before = _region.RebakeCount;

        AssertBool(_controller.BeginMove(placed)).IsTrue();
        _controller.AimAt(4f, 6f);
        _controller.Cancel();

        AssertBool(ReferenceEquals(_furnitureRoot.GetChild(0), placed)).IsTrue();
        AssertBool(placed.Position.IsEqualApprox(new Vector3(8f, 0f, 6f))).IsTrue();
        AssertInt(placed.QuarterTurns).IsEqual(1);
        AssertInt(_region.RebakeCount).IsEqual(before + 2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PickUpIsRefusedWhileTheShelfHoldsStock()
    {
        var placed = await PlaceShelf(8f, 6f);
        placed.HoldsStockOverride = () => true;
        int count = _region.RebakeCount;

        AssertBool(_controller.BeginMove(placed)).IsFalse();
        AssertBool(ReferenceEquals(_furnitureRoot.GetChild(0), placed)).IsTrue();
        AssertBool(_controller.IsActive).IsFalse();
        AssertInt(_region.RebakeCount).IsEqual(count);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task NavigationGoesAroundThePlacedShelf()
    {
        await PlaceShelf(8f, 6f);

        AssertBool(_region.Reaches(RouteA, RouteB)).IsTrue();
        var route = NavigationServer3D.MapGetPath(_region.GetNavigationMap(), RouteA, RouteB, true);
        AssertBool(CrossesShelf(route)).IsFalse();
        AssertBool(route.Zip(route.Skip(1), (a, b) => a.DistanceTo(b)).Sum() > 4.5f).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PickUpFreesTheWayAgain()
    {
        var placed = await PlaceShelf(8f, 6f);
        uint before = NavigationServer3D.RegionGetIterationId(_region.GetRid());
        AssertBool(_controller.BeginMove(placed)).IsTrue();
        await ShopNavigationRig.WaitForBakeSync(_region, before);

        AssertBool(_region.Reaches(RouteA, RouteB)).IsTrue();
        var route = NavigationServer3D.MapGetPath(_region.GetNavigationMap(), RouteA, RouteB, true);
        AssertBool(CrossesShelf(route)).IsTrue();
    }

    /// <summary>Places a shelf through begin, aim and confirm, and waits for the rebake it causes.</summary>
    private async Task<PlacedFurniture> PlaceShelf(float x, float z)
    {
        _controller.Begin(NewBox());
        _controller.AimAt(x, z);
        uint before = NavigationServer3D.RegionGetIterationId(_region.GetRid());
        AssertBool(_controller.Confirm()).IsTrue();
        await ShopNavigationRig.WaitForBakeSync(_region, before);
        return (PlacedFurniture)_furnitureRoot.GetChild(_furnitureRoot.GetChildCount() - 1);
    }

    /// <summary>Instances a shelf box, injects its services and adds it under the shop's World node.</summary>
    private FurnitureBox NewBox()
    {
        var box = GD.Load<PackedScene>(BoxScenePath).Instantiate<FurnitureBox>();
        TestServices.Inject(box);
        _shop.GetNode("World").AddChild(box);
        return box;
    }

}
