using System;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Tests.Features.SaveSlots.Services;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     Furniture through the real save path of the shop: the shop tree is written to a slot file by
///     <see cref="GameSaveService" /> (an in-memory file system) and loaded back with the shelf removed, so the
///     furniture record, the slot envelope and the tree loader are all in the round trip.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FurnitureSlotSaveSceneTest
{
    private const string RegionPath = "World/Navigation";
    private const string ControllerPath = "Services/FurniturePlacement";
    private const string FurnitureRootPath = "World/Furniture";
    private const string BoxScenePath = "res://Scenes/Shop/Items/ShelfBox.tscn";
    private const int SettleFrames = 10;

    private SceneTree _tree = null!;
    private Node? _previousScene;
    private Node3D _shop = null!;
    private ShopNavigationRegion _region = null!;
    private FurniturePlacementController _controller = null!;
    private GameSaveService _saves = null!;
    private Node3D _furnitureRoot = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _tree = (SceneTree)Engine.GetMainLoop();
        _previousScene = _tree.CurrentScene;
        _shop = ShopOrderingRig.LoadShopWithServices();

        var files = new InMemorySlotFileSystem();
        var slots = new ActiveSaveSlot();
        if (!SaveSlotName.TryCreate("furniture", out var slot, out var reason))
            throw new InvalidOperationException(reason);
        slots.Activate(slot);

        _saves = _shop.GetNode<GameSaveService>("Services/GameSaveService");
        TestServices.Container.RegisterSingleton<IActiveSaveSlot>(slots);
        TestServices.Container.RegisterSingleton<ISlotFileSystem>(files);
        TestServices.Container.RegisterSingleton<SlotStore>(new SlotStore(files));
        TestServices.Container.RegisterSingleton<ISaveGameCommand>(_saves);

        _region = _shop.GetNode<ShopNavigationRegion>(RegionPath);
        _controller = _shop.GetNode<FurniturePlacementController>(ControllerPath);
        // Inject before the shop enters the tree, so the controller's _Ready finds its resolver.
        TestServices.Inject(_controller);
        // The placement is not what is under test, so the cases set the pose with AimAt.
        _controller.AimOverride = true;

        uint regionBefore = NavigationServer3D.RegionGetIterationId(_region.GetRid());
        AddNode(_shop, autoFree: false);
        _tree.CurrentScene = _shop;
        _furnitureRoot = _shop.GetNode<Node3D>(FurnitureRootPath);
        await ShopNavigationRig.WaitForBakeSync(_region, regionBefore);
    }

    [AfterTest]
    public void Teardown()
    {
        _controller.Cancel();
        _tree.CurrentScene = _previousScene;
        _shop.Free();
        ShopPauseRig.Teardown();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task APlacedShelfComesBackFromTheSlotFile()
    {
        var placed = await PlaceShelf(8f, 6f);
        AssertBool(_saves.ManualSave()).IsTrue();
        _furnitureRoot.RemoveChild(placed);
        placed.Free();

        _saves.LoadGame();
        await Settle();

        AssertInt(_furnitureRoot.GetChildCount()).IsEqual(1);
        var restored = (PlacedFurniture)_furnitureRoot.GetChild(0);
        AssertString(restored.Item!.Id).IsEqual("shelf_unit");
        AssertBool(restored.Pose.X == 8f && restored.Pose.Z == 6f && restored.Pose.QuarterTurns == 0).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AnUnopenedBoxComesBackFromTheSlotFile()
    {
        var box = NewBox();
        box.GlobalPosition = new Vector3(3f, 0.5f, 9f);
        AssertBool(_saves.ManualSave()).IsTrue();
        box.GetParent().RemoveChild(box);
        box.Free();

        _saves.LoadGame();
        await Settle();

        var boxes = _tree.GetNodesInGroup(FurnitureBox.GroupName).OfType<FurnitureBox>().ToList();
        AssertInt(boxes.Count).IsEqual(1);
        AssertBool(boxes[0].GlobalPosition.X == 3f && boxes[0].GlobalPosition.Z == 9f).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ASaveDuringAMoveComesBackAsOneShelfAtItsOldPose()
    {
        var placed = await PlaceShelf(8f, 6f);
        AssertBool(_controller.BeginMove(placed)).IsTrue();
        _controller.AimAt(4f, 6f);
        AssertBool(_saves.ManualSave()).IsTrue();

        _saves.LoadGame();
        await Settle();
        // The load freed the carried piece; the cancel must cope with that and leave the restored shelf alone.
        _controller.Cancel();

        AssertInt(_furnitureRoot.GetChildCount()).IsEqual(1);
        var restored = (PlacedFurniture)_furnitureRoot.GetChild(0);
        AssertBool(restored.IsCarried).IsFalse();
        AssertBool(restored.Pose.X == 8f && restored.Pose.Z == 6f).IsTrue();
    }

    private async Task<PlacedFurniture> PlaceShelf(float x, float z)
    {
        _controller.Begin(NewBox());
        _controller.AimAt(x, z);
        uint before = NavigationServer3D.RegionGetIterationId(_region.GetRid());
        AssertBool(_controller.Confirm()).IsTrue();
        await ShopNavigationRig.WaitForBakeSync(_region, before);
        await Settle();
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

    private static async Task Settle()
    {
        for (var i = 0; i < SettleFrames; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }
}
