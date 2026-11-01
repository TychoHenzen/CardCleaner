using CardCleaner.Scripts.Features.Pause.Components;
using CardCleaner.Scripts.Features.Pause.Controllers;
using CardCleaner.Scripts.Features.Pause.Models;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Pause.Controllers;

[TestSuite]
[RequireGodotRuntime]
public class PauseControllerTest
{
    private Node _root = null!;
    private CursorOwner _cursor = null!;
    private PauseController _pause = null!;
    private Panel _panel = null!;
    private Button _resumeButton = null!;

    [BeforeTest]
    public void Setup()
    {
        TestServices.Reset();

        _root = new Node();
        _cursor = new CursorOwner();
        _root.AddChild(_cursor);
        TestServices.Container.RegisterSingleton<ICursorOwner>(_cursor);

        var menu = BuildMenu();
        _root.AddChild(menu);
        _pause = BuildController(menu);
        _root.AddChild(_pause);

        _panel = menu.GetNode<Panel>("Panel");
        _resumeButton = menu.GetNode<Button>("Panel/ResumeButton");

        AddNode(_root);
    }

    [AfterTest]
    public static void Teardown()
    {
        (Engine.GetMainLoop() as SceneTree)!.Paused = false;
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Escape_Pauses_ShowsMenu_CursorVisible()
    {
        _pause._UnhandledInput(EscapeEvent());

        AssertBool(_root.GetTree().Paused).IsTrue();
        AssertBool(_panel.Visible).IsTrue();
        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Visible);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Escape_Twice_Resumes_HidesMenu_CursorCaptured()
    {
        _pause._UnhandledInput(EscapeEvent());
        _pause._UnhandledInput(EscapeEvent());

        AssertBool(_root.GetTree().Paused).IsFalse();
        AssertBool(_panel.Visible).IsFalse();
        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Captured);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ResumeButton_Resumes_CursorCaptured()
    {
        _pause._UnhandledInput(EscapeEvent());

        _resumeButton.EmitSignal(BaseButton.SignalName.Pressed);

        AssertBool(_root.GetTree().Paused).IsFalse();
        AssertBool(_panel.Visible).IsFalse();
        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Captured);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EscapeWithForeignClaim_DoesNotPause()
    {
        _cursor.Claim(new object());

        _pause._UnhandledInput(EscapeEvent());

        AssertBool(_root.GetTree().Paused).IsFalse();
        AssertBool(_panel.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EscapeWhilePaused_ResumesEvenWithForeignClaim()
    {
        _pause._UnhandledInput(EscapeEvent());
        _cursor.Claim(new object());

        _pause._UnhandledInput(EscapeEvent());

        AssertBool(_root.GetTree().Paused).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void WithoutCursorService_StillPausesAndResumes()
    {
        // Setup registered ICursorOwner, so drop it and build a controller that has no cursor to resolve.
        TestServices.Reset();
        var menu = BuildMenu();
        var root = AddNode(new Node());
        root.AddChild(menu);
        var pause = BuildController(menu);
        root.AddChild(pause);

        pause._UnhandledInput(EscapeEvent());
        AssertBool(root.GetTree().Paused).IsTrue();

        pause._UnhandledInput(EscapeEvent());
        AssertBool(root.GetTree().Paused).IsFalse();
    }

    private static InputEventAction EscapeEvent() => new() { Action = "ui_cancel", Pressed = true };

    private static CanvasLayer BuildMenu()
    {
        var menu = new CanvasLayer();
        var panel = new Panel { Name = "Panel" };
        panel.AddChild(new Button { Name = "ResumeButton" });
        menu.AddChild(panel);
        return menu;
    }

    private static PauseController BuildController(CanvasLayer menu)
    {
        var pause = new PauseController { PauseMenuLayer = menu };
        return TestServices.Inject(pause);
    }
}
