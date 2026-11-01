using System.Threading.Tasks;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Shared setup for the shop pause suites: the shop scene with its services
/// handed over, the Escape event, and the teardown that unpauses the tree.
/// </summary>
public static class ShopPauseRig
{
    public static InputEventAction EscapeEvent() =>
        new() { Action = "ui_cancel", Pressed = true };

    public static async Task SettleAsync()
    {
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    public static void Teardown()
    {
        (Engine.GetMainLoop() as SceneTree)!.Paused = false;
        TestServices.Reset();
    }
}
