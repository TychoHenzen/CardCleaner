using System;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     The rules a walking person follows: which loop its speed asks for, which way it faces and how fast it turns
///     there. A yaw is a turn about the up axis in radians; yaw 0 faces -Z, the way Godot nodes look.
/// </summary>
public static class FigureMotion
{
    /// <summary>Below this speed (metres a second) a person stands; at or above it, it walks.</summary>
    public const float WalkingSpeed = 0.2f;

    /// <summary>The loop for a person moving at <paramref name="speed" /> metres a second.</summary>
    public static FigureClip LoopFor(float speed) => speed >= WalkingSpeed ? FigureClip.Walk : FigureClip.Idle;

    /// <summary>True for the clips that repeat until something else plays.</summary>
    public static bool Loops(FigureClip clip) => clip is FigureClip.Idle or FigureClip.Walk;

    /// <summary>The yaw that faces the flat direction (<paramref name="x" />, <paramref name="z" />).</summary>
    public static float YawToward(float x, float z) => MathF.Atan2(-x, -z);

    /// <summary>
    ///     Turns <paramref name="current" /> toward <paramref name="target" /> the short way round by at most
    ///     <paramref name="maxStep" /> radians, and returns the new yaw in [-pi, pi].
    /// </summary>
    public static float TurnToward(float current, float target, float maxStep)
    {
        var difference = Wrap(target - current);
        var step = Math.Clamp(difference, -maxStep, maxStep);
        return Wrap(current + step);
    }

    /// <summary>
    ///     The playback speed that keeps the feet of a walk clip, authored at <paramref name="clipSpeed" /> metres a
    ///     second, on the floor at <paramref name="speed" />. A clip with no authored speed plays at 1.
    /// </summary>
    public static float WalkPlayback(float speed, float clipSpeed) => clipSpeed > 0f ? speed / clipSpeed : 1f;

    private static float Wrap(float angle)
    {
        var wrapped = MathF.IEEERemainder(angle, MathF.Tau);
        return wrapped <= -MathF.PI ? wrapped + MathF.Tau : wrapped;
    }
}
