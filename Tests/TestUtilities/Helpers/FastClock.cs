using System;
using Godot;

namespace CardCleaner.Tests.TestUtilities.Helpers;

/// <summary>
///     Runs game time <c>factor</c> times faster than real time while every physics step stays the length the game
///     uses. Godot passes <c>physics_step * time_scale</c> to the physics server and to every physics callback, and
///     runs <c>PhysicsTicksPerSecond</c> steps per real second, so raising both by the same factor keeps each step at
///     1/60 s of game time (Jolt, the card slop and the rest thresholds behave as in the game) while the factor times
///     as many steps run per real second. Process frames keep their real-time rhythm, which the gdUnit bridge needs
///     (a global <c>--fixed-fps</c> drops the frame sleep and lost suite starts on every run). Dispose restores both.
/// </summary>
public sealed class FastClock : IDisposable
{
    // ASSUMPTION: sixteen times real time keeps a shop scene's physics within a headless frame on a CI runner; a frame
    // that runs late only slows game time back down, it does not change any step. One process frame then spans about
    // 0.11 s of game time (16 x the 6.9 ms headless frame), so a test that must see each stage of an animation passes
    // a smaller factor. 32 was barely faster than 16 locally (47 s against 52 s over the clocked suites) and 64 was
    // slower, with a restocker case missing the walk it waits to interrupt.
    public const int DefaultFactor = 16;

    private readonly int _ticks;
    private readonly double _scale;
    private readonly int _maxSteps;
    private bool _disposed;

    private FastClock(int factor)
    {
        _ticks = Engine.PhysicsTicksPerSecond;
        _scale = Engine.TimeScale;
        _maxSteps = Engine.MaxPhysicsStepsPerFrame;
        Engine.PhysicsTicksPerSecond = _ticks * factor;
        Engine.TimeScale = _scale * factor;
        Engine.MaxPhysicsStepsPerFrame = _maxSteps * factor;
    }

    public static FastClock Start(int factor = DefaultFactor) => new(factor);

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Engine.PhysicsTicksPerSecond = _ticks;
        Engine.TimeScale = _scale;
        Engine.MaxPhysicsStepsPerFrame = _maxSteps;
    }
}
