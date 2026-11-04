using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     Wiring of the shop scene's PC: the backoffice terminal is a ScreenTerminal with no order screen of its own, the
///     Stock and Bills apps are assigned on the desktop, and their icons open them. The workshop terminal keeps its UI.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopStockAppSceneTest
{
    private const string DesktopViewportPath = "PcDesktopViewport";
    private const string BackofficeTerminalPath = "World/Markers/PcLocation/PcTerminal";
    private const string WorkshopTerminalPath = "World/Workshop/OrderingRoom/OrderTerminal";

    private Node3D _shop = null!;
    private Window _root = null!;
    private Vector2I _originalRootSize;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = ShopOrderingRig.LoadShopWithServices();
        AddNode(_shop);
        _root = _shop.GetTree().Root;
        _originalRootSize = _root.Size;
        _root.Size = new Vector2I(1280, 720);
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
    public void BackofficePcIsAScreenTerminalWithoutOrderUi()
    {
        var pc = _shop.GetNode(BackofficeTerminalPath);

        AssertBool(pc is ScreenTerminal).IsTrue();
        AssertBool(pc is OrderTerminal).IsFalse();
        AssertBool(_shop.GetNodeOrNull($"{DesktopViewportPath}/OrderUi") == null).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void StockAndBillsAppsAreAssignedOnTheDesktop()
    {
        var desktop = _shop.GetNode<PcDesktop>($"{DesktopViewportPath}/PcDesktop");

        AssertBool(desktop.StockApp is StockApp).IsTrue();
        AssertBool(desktop.BillsApp is BillsApp).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task BillsIconOpensBillsAndStockIconOpensStock()
    {
        var stock = _shop.GetNode<Control>($"{DesktopViewportPath}/StockApp");
        var bills = _shop.GetNode<Control>($"{DesktopViewportPath}/BillsApp");
        var stockIcon = _shop.GetNode<Button>($"{DesktopViewportPath}/PcDesktop/Icons/StockIcon");
        var billsIcon = _shop.GetNode<Button>($"{DesktopViewportPath}/PcDesktop/Icons/BillsIcon");

        billsIcon.EmitSignal(BaseButton.SignalName.Pressed);
        await ISceneRunner.SyncPhysicsFrame;
        AssertBool(bills.Visible).IsTrue();
        AssertBool(stock.Visible).IsFalse();

        // Each app rebuilds its rows by queueing the old ones for deletion; a frame lets those frees run before the
        // test ends, so no detached node is left behind.
        stockIcon.EmitSignal(BaseButton.SignalName.Pressed);
        await ISceneRunner.SyncPhysicsFrame;
        AssertBool(stock.Visible).IsTrue();
        AssertBool(bills.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WorkshopOrderTerminalStillHasItsUi()
    {
        var terminal = _shop.GetNode<OrderTerminal>(WorkshopTerminalPath);

        AssertBool(terminal.Ui != null).IsTrue();
    }
}
