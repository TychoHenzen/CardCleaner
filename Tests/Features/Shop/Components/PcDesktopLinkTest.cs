using CardCleaner.Scripts.Features.Pause.Components;
using CardCleaner.Scripts.Features.Pause.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

[TestSuite]
[RequireGodotRuntime]
public class PcDesktopLinkTest
{
    private const string DesktopScene = "res://Scenes/Shop/PcDesktop.tscn";

    private Node3D _world = null!;
    private OrderTerminal _terminal = null!;
    private OrderTerminalUi _ui = null!;
    private CursorOwner _cursor = null!;

    [BeforeTest]
    public void Setup()
    {
        _world = new Node3D();
        var money = new MoneyService { StartingBalance = 0 };
        _ui = new OrderTerminalUi();
        _terminal = new OrderTerminal { Catalog = new OrderCatalog(), Ui = _ui };
        _cursor = new CursorOwner();
        _world.AddChild(money);
        _world.AddChild(_ui);
        _world.AddChild(_cursor);

        TestServices.Reset();
        TestServices.Container.RegisterSingleton<IMoneyService>(money);
        TestServices.Container.RegisterSingleton<ICursorOwner>(_cursor);

        _world.AddChild(_terminal);
        AddNode(_world);
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OpenWithDesktopMakesScreenCameraCurrentAndRestoresOnClose()
    {
        var playerCamera = AddPlayerCamera();
        var (desktop, screenCamera, pointer) = BuildDesktopPc();

        _terminal.Interact();

        AssertBool(screenCamera.Current).IsTrue();
        AssertBool(playerCamera.Current).IsFalse();
        AssertBool(pointer.Active).IsTrue();
        AssertBool(desktop.Visible).IsTrue();
        AssertBool(_ui.Visible).IsFalse();
        AssertBool(_cursor.Claims.HasClaims).IsTrue();

        _terminal.Close();

        AssertBool(playerCamera.Current).IsTrue();
        AssertBool(screenCamera.Current).IsFalse();
        AssertBool(pointer.Active).IsFalse();
        AssertBool(desktop.Visible).IsFalse();
        AssertBool(_cursor.Claims.HasClaims).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EscapeClosesTheDesktop()
    {
        var playerCamera = AddPlayerCamera();
        var (desktop, _, _) = BuildDesktopPc();
        _terminal.Interact();

        _terminal._UnhandledInput(new InputEventAction { Action = "ui_cancel", Pressed = true });

        AssertBool(_terminal.IsOpen).IsFalse();
        AssertBool(desktop.Visible).IsFalse();
        AssertBool(playerCamera.Current).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TaskbarCloseButtonClosesTheDesktop()
    {
        var playerCamera = AddPlayerCamera();
        var (desktop, _, _) = BuildDesktopPc();
        _terminal.Interact();

        desktop.GetNode<Button>("Taskbar/CloseButton").EmitSignal(BaseButton.SignalName.Pressed);

        AssertBool(_terminal.IsOpen).IsFalse();
        AssertBool(desktop.Visible).IsFalse();
        AssertBool(playerCamera.Current).IsTrue();
        AssertBool(_cursor.Claims.HasClaims).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FreeingTheTerminalWhileOpenRestoresTheCamera()
    {
        var playerCamera = AddPlayerCamera();
        var (_, screenCamera, _) = BuildDesktopPc();
        _terminal.Interact();

        _terminal.Free();

        AssertBool(playerCamera.Current).IsTrue();
        AssertBool(screenCamera.Current).IsFalse();
        AssertBool(_cursor.Claims.HasClaims).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DoubleOpenAndDoubleCloseActOnce()
    {
        var playerCamera = AddPlayerCamera();
        var (_, screenCamera, _) = BuildDesktopPc();

        _terminal.Open();
        _terminal.Open();

        AssertBool(screenCamera.Current).IsTrue();
        AssertBool(_cursor.Claims.HasClaims).IsTrue();

        _terminal.Close();
        _terminal.Close();

        AssertBool(playerCamera.Current).IsTrue();
        AssertBool(_cursor.Claims.HasClaims).IsFalse();
        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Captured);
    }

    private Camera3D AddPlayerCamera()
    {
        var camera = new Camera3D();
        _world.AddChild(camera);
        camera.Current = true;
        return camera;
    }

    // A PC built the way the shop scene builds it: the desktop from its scene (showing the terminal's ordering
    // screen), a screen camera and a pointer, all wired by a PcDesktopLink child of the terminal.
    private (PcDesktop Desktop, Camera3D ScreenCamera, PcScreenPointer Pointer) BuildDesktopPc()
    {
        var desktop = GD.Load<PackedScene>(DesktopScene).Instantiate<PcDesktop>();
        desktop.StockApp = _ui;
        desktop.Visible = false;
        var screenCamera = new Camera3D();
        var pointer = new PcScreenPointer();
        var link = new PcDesktopLink { Desktop = desktop, ScreenCamera = screenCamera, ScreenPointer = pointer };

        _world.AddChild(desktop);
        _world.AddChild(screenCamera);
        _world.AddChild(pointer);
        _terminal.AddChild(link);
        return (desktop, screenCamera, pointer);
    }
}
