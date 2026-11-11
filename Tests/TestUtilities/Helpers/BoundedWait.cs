using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

namespace CardCleaner.Tests.TestUtilities.Helpers;

// Waits that a headless test cannot hang on. The tick is the process frame and the bound is wall-clock time: a wait
// counted in physics frames never ends when physics-frame signals stop while the main loop runs.
public static class BoundedWait
{
    public const double DefaultMaxSeconds = 45;

    // ASSUMPTION: a headless physics run ticks at least this often, so the extra allowance per requested frame
    // covers a normal run and only a stalled one reaches the bound.
    private const double MinPhysicsTicksPerSecond = 30;

    // Returns null when done() became true, otherwise a message naming the wait and the physics frames that passed.
    public static async Task<string?> Until(Func<bool> done, string what, double maxSeconds = DefaultMaxSeconds)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var clock = Stopwatch.StartNew();
        var firstTick = Engine.GetPhysicsFrames();
        while (!done())
        {
            if (clock.Elapsed.TotalSeconds > maxSeconds)
            {
                var passed = Engine.GetPhysicsFrames() - firstTick;
                return $"Timed out after {clock.Elapsed.TotalSeconds:F1} s waiting for: {what}. " +
                       $"Physics frames passed: {passed}.";
            }

            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        return null;
    }

    // Returns null once count physics frames have passed, otherwise the timeout message from Until.
    public static async Task<string?> PhysicsFrames(int count, double maxSeconds = DefaultMaxSeconds)
    {
        var first = Engine.GetPhysicsFrames();
        return await Until(
            () => Engine.GetPhysicsFrames() - first >= (ulong)count,
            $"{count} physics frames",
            maxSeconds + count / MinPhysicsTicksPerSecond);
    }
}
