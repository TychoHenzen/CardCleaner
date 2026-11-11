using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// 3D physics runs on the main thread. Godot 4.7.1 deadlocks the whole process when physics runs on its own thread:
/// a main-thread physics call during the physics frame can find the server's command queue mid-flush, queue a sync
/// and wait, while the physics thread finishes the flush and then refuses to flush again until the frame ends
/// (godotengine/godot issue 123654, fixed by pull request 124990, not yet in a 4.7 stable release). Re-enable the
/// setting, and drop this test, only on an engine version that contains that fix.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PhysicsThreadSettingTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void PhysicsDoesNotRunOnASeparateThread()
    {
        var separate = ProjectSettings.GetSetting("physics/3d/run_on_separate_thread").AsBool();

        Assertions.AssertBool(separate)
            .OverrideFailureMessage(
                "physics/3d/run_on_separate_thread is on; Godot 4.7.1 deadlocks with it (godot#123654)")
            .IsFalse();
    }
}
