using System;

namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>
///     The timeline of opening a booster in the player's hand, in seconds from the moment it is opened. The booster
///     comes to the middle of the view (<see cref="Present" />), its strip tears off (<see cref="Tear" />), its cards
///     leave it one after another (each <see cref="ReleaseStep" /> after the one before) and each spreads into the
///     fan in front of the player over <see cref="Rise" />, while the empty wrapper shrinks away
///     (<see cref="Vanish" />) after the last card has left it. The fan stays in view for <see cref="Show" />, then
///     slides into the hand over <see cref="IntoHand" />.
/// </summary>
public sealed class BoosterRevealPlan
{
    public const float Present = 0.2f;
    public const float Tear = 0.2f;
    public const float ReleaseStep = 0.05f;
    public const float Rise = 0.3f;
    public const float Vanish = 0.2f;
    public const float Show = 0.35f;
    public const float IntoHand = 0.3f;

    public BoosterRevealPlan(int cards)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cards);
        Cards = cards;
    }

    /// <summary>How many cards the booster opens into.</summary>
    public int Cards { get; }

    /// <summary>When the card at <paramref name="index" /> leaves the booster.</summary>
    public float ReleaseAt(int index) => Present + Tear + index * ReleaseStep;

    /// <summary>When the last card has left the booster and the wrapper starts to shrink.</summary>
    public float VanishStart => Cards == 0 ? Present + Tear : ReleaseAt(Cards - 1);

    /// <summary>When the fan starts to slide into the hand: once every card has spread out and been shown.</summary>
    public float IntoHandStart => Cards == 0 ? Present + Tear : ReleaseAt(Cards - 1) + Rise + Show;

    public float Duration => Math.Max(IntoHandStart + IntoHand, VanishStart + Vanish);

    public float PresentProgress(float time) => ContainerOpeningPlan.Fraction(time, 0f, Present);

    public float TearProgress(float time) => ContainerOpeningPlan.Fraction(time, Present, Tear);

    public float RiseProgress(int index, float time) => ContainerOpeningPlan.Fraction(time, ReleaseAt(index), Rise);

    public float VanishProgress(float time) => ContainerOpeningPlan.Fraction(time, VanishStart, Vanish);

    /// <summary>
    ///     How far the fan has slid into the hand, <paramref name="sinceStart" /> seconds after it started to. The
    ///     slide is timed from when it really began, which a slow frame may put after <see cref="IntoHandStart" />.
    /// </summary>
    public static float IntoHandProgress(float sinceStart) => ContainerOpeningPlan.Fraction(sinceStart, 0f, IntoHand);
}
