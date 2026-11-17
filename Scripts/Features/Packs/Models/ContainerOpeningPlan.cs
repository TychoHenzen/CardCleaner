using System;

namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>
///     The timeline of opening one container, in seconds from the moment it is opened. It falls to the surface below
///     it (<see cref="Settle" />), its seal opens (<see cref="Unseal" />), its items rise out one after another (each
///     released <see cref="ReleaseStep" /> after the one before and rising for <see cref="Rise" />), and then the empty
///     wrapper shrinks away (<see cref="Vanish" />).
/// </summary>
public sealed class ContainerOpeningPlan
{
    private const float Gravity = 9.8f;

    // ASSUMPTION (Low): a drop from higher than about 2.4 m is cut short, so opening never keeps the player waiting.
    private const float LongestFall = 0.7f;

    // Built only by For, which names every value.
    private ContainerOpeningPlan()
    {
    }

    public float Settle { get; private init; }

    public float Unseal { get; private init; }

    public float ReleaseStep { get; private init; }

    public float Rise { get; private init; }

    public float Vanish { get; private init; }

    public int Items { get; private init; }

    /// <summary>
    ///     The plan for a container of <paramref name="kind" /> holding <paramref name="items" /> that falls
    ///     <paramref name="fallHeight" /> metres.
    /// </summary>
    public static ContainerOpeningPlan For(CardContainerKind kind, float fallHeight, int items)
    {
        var settle = FallSeconds(fallHeight);
        return kind switch
        {
            CardContainerKind.Box => new ContainerOpeningPlan
            {
                Settle = settle, Unseal = 0.6f, ReleaseStep = 0.1f, Rise = 0.45f, Vanish = 0.3f, Items = items
            },
            CardContainerKind.Pack => new ContainerOpeningPlan
            {
                Settle = settle, Unseal = 0.4f, ReleaseStep = 0.08f, Rise = 0.35f, Vanish = 0.25f, Items = items
            },
            CardContainerKind.Booster => new ContainerOpeningPlan
            {
                Settle = settle, Unseal = 0.3f, ReleaseStep = 0.06f, Rise = 0.3f, Vanish = 0.2f, Items = items
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown container kind")
        };
    }

    /// <summary>
    ///     The seconds a free fall of <paramref name="height" /> metres takes, at most <see cref="LongestFall" />.
    /// </summary>
    public static float FallSeconds(float height) =>
        height <= 0f ? 0f : Math.Min(LongestFall, MathF.Sqrt(2f * height / Gravity));

    /// <summary>Smoothstep: starts and ends at rest.</summary>
    public static float Ease(float progress)
    {
        var t = Math.Clamp(progress, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>When the item at <paramref name="index" /> leaves the container.</summary>
    public float ReleaseAt(int index) => Settle + Unseal + index * ReleaseStep;

    /// <summary>When the last item has risen to its spot and the wrapper starts to shrink.</summary>
    public float VanishStart => Items == 0 ? Settle + Unseal : ReleaseAt(Items - 1) + Rise;

    public float Duration => VanishStart + Vanish;

    public float SettleProgress(float time) => Fraction(time, 0f, Settle);

    public float UnsealProgress(float time) => Fraction(time, Settle, Unseal);

    public float RiseProgress(int index, float time) => Fraction(time, ReleaseAt(index), Rise);

    public float VanishProgress(float time) => Fraction(time, VanishStart, Vanish);

    private static float Fraction(float time, float start, float length)
    {
        if (length <= 0f)
            return time >= start ? 1f : 0f;

        return Math.Clamp((time - start) / length, 0f, 1f);
    }
}
