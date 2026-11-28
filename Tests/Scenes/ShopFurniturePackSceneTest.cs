using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Shop.Building;
using CardCleaner.Scripts.Features.Shop.Furniture;
using CardCleaner.Scripts.Features.Shop.Shelves;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;
using Saveable;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     Packing placed furniture back into its box in the real shop scene: the box takes the piece's place, a stocked
///     shelf is refused, the box places the piece again, and a packed starting shelf stays packed through a reload.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopFurniturePackSceneTest
{
    private const string RegionPath = "World/Navigation";
    private const string ControllerPath = "Services/FurniturePlacement";
    private const string FurnitureRootPath = "World/Furniture";
    private const string SavePath = "Services/FurnitureSave";
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

        uint regionBefore = NavigationServer3D.RegionGetIterationId(_region.GetRid());
        AddNode(_shop);
        _furnitureRoot = _shop.GetNode<Node3D>(FurnitureRootPath);
        _root = _shop.GetTree().Root;
        await ShopNavigationRig.WaitForBakeSync(_region, regionBefore);
    }

    [AfterTest]
    public void Teardown()
    {
        _controller.Cancel();
        TestServices.Reset();
        _root.RemoveChild(_shop);
        _shop.Free();
    }

    [TestCase]
    public async Task PackingAnEmptyShelfLeavesItsBoxWhereItStood()
    {
        var placed = await PlaceShelf(4f, 8f);
        var standing = _furnitureRoot.GetChildCount();
        int rebakes = _region.RebakeCount;

        AssertBool(_controller.Pack(placed)).IsTrue();

        AssertInt(_furnitureRoot.GetChildCount()).IsEqual(standing - 1);
        AssertBool(placed.IsInsideTree()).IsFalse();
        AssertInt(_region.RebakeCount).IsEqual(rebakes + 1);
        var box = BoxesAt(4f, 8f).Single();
        AssertThat(box.Item!.Id).IsEqual("shelf_unit");
        // Packing takes the piece out of the tree and queues it; let it go before the test returns.
        await BoundedWait.ForQueuedFrees();
    }

    [TestCase]
    public async Task PackingIsRefusedWhileTheShelfHoldsStock()
    {
        var placed = await PlaceShelf(4f, 8f);
        var slot = placed.GetNode<CardShelf>(PlacedFurniture.ContentName).Slots.First();
        var card = ShopTestCards.Create(new CardSignature());
        _shop.GetNode("World").AddChild(card);
        AssertBool(slot.TryLock(card)).IsTrue();
        int rebakes = _region.RebakeCount;

        AssertBool(_controller.Pack(placed)).IsFalse();

        AssertBool(placed.IsInsideTree()).IsTrue();
        AssertInt(BoxesAt(4f, 8f).Count()).IsEqual(0);
        AssertInt(_region.RebakeCount).IsEqual(rebakes);
    }

    [TestCase]
    public async Task APackedBoxPlacesTheShelfAgain()
    {
        var placed = await PlaceShelf(4f, 8f);
        var standing = _furnitureRoot.GetChildCount();
        AssertBool(_controller.Pack(placed)).IsTrue();
        await ISceneRunner.SyncPhysicsFrame;
        var box = BoxesAt(4f, 8f).Single();

        AssertBool(_controller.Begin(box)).IsTrue();
        // The box is lifted while its shelf is placed, so the shelf can go back down on the very spot the box holds.
        _controller.AimAt(4f, 8f);
        AssertBool(_controller.Confirm()).IsTrue();

        AssertInt(_furnitureRoot.GetChildCount()).IsEqual(standing);
        AssertBool(box.IsQueuedForDeletion()).IsTrue();
    }

    [TestCase]
    public async Task CancellingThePlacementSetsTheBoxDownAgain()
    {
        var placed = await PlaceShelf(4f, 8f);
        AssertBool(_controller.Pack(placed)).IsTrue();
        await ISceneRunner.SyncPhysicsFrame;
        var box = BoxesAt(4f, 8f).Single();

        AssertBool(_controller.Begin(box)).IsTrue();
        AssertBool(box.Visible).IsFalse();
        _controller.Cancel();

        AssertBool(box.Visible).IsTrue();
        AssertBool(box.IsQueuedForDeletion()).IsFalse();
        AssertBool(PhysicsServer3D.BodyGetSpace(box.GetRid()).IsValid).IsTrue();
    }

    [TestCase]
    public async Task APackedStartingShelfStaysPackedThroughAReload()
    {
        var product = _furnitureRoot.GetNode<PlacedFurniture>("ProductShelf");
        var at = product.Pose;
        AssertBool(product.CanPickUp).IsTrue();
        AssertBool(_controller.Pack(product)).IsTrue();
        await ISceneRunner.SyncPhysicsFrame;
        var service = _shop.GetNode<FurnitureSaveService>(SavePath);
        var save = new NodeSave();
        service.Save(save);

        service.Load(save);

        AssertBool(_furnitureRoot.HasNode("ProductShelf")).IsFalse();
        AssertThat(BoxesAt(at.X, at.Z).Single().Item!.Id).IsEqual("product_shelf");
        // The other starting shelves are the same nodes, so their price tags keep their save ids.
        AssertBool(_furnitureRoot.HasNode("CardShelf") && _furnitureRoot.HasNode("StorageRack")).IsTrue();
    }

    /// <summary>The furniture boxes standing on the floor point (x, z).</summary>
    private System.Collections.Generic.IEnumerable<FurnitureBox> BoxesAt(float x, float z) =>
        _shop.GetTree().GetNodesInGroup(FurnitureBox.GroupName).OfType<FurnitureBox>()
            .Where(box => !box.IsQueuedForDeletion()
                          && Mathf.IsEqualApprox(box.GlobalPosition.X, x, 0.05f)
                          && Mathf.IsEqualApprox(box.GlobalPosition.Z, z, 0.05f));

    /// <summary>Places a shelf through begin, aim and confirm, and waits for the rebake it causes.</summary>
    private async Task<PlacedFurniture> PlaceShelf(float x, float z)
    {
        var box = GD.Load<PackedScene>(BoxScenePath).Instantiate<FurnitureBox>();
        TestServices.Inject(box);
        _shop.GetNode("World").AddChild(box);
        _controller.Begin(box);
        _controller.AimAt(x, z);
        uint before = NavigationServer3D.RegionGetIterationId(_region.GetRid());
        AssertBool(_controller.Confirm()).IsTrue();
        await ShopNavigationRig.WaitForBakeSync(_region, before);
        return (PlacedFurniture)_furnitureRoot.GetChild(_furnitureRoot.GetChildCount() - 1);
    }
}
