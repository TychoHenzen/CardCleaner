using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Building;
using CardCleaner.Scripts.Features.Shop.Furniture;
using CardCleaner.Tests.TestUtilities;
using Godot;
using Saveable;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     Furniture saved and loaded through the real shop scene: the shelves survive, and a load rebakes once.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FurnitureSaveSceneTest
{
    private const string RegionPath = "World/Navigation";
    private const string ControllerPath = "Services/FurniturePlacement";
    private const string SavePath = "Services/FurnitureSave";
    private const string FurnitureRootPath = "World/Furniture";
    private const string BoxScenePath = "res://Scenes/Shop/Items/ShelfBox.tscn";

    private Node3D _shop = null!;
    private ShopNavigationRegion _region = null!;
    private FurniturePlacementController _controller = null!;
    private FurnitureSaveService _save = null!;
    private Node3D _furnitureRoot = null!;

    // The shop's starting furniture, standing under the furniture root before a case places a piece.
    private int _standing;
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

        // The first bake runs in AddNode once the scene is ready, so read the iteration id before adding the scene.
        uint regionBefore = NavigationServer3D.RegionGetIterationId(_region.GetRid());
        AddNode(_shop);
        _save = _shop.GetNode<FurnitureSaveService>(SavePath);
        _furnitureRoot = _shop.GetNode<Node3D>(FurnitureRootPath);
        _standing = _furnitureRoot.GetChildCount();
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
    public async Task PlacedShelvesSurviveASave()
    {
        var placed = await PlaceShelf(4f, 8f);
        var save = new NodeSave();
        _save.Save(save);
        _furnitureRoot.RemoveChild(placed);
        placed.Free();
        AssertInt(_furnitureRoot.GetChildCount()).IsEqual(_standing);

        _save.Load(save);

        AssertInt(_furnitureRoot.GetChildCount()).IsEqual(_standing + 1);
        var restored = (PlacedFurniture)_furnitureRoot.GetChild(_standing);
        AssertString(restored.Item!.Id).IsEqual("shelf_unit");
        AssertBool(restored.Pose.X == 4f && restored.Pose.Z == 8f && restored.Pose.QuarterTurns == 0).IsTrue();
    }

    [TestCase]
    public async Task LoadRebakesTheNavigationOnce()
    {
        await PlaceShelf(4f, 8f);
        var save = new NodeSave();
        _save.Save(save);
        int count = _region.RebakeCount;
        uint before = NavigationServer3D.RegionGetIterationId(_region.GetRid());

        _save.Load(save);
        await ShopNavigationRig.WaitForBakeSync(_region, before);

        AssertInt(_region.RebakeCount).IsEqual(count + 1);
        // The restored shelf is an obstacle again: the route between the two points goes around its rect.
        var route = NavigationServer3D.MapGetPath(
            _region.GetNavigationMap(), ShopFurnitureRig.RouteA, ShopFurnitureRig.RouteB, true);
        AssertBool(ShopFurnitureRig.CrossesShelf(route)).IsFalse();
    }

    // NodeSave level only: the stored records are copied into a fresh save and loaded into a shop with the shelf
    // removed. The slot file round trip is FurnitureSlotSaveSceneTest.
    [TestCase]
    public async Task ReloadingTheStoredRecordsKeepsTheShelf()
    {
        var placed = await PlaceShelf(4f, 8f);
        var save = new NodeSave();
        _save.Save(save);
        AssertBool(save.TryGetProperty<string>(FurnitureSaveService.RecordsKey, out var json)).IsTrue();
        _furnitureRoot.RemoveChild(placed);
        placed.Free();

        var reloaded = new NodeSave();
        reloaded.SetOrAddProperty(FurnitureSaveService.RecordsKey, json ?? string.Empty);
        reloaded.SetOrAddProperty(FurnitureSaveService.WholeLayoutKey, true);
        _save.Load(reloaded);

        AssertInt(_furnitureRoot.GetChildCount()).IsEqual(_standing + 1);
        var restored = (PlacedFurniture)_furnitureRoot.GetChild(_standing);
        AssertBool(restored.Pose.X == 4f && restored.Pose.Z == 8f).IsTrue();
    }

    /// <summary>
    ///     Places a shelf through begin, aim and confirm, and waits for the rebake and the freed ghost box.
    /// </summary>
    private async Task<PlacedFurniture> PlaceShelf(float x, float z)
    {
        _controller.Begin(NewBox());
        _controller.AimAt(x, z);
        uint before = NavigationServer3D.RegionGetIterationId(_region.GetRid());
        AssertBool(_controller.Confirm()).IsTrue();
        await ShopNavigationRig.WaitForBakeSync(_region, before);
        await ISceneRunner.SyncPhysicsFrame;
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
