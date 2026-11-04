using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     The Furniture app's click path with real mouse input: the PC opens on the screen, the Furniture icon opens the
///     app, and Checkout delivers a shelf unit box to the delivery point, or refuses it when the balance is short.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FurnitureAppSceneTest
{
    private const string DesktopViewportPath = "PcDesktopViewport";
    private const string ShelfUnitName = "Shelf unit";
    private const string ShelfUnitId = "shelf_unit";
    private const string RawShelfId = "shelf";
    private const string FurnitureBoxGroup = "furniture_boxes";
    private const int UnaffordableBalance = 50;
    private static readonly Vector2I RootWindowSize = new(1280, 720);

    private Node3D _shop = null!;
    private ScreenTerminal _terminal = null!;
    private StockApp _furnitureApp = null!;
    private StockApp _stockApp = null!;
    private Button _furnitureIcon = null!;
    private PcScreen _screen = null!;
    private MoneyService _money = null!;
    private Window _root = null!;
    private Vector2I _originalRootSize;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = ShopOrderingRig.LoadShopWithServices();
        _terminal = _shop.GetNode<ScreenTerminal>("World/Markers/PcLocation/PcTerminal");
        _furnitureApp = _shop.GetNode<StockApp>($"{DesktopViewportPath}/FurnitureApp");
        _stockApp = _shop.GetNode<StockApp>($"{DesktopViewportPath}/StockApp");
        _furnitureIcon = _shop.GetNode<Button>($"{DesktopViewportPath}/PcDesktop/Icons/FurnitureIcon");
        _screen = PcScreen.From(_shop);
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        AddNode(_shop);
        _root = _shop.GetTree().Root;
        _originalRootSize = _root.Size;
        // A headless window is 64 x 64, which would collapse the desktop layout; mouse positions need the real size.
        _root.Size = RootWindowSize;
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public void Teardown()
    {
        _root.Size = _originalRootSize;
        TestServices.Reset();
        _root.RemoveChild(_shop);
        _shop.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task BuyingAShelfUnitDeliversAFurnitureBox()
    {
        var world = _shop.GetNode("World");
        var delivery = _shop.GetNode<Marker3D>("World/Markers/DeliveryPoint").GlobalPosition;
        var balance = _money.Balance;
        var price = ShelfUnitPrice();
        var before = world.GetChildCount();

        await OpenFurnitureApp();
        await AddAndCheckOutShelfUnit();

        AssertThat(_money.Balance).IsEqual(balance - price);
        AssertThat(world.GetChildCount()).IsEqual(before + 1);
        var delivered = world.GetChild(before) as FurnitureBox;
        AssertBool(delivered != null).IsTrue();
        AssertBool(delivered!.IsInGroup(FurnitureBoxGroup)).IsTrue();
        AssertString(delivered.Item!.Id).IsEqual(ShelfUnitId);
        AssertDeliveredInSlots(world, before, delivery);
        // The cart rows of the app are queued for deletion; the last frame lets those frees run before the test ends.
        await ISceneRunner.SyncPhysicsFrame;
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AnUnaffordableShelfUnitDeliversNothing()
    {
        AssertBool(_money.TrySpend(_money.Balance - UnaffordableBalance)).IsTrue();
        var world = _shop.GetNode("World");
        var before = world.GetChildCount();

        await OpenFurnitureApp();
        await AddAndCheckOutShelfUnit();

        AssertThat(_money.Balance).IsEqual(UnaffordableBalance);
        AssertThat(world.GetChildCount()).IsEqual(before);
        await ISceneRunner.SyncPhysicsFrame;
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TheStockAppStillSellsTheRawShelf()
    {
        // D3: the raw "shelf" row stays in the Stock app's catalog; the Furniture app sells shelf_unit instead.
        AssertBool(_stockApp.Catalog!.Items.Any(item => item.Id == RawShelfId)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task BuyThenPlaceEndsWithARealShelf()
    {
        var world = _shop.GetNode("World");
        var furniture = _shop.GetNode<Node3D>("World/Furniture");
        var controller = _shop.GetNode<FurniturePlacementController>("Services/FurniturePlacement");
        // The camera aim is not driven headless, so the pose is set with AimAt.
        controller.AimOverride = true;
        var before = world.GetChildCount();

        await OpenFurnitureApp();
        await AddAndCheckOutShelfUnit();

        var box = (FurnitureBox)world.GetChild(before);
        // Interact is proven in FurnitureBoxTest; the test rig does not tree-inject a delivered box, so begin directly.
        AssertBool(controller.Begin(box)).IsTrue();
        controller.AimAt(8f, 6f);
        AssertBool(controller.Confirm()).IsTrue();

        AssertThat(furniture.GetChildCount()).IsEqual(1);
        var placed = (PlacedFurniture)furniture.GetChild(0);
        AssertBool(placed.GetNodeOrNull<CardShelf>(PlacedFurniture.ContentName) != null).IsTrue();
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
        AssertBool(GodotObject.IsInstanceValid(box)).IsFalse();
    }

    private async Task OpenFurnitureApp()
    {
        _terminal.Interact();
        await _screen.Click(_furnitureIcon);
        AssertBool(_furnitureApp.Visible).IsTrue();
    }

    private async Task AddAndCheckOutShelfUnit()
    {
        await _screen.Click(AddButtonFor(_furnitureApp, ShelfUnitName));
        var checkout = FindButtonWithText(_furnitureApp, "Checkout");
        AssertBool(checkout != null && !checkout.Disabled).IsTrue();
        await _screen.Click(checkout!);
    }

    private int ShelfUnitPrice() => _furnitureApp.Catalog!.Items.First(item => item.Id == ShelfUnitId).Price;

    // Copied from ShopStockAppClickPathSceneTest: each box lands in its own delivery slot, within 1 cm of the slot.
    private static void AssertDeliveredInSlots(Node world, int before, Vector3 delivery)
    {
        var delivered = world.GetChildren().Skip(before).Cast<Node3D>().ToList();
        for (var slot = 0; slot < delivered.Count; slot++)
            AssertBool(delivered[slot].GlobalPosition.DistanceTo(delivery + OrderingService.SlotOffset(slot)) < 0.01f)
                .IsTrue();
    }

    // Copied from ShopStockAppClickPathSceneTest: the Add button of the card whose title label reads displayName.
    private static Button AddButtonFor(Node app, string displayName)
    {
        foreach (var panel in app.FindChildren("*", "PanelContainer", true, false).OfType<PanelContainer>())
        {
            var titled = panel.FindChildren("*", "Label", true, false).OfType<Label>()
                .Any(label => label.Text == displayName);
            var add = titled ? FindButtonWithText(panel, "Add") : null;
            if (add != null)
                return add;
        }
        throw new System.InvalidOperationException($"No Add button for {displayName}");
    }

    // Copied from ShopStockAppClickPathSceneTest.
    private static Button? FindButtonWithText(Node root, string text)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button button && button.Text == text)
                return button;
            var found = FindButtonWithText(child, text);
            if (found != null)
                return found;
        }
        return null;
    }
}
