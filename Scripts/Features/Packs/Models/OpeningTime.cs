using System;

namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>
///     Timing helpers shared by everything on a container that moves over time: a booster opening in the hand
///     (<see cref="BoosterRevealPlan" />) and the parts of a wrapper that swing or tear open.
/// </summary>
public static class OpeningTime
{
    /// <summary>Smoothstep: starts and ends at rest.</summary>
    public static float Ease(float progress)
    {
        var t = Math.Clamp(progress, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    ///     How far <paramref name="time" /> is through the span of <paramref name="length" /> seconds that begins at
    ///     <paramref name="start" />, from 0 to 1. A span of no length is done once it has begun.
    /// </summary>
    public static float Fraction(float time, float start, float length)
    {
        if (length <= 0f)
            return time >= start ? 1f : 0f;

        return Math.Clamp((time - start) / length, 0f, 1f);
    }
}
