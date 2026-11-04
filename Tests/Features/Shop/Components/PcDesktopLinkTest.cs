using CardCleaner.Scripts.Features.Pause.Components;
using CardCleaner.Scripts.Features.Pause.Models;
using CardCleaner.Scripts.Features.Shop.Components;
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
    private ScreenTerminal _terminal = null!;
    private CursorOwner _cursor = null!;

    [BeforeTest]
    public void Setup()
    {
        _world = new Node3D();
        _terminal = new ScreenTerminal();
        _cursor = new CursorOwner();
        _world.AddChild(_cursor);

        TestServices.Reset();
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
        AssertBool(_terminal.IsOpen).IsTrue();
        // ASSUMPTION: this fixture is a bare ScreenTerminal with no Ui, so opening it raised no overlay.
        AssertBool(_terminal is OrderTerminal).IsFalse();
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

    // A PC built the way the shop scene builds it: the desktop from its scene, a plain Control as its stock app,
    // a screen camera and a pointer, all wired by a PcDesktopLink child of the terminal.
    private DesktopPc BuildDesktopPc()
    {
        var desktop = GD.Load<PackedScene>(DesktopScene).Instantiate<PcDesktop>();
        var stockApp = new Control { Name = "StockApp", Visible = false };
        desktop.AddChild(stockApp);
        desktop.StockApp = stockApp;
        desktop.Visible = false;
        var screenCamera = new Camera3D();
        var pointer = new PcScreenPointer();
        var link = new PcDesktopLink { Desktop = desktop, ScreenCamera = screenCamera, ScreenPointer = pointer };

        _world.AddChild(desktop);
        _world.AddChild(screenCamera);
        _world.AddChild(pointer);
        _terminal.AddChild(link);
        return new DesktopPc(desktop, screenCamera, pointer);
    }

    private sealed record DesktopPc(PcDesktop Desktop, Camera3D ScreenCamera, PcScreenPointer Pointer);
}
