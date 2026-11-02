using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Pause.Components;
using CardCleaner.Scripts.Features.Pause.Controllers;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The shop scene's mouse and Escape behaviour: play captures the mouse,
/// Escape pauses behind the menu, and Resume or Escape unpauses. The PC
/// screen cases live in <see cref="ShopPausePcTest"/>. Every case is
/// synchronous after Setup, so no case awaits while the tree is paused.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopPauseTest
{
    private Node3D _shop = null!;
    private PauseController _pause = null!;
    private CursorOwner _cursor = null!;
    private Control _menu = null!;
    private Button _resumeButton = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = ShopOrderingRig.LoadShopWithServices();
        _pause = _shop.GetNode<PauseController>("Pause");
        _cursor = _shop.GetNode<CursorOwner>("Services/CursorOwner");
        _menu = _shop.GetNode<Control>("Pause/PauseMenu/Panel");
        _resumeButton =
            _shop.GetNode<Button>("Pause/PauseMenu/Panel/ResumeButton");

        AddNode(_shop);
        await ShopPauseRig.SettleAsync();
    }

    [AfterTest]
    public static void Teardown() => ShopPauseRig.Teardown();

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
        _pause._UnhandledInput(ShopPauseRig.EscapeEvent());

        AssertBool(_shop.GetTree().Paused).IsTrue();
        AssertBool(_menu.Visible).IsTrue();
        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Visible);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EscapeThenResumeButton_ResumesAndCaptures()
    {
        _pause._UnhandledInput(ShopPauseRig.EscapeEvent());

        _resumeButton.EmitSignal(BaseButton.SignalName.Pressed);

        AssertBool(_shop.GetTree().Paused).IsFalse();
        AssertBool(_menu.Visible).IsFalse();
        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Captured);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EscapeTwice_ResumesAndCaptures()
    {
        _pause._UnhandledInput(ShopPauseRig.EscapeEvent());
        _pause._UnhandledInput(ShopPauseRig.EscapeEvent());

        AssertBool(_shop.GetTree().Paused).IsFalse();
        AssertBool(_menu.Visible).IsFalse();
        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Captured);
    }
}
