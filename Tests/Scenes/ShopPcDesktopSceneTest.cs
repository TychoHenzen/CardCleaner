using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Pause.Components;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     End to end in the shop scene: interacting with the backoffice PC opens its desktop on the screen, the mouse is
///     forwarded to the desktop as real input, and the Stock icon opens the Stock app on that screen. Ordering through
///     the app is covered by ShopStockAppClickPathSceneTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopPcDesktopSceneTest
{
    private const string DesktopViewportPath = "PcDesktopViewport";
    private static readonly Vector2I RootWindowSize = new(1280, 720);

    private Node3D _shop = null!;
    private PlayerController _player = null!;
    private ScreenTerminal _terminal = null!;
    private StockApp _stockApp = null!;
    private PcDesktop _desktop = null!;
    private Button _stockIcon = null!;
    private PcScreen _screen = null!;
    private CursorOwner _cursor = null!;
    private Window _root = null!;
    private Vector2I _originalRootSize;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = ShopOrderingRig.LoadShopWithServices();
        _player = _shop.GetNode<PlayerController>("Player");
        _terminal = _shop.GetNode<ScreenTerminal>("World/Markers/PcLocation/PcTerminal");
        _stockApp = _shop.GetNode<StockApp>($"{DesktopViewportPath}/StockApp");
        _desktop = _shop.GetNode<PcDesktop>($"{DesktopViewportPath}/PcDesktop");
        _stockIcon = _shop.GetNode<Button>($"{DesktopViewportPath}/PcDesktop/Icons/StockIcon");
        _screen = PcScreen.From(_shop);
        _cursor = _shop.GetNode<CursorOwner>("Services/CursorOwner");
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
    }

    [TestCase]
    [TestCategory("Unit")]
    public void InteractMakesScreenCameraCurrentAndCursorVisible()
    {
        _terminal.Interact();

        AssertBool(_desktop.Visible).IsTrue();
        AssertBool(_screen.Camera.Current).IsTrue();
        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Visible);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CloseRestoresPlayerCameraAndCapturedCursor()
    {
        var playerCamera = _player.GetNode<Camera3D>("Head/Camera3D");
        AssertBool(_screen.Camera.Current).IsFalse();

        _terminal.Interact();
        _terminal.Close();

        AssertBool(playerCamera.Current).IsTrue();
        AssertBool(_screen.Camera.Current).IsFalse();
        AssertBool(_desktop.Visible).IsFalse();
        AssertBool(_player.ControlEnabled).IsTrue();
        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Captured);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task EscapeWithPcOpenClosesOnlyThePc()
    {
        _terminal.Interact();

        await PcScreen.PressEscape();

        AssertBool(_terminal.IsOpen).IsFalse();
        AssertBool(_desktop.Visible).IsFalse();
        AssertBool(_shop.GetTree().Paused).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ReopenShowsDesktopWithStockAppClosed()
    {
        _terminal.Interact();
        await _screen.Click(_stockIcon);
        AssertBool(_stockApp.Visible).IsTrue();

        await PcScreen.PressEscape();
        _terminal.Interact();

        AssertBool(_desktop.Visible).IsTrue();
        AssertBool(_stockApp.Visible).IsFalse();
    }
}
