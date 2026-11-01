using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Pause.Components;
using CardCleaner.Scripts.Features.Pause.Controllers;
using CardCleaner.Scripts.Features.Pause.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The shop scene's mouse and Escape behaviour: play captures the mouse, Escape pauses behind the menu (and
/// Resume or Escape unpauses), and the PC screen claims the mouse without letting Escape pause the game.
/// Every case is synchronous after Setup, so no case awaits while the tree is paused.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopPauseTest
{
    private Node3D _shop = null!;
    private PauseController _pause = null!;
    private CursorOwner _cursor = null!;
    private OrderTerminal _terminal = null!;
    private OrderTerminalUi _ui = null!;
    private Control _menu = null!;
    private Button _resumeButton = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _pause = _shop.GetNode<PauseController>("Pause");
        _cursor = _shop.GetNode<CursorOwner>("Services/CursorOwner");
        _terminal = _shop.GetNode<OrderTerminal>("World/Markers/PcLocation/PcTerminal");
        _ui = _shop.GetNode<OrderTerminalUi>("OrderUi");
        _menu = _shop.GetNode<Control>("Pause/PauseMenu/Panel");
        _resumeButton = _shop.GetNode<Button>("Pause/PauseMenu/Panel/ResumeButton");

        // The test scene is not the current scene, so hand the scene-owned services over directly.
        TestServices.Reset();
        TestServices.Container.RegisterSingleton<IGameSettings>(_shop.GetNode<GameSettings>("Services/GameSettings"));
        TestServices.Container.RegisterSingleton<IMoneyService>(_shop.GetNode<MoneyService>("Services/MoneyService"));
        TestServices.Container.RegisterSingleton<IOrderingService>(
            _shop.GetNode<OrderingService>("Services/OrderingService"));
        TestServices.Container.RegisterSingleton<ICursorOwner>(_cursor);

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public static void Teardown()
    {
        (Engine.GetMainLoop() as SceneTree)!.Paused = false;
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void InPlay_CursorCaptured()
    {
        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Captured);
        AssertBool(_shop.GetTree().Paused).IsFalse();
        AssertBool(_menu.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Escape_PausesShowsMenuAndReleasesCursor()
    {
        _pause._UnhandledInput(EscapeEvent());

        AssertBool(_shop.GetTree().Paused).IsTrue();
        AssertBool(_menu.Visible).IsTrue();
        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Visible);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EscapeThenResumeButton_ResumesAndCaptures()
    {
        _pause._UnhandledInput(EscapeEvent());

        _resumeButton.EmitSignal(BaseButton.SignalName.Pressed);

        AssertBool(_shop.GetTree().Paused).IsFalse();
        AssertBool(_menu.Visible).IsFalse();
        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Captured);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EscapeTwice_ResumesAndCaptures()
    {
        _pause._UnhandledInput(EscapeEvent());
        _pause._UnhandledInput(EscapeEvent());

        AssertBool(_shop.GetTree().Paused).IsFalse();
        AssertBool(_menu.Visible).IsFalse();
        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Captured);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PcOpenClose_LeavesCursorCaptured()
    {
        _terminal.Interact();
        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Visible);

        _terminal.Close();

        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Captured);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EscapeWithPcOpen_ClosesOnlyThePc()
    {
        _terminal.Interact();

        _pause._UnhandledInput(EscapeEvent());

        AssertBool(_shop.GetTree().Paused).IsFalse();
        AssertBool(_terminal.IsOpen).IsTrue();

        _ui._UnhandledInput(EscapeEvent());

        AssertBool(_terminal.IsOpen).IsFalse();
        AssertBool(_shop.GetTree().Paused).IsFalse();
        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Captured);
    }

    private static InputEventAction EscapeEvent() => new() { Action = "ui_cancel", Pressed = true };
}
