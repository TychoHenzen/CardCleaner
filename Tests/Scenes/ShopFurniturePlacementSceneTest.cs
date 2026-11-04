using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Tests.TestUtilities;
using Godot;
using static CardCleaner.Tests.Scenes.ShopFurnitureRig;

namespace CardCleaner.Tests.Scenes;

/// <summary>Furniture placement in the real shop scene: ghost, confirm, cancel, blockers and the route.</summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopFurniturePlacementSceneTest
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
        TestServices.Reset();
        _root.RemoveChild(_shop);
        _shop.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PathCrossesTheShelfRectBeforePlacement()
    {
        // Control: with no furniture placed, the straight route from A to B passes where the shelf will stand.
        AssertBool(_region.Reaches(RouteA, RouteB)).IsTrue();
        var route = NavigationServer3D.MapGetPath(_region.GetNavigationMap(), RouteA, RouteB, true);
        AssertBool(CrossesShelf(route)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void BeginShowsAGhostAtTheSnappedAim()
    {
        AssertBool(_controller.Begin(NewBox())).IsTrue();
        AssertBool(_controller.IsActive).IsTrue();

        _controller.AimAt(7.8f, 6.2f);

        AssertFloat(_controller.CurrentPose.X).IsEqual(8f);
        AssertFloat(_controller.CurrentPose.Z).IsEqual(6f);
        AssertBool(_controller.CurrentValid).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void BeginRefusesASecondSession()
    {
        AssertBool(_controller.Begin(NewBox())).IsTrue();
        AssertBool(_controller.Begin(NewBox())).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ConfirmOnAnInvalidPoseIsRefused()
    {
        var box = NewBox();
        _controller.Begin(box);
        int rebakes = _region.RebakeCount;

        _controller.AimAt(0.5f, 0.5f);

        AssertBool(_controller.CurrentValid).IsFalse();
        AssertBool(_controller.Confirm()).IsFalse();
        AssertInt(_furnitureRoot.GetChildCount()).IsEqual(0);
        AssertBool(_controller.IsActive).IsTrue();
        AssertBool(GodotObject.IsInstanceValid(box)).IsTrue();
        AssertInt(_region.RebakeCount).IsEqual(rebakes);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ConfirmOnAValidPoseBecomesTheRealShelf()
    {
        var box = NewBox();
        _controller.Begin(box);
        int rebakes = _region.RebakeCount;
        _controller.AimAt(8f, 6f);

        AssertBool(_controller.Confirm()).IsTrue();
        AssertInt(_furnitureRoot.GetChildCount()).IsEqual(1);
        var placed = (PlacedFurniture)_furnitureRoot.GetChild(0);
        AssertString(placed.Item!.Id).IsEqual("shelf_unit");
        AssertBool(placed.Position.IsEqualApprox(new Vector3(8f, 0f, 6f))).IsTrue();

        await ISceneRunner.SyncPhysicsFrame;
        AssertBool(GodotObject.IsInstanceValid(box)).IsFalse();
        AssertBool(_controller.IsActive).IsFalse();
        AssertInt(_region.RebakeCount).IsEqual(rebakes + 1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RotateSwapsTheFootprint()
    {
        _controller.Begin(NewBox());
        _controller.AimAt(15f, 6f);
        AssertBool(_controller.CurrentValid).IsFalse();

        _controller.Rotate();
        AssertInt(_controller.CurrentPose.QuarterTurns).IsEqual(1);
        AssertBool(_controller.CurrentValid).IsTrue();

        for (int i = 0; i < 3; i++)
            _controller.Rotate();
        AssertInt(_controller.CurrentPose.QuarterTurns).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PlacedFurnitureBlocksAnOverlappingPose()
    {
        await PlaceShelf(8f, 6f);

        _controller.Begin(NewBox());
        _controller.AimAt(8f, 6.5f);
        AssertBool(_controller.CurrentValid).IsFalse();
        _controller.AimAt(8f, 7f);
        AssertBool(_controller.CurrentValid).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task APhysicsBodyBlocksThePose()
    {
        var body = new StaticBody3D { Name = "Blocker", Position = new Vector3(4f, 1f, 6f) };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2f, 2f, 2f) } });
        _shop.GetNode("World").AddChild(body);
        _controller.Begin(NewBox());

        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
        _controller.AimAt(4f, 6f);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
        AssertBool(_controller.CurrentValid).IsFalse();

        _controller.AimAt(4f, 9f);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
        AssertBool(_controller.CurrentValid).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CancelKeepsTheBox()
    {
        var box = NewBox();
        _controller.Begin(box);
        int rebakes = _region.RebakeCount;
        _controller.AimAt(8f, 6f);

        _controller.Cancel();

        AssertBool(_controller.IsActive).IsFalse();
        AssertBool(GodotObject.IsInstanceValid(box)).IsTrue();
        AssertInt(_furnitureRoot.GetChildCount()).IsEqual(0);
        AssertInt(_region.RebakeCount).IsEqual(rebakes);
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
