using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     The whole backoffice ordering path with real mouse input: the PC opens on the screen, the Stock icon opens the
///     Stock app, a cardboard box is added twice and Checkout charges and delivers both boxes.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopStockAppClickPathSceneTest
{
    private const string DesktopViewportPath = "PcDesktopViewport";
    private const string BoxId = "cardboard_box";
    private static readonly Vector2I RootWindowSize = new(1280, 720);

    private Node3D _shop = null!;
    private ScreenTerminal _terminal = null!;
    private StockApp _stockApp = null!;
    private Button _stockIcon = null!;
    private PcScreen _screen = null!;
    private MoneyService _money = null!;
    private ShopProgression _progression = null!;
    private Window _root = null!;
    private Vector2I _originalRootSize;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = ShopOrderingRig.LoadShopWithServices();
        _terminal = _shop.GetNode<ScreenTerminal>("World/Markers/PcLocation/PcTerminal");
        _stockApp = _shop.GetNode<StockApp>($"{DesktopViewportPath}/StockApp");
        _stockIcon = _shop.GetNode<Button>($"{DesktopViewportPath}/PcDesktop/Icons/StockIcon");
        _screen = PcScreen.From(_shop);
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        _progression = _shop.GetNode<ShopProgression>("Services/ShopProgression");
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
    public async Task ClickPathFromPcToDeliveredItem()
    {
        // The cardboard box unlocks at shop level 1, which a new shop already is, so its Add button is enabled here.
        var box = _stockApp.Catalog!.Items.First(item => item.Id == BoxId);
        var world = _shop.GetNode("World");
        var delivery = _shop.GetNode<Marker3D>("World/Markers/DeliveryPoint").GlobalPosition;
        var balance = _money.Balance;
        var before = world.GetChildCount();

        // The terminal opens through Interact(), the method the player's InteractionSystem calls on a hit. Driving the
        // real ray needs the player, the input service and a camera aimed at the PC; this test replaces the scene's
        // services with the scene-owned ones, so that path is not available headless here. That the ray reaches the
        // PC is proven by ShopOrderingSceneTest.PlayerInteractionRayCanReachThePcFromInFrontOfTheDesk. Everything
        // after the open (the screen click, the icon, the cards, Checkout) is real mouse input.
        _terminal.Interact();
        await _screen.Click(_stockIcon);
        AssertBool(_stockApp.Visible).IsTrue();

        await _screen.Click(AddButtonFor(box.DisplayName));
        await _screen.Click(AddButtonFor(box.DisplayName));
        var checkout = FindButtonWithText(_stockApp, "Checkout");
        AssertBool(checkout != null && !checkout.Disabled).IsTrue();
        await _screen.Click(checkout!);

        AssertThat(_money.Balance).IsEqual(balance - 2 * box.Price);
        AssertThat(world.GetChildCount()).IsEqual(before + 2);
        AssertDeliveredInSlots(world, before, delivery);
        AssertBool(FindButtonWithText(_stockApp, "Checkout")!.Disabled).IsTrue();
        // The cart rows of the app are queued for deletion; the last frame lets those frees run before the test ends.
        await ISceneRunner.SyncPhysicsFrame;
    }

    // Each box lands in its own delivery slot. Placed through a float32 transform, so the position differs from
    // the expected one by rounding; the 1 cm tolerance matches ShopCarryingSceneTest.
    private static void AssertDeliveredInSlots(Node world, int before, Vector3 delivery)
    {
        var delivered = world.GetChildren().Skip(before).Cast<Node3D>().ToList();
        for (var slot = 0; slot < delivered.Count; slot++)
            AssertBool(delivered[slot].GlobalPosition.DistanceTo(delivery + OrderingService.SlotOffset(slot)) < 0.01f)
                .IsTrue();
    }

    // The Add button of the card whose title label reads displayName. Found again after every click, because a
    // card can be rebuilt between clicks.
    private Button AddButtonFor(string displayName)
    {
        foreach (var panel in _stockApp.FindChildren("*", "PanelContainer", true, false).OfType<PanelContainer>())
        {
            var titled = panel.FindChildren("*", "Label", true, false).OfType<Label>()
                .Any(label => label.Text == displayName);
            var add = titled ? FindButtonWithText(panel, "Add") : null;
            if (add != null)
                return add;
        }
        throw new System.InvalidOperationException($"No Add button for {displayName}");
    }

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
