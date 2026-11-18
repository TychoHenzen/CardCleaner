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
    // ASSUMPTION: four times real time keeps a shop scene's physics within a headless frame on a CI runner; a frame
    // that runs late only slows game time back down, it does not change any step.
    public const int DefaultFactor = 4;

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
