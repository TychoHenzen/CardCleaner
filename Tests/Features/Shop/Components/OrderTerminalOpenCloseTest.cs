using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>Opening, closing and cursor claims of the terminal, its escape key, highlight and layer.</summary>
[TestSuite]
[RequireGodotRuntime]
public class OrderTerminalOpenCloseTest
{
    private const int StartingBalance = OrderTerminalFixture.StartingBalance;

    private OrderTerminalFixture _f = null!;

    [BeforeTest]
    public void Setup()
    {
        _f = new OrderTerminalFixture();
        AddNode(_f.World);
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void InteractOpensTheUiWithTheCurrentBalanceAndBlocksFurtherInteraction()
    {
        AssertBool(_f.Terminal.CanInteract).IsTrue();

        _f.Terminal.Interact();

        AssertBool(_f.Ui.Visible).IsTrue();
        AssertBool(_f.Terminal.IsOpen).IsTrue();
        AssertBool(_f.Terminal.CanInteract).IsFalse();
        AssertThat(_f.Ui.BalanceText).IsEqual($"Balance: {StartingBalance}");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CloseHidesTheUiAndAllowsInteractionAgain()
    {
        _f.Terminal.Interact();

        _f.Terminal.Close();

        AssertBool(_f.Ui.Visible).IsFalse();
        AssertBool(_f.Terminal.IsOpen).IsFalse();
        AssertBool(_f.Terminal.CanInteract).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CloseButtonClosesTheTerminal()
    {
        _f.Terminal.Interact();

        _f.Ui.PressClose();

        AssertBool(_f.Terminal.IsOpen).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OpenThenClose_LeavesCursorCaptured()
    {
        _f.Terminal.Interact();

        AssertThat(_f.Cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Visible);
        AssertBool(_f.Cursor.Claims.HasClaims).IsTrue();

        _f.Terminal.Close();

        AssertThat(_f.Cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Captured);
        AssertBool(_f.Cursor.Claims.HasClaims).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CloseTwiceAndOpenTwice_ClaimsAndReleasesOnce()
    {
        _f.Terminal.Open();
        _f.Terminal.Open();
        _f.Terminal.Close();

        AssertBool(_f.Cursor.Claims.HasClaims).IsFalse();

        _f.Terminal.Close();

        AssertBool(_f.Cursor.Claims.HasClaims).IsFalse();
        AssertThat(_f.Cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Captured);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FreedWhileOpen_ReleasesClaim()
    {
        _f.Terminal.Interact();

        _f.Terminal.Free();

        AssertBool(_f.Cursor.Claims.HasClaims).IsFalse();
        AssertThat(_f.Cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Captured);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EscapeClosesTheOpenTerminal()
    {
        _f.Terminal.Interact();

        _f.Ui._UnhandledInput(new InputEventAction { Action = "ui_cancel", Pressed = true });

        AssertBool(_f.Terminal.IsOpen).IsFalse();
        AssertBool(_f.Ui.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EscapeDoesNothingWhileTheTerminalIsClosed()
    {
        _f.Ui._UnhandledInput(new InputEventAction { Action = "ui_cancel", Pressed = true });

        AssertBool(_f.Terminal.IsOpen).IsFalse();
        AssertBool(_f.Terminal.CanInteract).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void HighlightShowsAndClearsTheHighlightMesh()
    {
        var mesh = new MeshInstance3D { Visible = false };
        _f.Terminal.AddChild(mesh);
        _f.Terminal.HighlightMesh = mesh;

        _f.Terminal.Highlight();
        AssertBool(mesh.Visible).IsTrue();

        _f.Terminal.ClearHighlight();
        AssertBool(mesh.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TerminalSitsOnTheInteractableLayerSeenByThePlayerRay()
    {
        AssertBool((_f.Terminal.CollisionLayer & 4) != 0).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TerminalWithoutDesktopKeepsTheOverlayBehaviour()
    {
        var playerCamera = AddPlayerCamera();

        _f.Terminal.Interact();

        AssertBool(_f.Ui.Visible).IsTrue();
        AssertBool(playerCamera.Current).IsTrue();
    }

    private Camera3D AddPlayerCamera()
    {
        var camera = new Camera3D();
        _f.World.AddChild(camera);
        camera.Current = true;
        return camera;
    }
}
