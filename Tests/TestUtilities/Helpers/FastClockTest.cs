using System.Threading.Tasks;
using Godot;

namespace CardCleaner.Tests.TestUtilities.Helpers;

[TestSuite]
[RequireGodotRuntime]
public class FastClockTest
{
    private readonly SceneTree _tree = (SceneTree)Engine.GetMainLoop();

    // Pins the engine behaviour FastClock rests on: Godot 4.7.1 multiplies the physics step by the time scale, so the
    // step the game's physics sees stays 1/60 s when the tick rate and the time scale rise together.
    [TestCase]
    public async Task EveryPhysicsStepStaysTheGamesStepUnderAFastClock()
    {
        var probe = new Node();
        _tree.Root.AddChild(probe);
        var normal = 1.0 / Engine.PhysicsTicksPerSecond;
        try
        {
            using (FastClock.Start(4))
            {
                await _tree.ToSignal(_tree, SceneTree.SignalName.PhysicsFrame);
                await _tree.ToSignal(_tree, SceneTree.SignalName.PhysicsFrame);

                AssertThat(probe.GetPhysicsProcessDeltaTime()).IsEqualApprox(normal, 1e-9);
            }
        }
        finally
        {
            probe.Free();
        }
    }

    [TestCase]
    public static void DisposeRestoresTheTickRateTheTimeScaleAndTheStepCap()
    {
        var ticks = Engine.PhysicsTicksPerSecond;
        var scale = Engine.TimeScale;
        var steps = Engine.MaxPhysicsStepsPerFrame;

        using (FastClock.Start(4))
        {
            AssertThat(Engine.PhysicsTicksPerSecond).IsEqual(ticks * 4);
            AssertThat(Engine.TimeScale).IsEqual(scale * 4);
        }

        AssertThat(Engine.PhysicsTicksPerSecond).IsEqual(ticks);
        AssertThat(Engine.TimeScale).IsEqual(scale);
        AssertThat(Engine.MaxPhysicsStepsPerFrame).IsEqual(steps);
    }
}
