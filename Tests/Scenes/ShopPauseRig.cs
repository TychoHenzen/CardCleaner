using System.Threading.Tasks;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Tests.TestUtilities.Helpers;
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

    public static Task SettleAsync() => BoundedWait.SyncPhysicsFrames(2);

    public static void Teardown()
    {
        (Engine.GetMainLoop() as SceneTree)!.Paused = false;
        TestServices.Reset();
    }
}
