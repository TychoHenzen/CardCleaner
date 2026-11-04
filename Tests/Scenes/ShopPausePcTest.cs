using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Pause.Components;
using CardCleaner.Scripts.Features.Pause.Controllers;
using CardCleaner.Scripts.Features.Shop.Components;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The PC screen claims the mouse in the shop scene without letting Escape
/// pause the game. Every case is synchronous after Setup, so no case awaits
/// while the tree is paused.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopPausePcTest
{
    private Node3D _shop = null!;
    private PauseController _pause = null!;
    private CursorOwner _cursor = null!;
    private ScreenTerminal _terminal = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = ShopOrderingRig.LoadShopWithServices();
        _pause = _shop.GetNode<PauseController>("Pause");
        _cursor = _shop.GetNode<CursorOwner>("Services/CursorOwner");
        _terminal = _shop.GetNode<ScreenTerminal>(
            "World/Markers/PcLocation/PcTerminal");

        AddNode(_shop);
        await ShopPauseRig.SettleAsync();
    }

    [AfterTest]
    public static void Teardown() => ShopPauseRig.Teardown();

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

        _pause._UnhandledInput(ShopPauseRig.EscapeEvent());

        AssertBool(_shop.GetTree().Paused).IsFalse();
        AssertBool(_terminal.IsOpen).IsTrue();

        // With a desktop attached, the terminal owns Escape: it closes the PC and its desktop together.
        _terminal._UnhandledInput(ShopPauseRig.EscapeEvent());

        AssertBool(_terminal.IsOpen).IsFalse();
        AssertBool(_shop.GetTree().Paused).IsFalse();
        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Captured);
    }
}
