using System;
using Godot;

namespace CardCleaner.Scripts.Features.CardStacks.Models;

/// <summary>
///     The rules of a bundle of cards: when loose cards form a pile, when a hand may strap, and how big a bundle is for
///     its count. What a click takes is the transfer rule's (ItemTransfer).
/// </summary>
public static class CardBundleRules
{
    /// <summary>More loose cards than this lying close together form a pile.</summary>
    public const int MostLooseCards = 100;

    /// <summary>A deck holds at least this many cards; one card is just a card.</summary>
    public const int FewestToStrap = 2;

    /// <summary>Size of one card lying flat (x across, y thick, z long), as the card mesh is.</summary>
    public static readonly Vector3 CardSize = new(0.32f, 0.005f, 0.45f);

    // A pile's height is this share of its radius: a low mound, as cards slide off a steep one.
    private const float PileHeightRatio = 0.4f;

    // A heap of cards lying every which way takes this many times their own volume.
    private const float PileLooseness = 2f;

    // Never narrower than one card lying flat, so the cards on its top still rest on it.
    private const float SmallestPileRadius = 0.3f;

    /// <summary>The box a deck of <paramref name="count" /> cards fills: one card's face, the cards' height.</summary>
    public static Vector3 DeckSize(int count) =>
        new(CardSize.X, Math.Max(1, count) * CardSize.Y, CardSize.Z);

    /// <summary>
    ///     The radius of a pile of <paramref name="count" /> cards: a dome (half an ellipsoid of height
    ///     <see cref="PileHeight" />) that holds the cards' volume taken loosely, so it grows with the cube root of the
    ///     count.
    /// </summary>
    public static float PileRadius(int count)
    {
        var volume = Math.Max(1, count) * CardSize.X * CardSize.Y * CardSize.Z * PileLooseness;
        // A half ellipsoid of radius r and height k r holds (2/3) pi k r^3.
        var radius = MathF.Cbrt(volume / (2f / 3f * MathF.PI * PileHeightRatio));
        return Math.Max(SmallestPileRadius, radius);
    }

    /// <summary>The height of a pile of <paramref name="count" /> cards.</summary>
    public static float PileHeight(int count) => PileRadius(count) * PileHeightRatio;
}
