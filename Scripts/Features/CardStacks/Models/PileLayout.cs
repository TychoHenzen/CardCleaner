using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.CardStacks.Models;

/// <summary>
///     Where each card of a pile lies, in the pile's frame (origin at the middle of its base). Card number
///     <c>index</c>, counted in join order from 0, lies on the dome of a pile of <c>index + 1</c> cards (the dome the
///     pile had once that card was in it), at a spot and turn drawn from the pile seed and the index alone. A pose
///     therefore never depends on any later card: the dome only grows, so newer cards lie on top and older ones sink
///     under them into the mound, and adding or taking cards moves no other card.
/// </summary>
public static class PileLayout
{
    // A card lies this much flatter than the dome under it, so the cards at the rim do not stand on edge.
    private const float FlattenTowardUp = 0.6f;
    private const float MostTiltRadians = 0.35f;

    // Every card lies a hair above its dome, and while the dome keeps its smallest size each newer card lies a little
    // higher than the one before, so overlapping cards never flicker and the newest lies on top.
    private const float CardLift = 0.002f;
    private const float LiftPerCard = 0.0005f;
    private const int MostLiftedCards = 16;

    private const ulong Golden = 0x9E3779B97F4A7C15UL;

    /// <summary>The pose of card <paramref name="index" /> of a pile laid with <paramref name="seed" />.</summary>
    public static Transform3D PoseOf(int seed, int index)
    {
        var key = Key(seed, index);
        var radius = CardBundleRules.PileRadius(index + 1);
        var height = CardBundleRules.PileHeight(index + 1);

        var angle = Draw(key, 0) * MathF.Tau;
        var reach = MathF.Sqrt(Draw(key, 1));
        var x = reach * radius * MathF.Cos(angle);
        var z = reach * radius * MathF.Sin(angle);
        var y = height * MathF.Sqrt(Math.Max(0f, 1f - reach * reach));

        var normal = new Vector3(x / (radius * radius), y / (height * height), z / (radius * radius)).Normalized();
        var up = (normal + Vector3.Up * FlattenTowardUp).Normalized();
        var side = up.Cross(Vector3.Forward).Normalized();
        var basis = new Basis(side, up, side.Cross(up));
        basis = new Basis(up, Draw(key, 2) * MathF.Tau) * basis;
        var tiltAxis = new Vector3(Draw(key, 3) - 0.5f, 0f, Draw(key, 4) - 0.5f);
        if (tiltAxis.LengthSquared() > 1e-6f)
            basis = new Basis(tiltAxis.Normalized(), (Draw(key, 5) - 0.5f) * MostTiltRadians) * basis;

        var lift = CardLift + LiftPerCard * Math.Min(index, MostLiftedCards);
        return new Transform3D(basis.Orthonormalized(), new Vector3(x, y, z) + up * lift);
    }

    /// <summary>
    ///     A pile seed from the cards a pile starts with: the same cards always start the same heap, so no seed is
    ///     drawn from a shared random source.
    /// </summary>
    public static int SeedFor(IReadOnlyList<int?> lookSeeds)
    {
        var seed = lookSeeds.Count;
        foreach (var look in lookSeeds)
            seed = unchecked(seed * 31 + (look ?? 0));
        return seed;
    }

    // One card's key: the pile seed and the index, mixed, so neighbouring cards share no draws.
    private static ulong Key(int seed, int index) => Mix(((ulong)(uint)seed << 32) | (uint)index);

    // The k-th draw for one card, in [0, 1), from the card's key alone, so no draw depends on another card's.
    private static float Draw(ulong key, int k) =>
        (Mix(unchecked(key + (ulong)(k + 1) * Golden)) >> 40) / (float)(1UL << 24);

    // The SplitMix64 finaliser.
    private static ulong Mix(ulong z)
    {
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        return z ^ (z >> 31);
    }
}
