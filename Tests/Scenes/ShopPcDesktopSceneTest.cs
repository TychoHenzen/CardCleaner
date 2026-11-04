using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Pause.Components;
using CardCleaner.Scripts.Features.Pause.Models;
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
    private SubViewport _desktopViewport = null!;
    private MeshInstance3D _screenQuad = null!;
    private Camera3D _screenCamera = null!;
    private CursorOwner _cursor = null!;
    private Window _root = null!;
    private Vector2I _originalRootSize;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _player = _shop.GetNode<PlayerController>("Player");
        _terminal = _shop.GetNode<ScreenTerminal>("World/Markers/PcLocation/PcTerminal");
        _stockApp = _shop.GetNode<StockApp>($"{DesktopViewportPath}/StockApp");
        _desktop = _shop.GetNode<PcDesktop>($"{DesktopViewportPath}/PcDesktop");
        _stockIcon = _shop.GetNode<Button>($"{DesktopViewportPath}/PcDesktop/Icons/StockIcon");
        _desktopViewport = _shop.GetNode<SubViewport>(DesktopViewportPath);
        _screenQuad = _shop.GetNode<MeshInstance3D>("World/Markers/PcLocation/PcTerminal/ScreenQuad");
        _screenCamera = _shop.GetNode<Camera3D>("World/Markers/PcLocation/PcTerminal/ScreenCamera");
        _cursor = _shop.GetNode<CursorOwner>("Services/CursorOwner");
        var money = _shop.GetNode<MoneyService>("Services/MoneyService");
        var ordering = _shop.GetNode<OrderingService>("Services/OrderingService");

        // The test scene is not the current scene, so hand the scene-owned services over directly.
        TestServices.Reset();
        TestServices.Container.RegisterSingleton<IGameSettings>(_shop.GetNode<GameSettings>("Services/GameSettings"));
        TestServices.Container.RegisterSingleton<IMoneyService>(money);
        var progression = _shop.GetNode<ShopProgression>("Services/ShopProgression");
        TestServices.Container.RegisterSingleton<IShopProgression>(progression);
        TestServices.Container.RegisterSingleton<IOrderingService>(ordering);
        TestServices.Container.RegisterSingleton<ICursorOwner>(_cursor);

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
        AssertBool(_screenCamera.Current).IsTrue();
        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Visible);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CloseRestoresPlayerCameraAndCapturedCursor()
    {
        var playerCamera = _player.GetNode<Camera3D>("Head/Camera3D");
        AssertBool(_screenCamera.Current).IsFalse();

        _terminal.Interact();
        _terminal.Close();

        AssertBool(playerCamera.Current).IsTrue();
        AssertBool(_screenCamera.Current).IsFalse();
        AssertBool(_desktop.Visible).IsFalse();
        AssertBool(_player.ControlEnabled).IsTrue();
        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Captured);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task EscapeWithPcOpenClosesOnlyThePc()
    {
        _terminal.Interact();

        await PressEscape();

        AssertBool(_terminal.IsOpen).IsFalse();
        AssertBool(_desktop.Visible).IsFalse();
        AssertBool(_shop.GetTree().Paused).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ReopenShowsDesktopWithStockAppClosed()
    {
        _terminal.Interact();
        await ClickAt(ScreenPositionOf(_stockIcon));
        AssertBool(_stockApp.Visible).IsTrue();

        await PressEscape();
        _terminal.Interact();

        AssertBool(_desktop.Visible).IsTrue();
        AssertBool(_stockApp.Visible).IsFalse();
    }

    // Maps a control on the desktop viewport to the window pixel where the mouse must click to hit it. The control's
    // pixel becomes a point on the screen quad, and the screen camera projects that point back to the window.
    private Vector2 ScreenPositionOf(Control control)
    {
        Vector2 pixel = control.GetGlobalRect().GetCenter();
        Vector2 viewportSize = new(_desktopViewport.Size.X, _desktopViewport.Size.Y);
        var quadSize = ((QuadMesh)_screenQuad.Mesh).Size;
        var local = new Vector3(
            (pixel.X / viewportSize.X - 0.5f) * quadSize.X,
            (0.5f - pixel.Y / viewportSize.Y) * quadSize.Y,
            0f);
        return _screenCamera.UnprojectPosition(_screenQuad.GlobalTransform * local);
    }

    // Each injected event is flushed to the nodes as it is sent: ParseInputEvent only queues it, and without the
    // flush the event would not reach _Input or _UnhandledInput before the test reads the result.
    private static async Task ClickAt(Vector2 position)
    {
        Send(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left, Pressed = true, Position = position, GlobalPosition = position
        });
        await ISceneRunner.SyncPhysicsFrame;
        Send(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left, Pressed = false, Position = position, GlobalPosition = position
        });
        await ISceneRunner.SyncPhysicsFrame;
    }

    // Sent as the ui_cancel action, the same way ShopCheckoutSceneTest closes its screens.
    private static async Task PressEscape()
    {
        Send(new InputEventAction { Action = "ui_cancel", Pressed = true });
        await ISceneRunner.SyncPhysicsFrame;
        Send(new InputEventAction { Action = "ui_cancel", Pressed = false });
        await ISceneRunner.SyncPhysicsFrame;
    }

    private static void Send(InputEvent input)
    {
        Input.ParseInputEvent(input);
        Input.FlushBufferedEvents();
    }
}
