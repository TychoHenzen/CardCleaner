using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>The shape of an item that can be packed into a cardboard box.</summary>
public enum ItemShape
{
    Card,
    Booster,
    Pack
}

/// <summary>
///     How an item lies in a box. <c>Flat</c> is the item as it is built (a card or booster lying on its face, a
///     booster box standing on its base); <c>Turned</c> is that turned a quarter about the vertical. <c>OnEnd</c> stands
///     the item on its short end, and <c>OnEndTurned</c> is that turned a quarter about the vertical.
/// </summary>
public enum ItemPose
{
    Flat,
    Turned,
    OnEnd,
    OnEndTurned
}

/// <summary>The size and allowed poses of each <see cref="ItemShape" />, in metres along x, y (up) and z.</summary>
public static class ItemShapes
{
    private static readonly ItemPose[] LyingPoses = [ItemPose.Flat, ItemPose.Turned];

    // A booster box stands on end first: eight fit a cardboard box that way (two across, four deep).
    private static readonly ItemPose[] BoxPoses = [ItemPose.OnEnd, ItemPose.OnEndTurned, ItemPose.Flat, ItemPose.Turned];

    /// <summary>The size of <paramref name="shape" /> as it is built, before any pose.</summary>
    public static Vector3 Size(ItemShape shape)
    {
        return shape switch
        {
            ItemShape.Card => new Vector3(0.32f, 0.005f, 0.45f),
            ItemShape.Booster => new Vector3(0.35f, 0.045f, 0.53f),
            ItemShape.Pack => new Vector3(0.38f, 0.20f, 0.55f),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown item shape")
        };
    }

    /// <summary>The poses <paramref name="shape" /> may take in a box, the preferred one first.</summary>
    public static IReadOnlyList<ItemPose> Poses(ItemShape shape) => shape == ItemShape.Pack ? BoxPoses : LyingPoses;

    /// <summary>
    ///     True for a flat item (a card or booster) that a packer stacks straight on top of the same item put in just
    ///     before it, so a run of them forms one tidy stack.
    /// </summary>
    public static bool Stacks(ItemShape shape) => shape != ItemShape.Pack;

    /// <summary>The size of an item of <paramref name="size" /> once it takes <paramref name="pose" />.</summary>
    public static Vector3 SizeIn(Vector3 size, ItemPose pose)
    {
        return pose switch
        {
            ItemPose.Flat => size,
            ItemPose.Turned => new Vector3(size.Z, size.Y, size.X),
            ItemPose.OnEnd => new Vector3(size.X, size.Z, size.Y),
            ItemPose.OnEndTurned => new Vector3(size.Y, size.Z, size.X),
            _ => throw new ArgumentOutOfRangeException(nameof(pose), pose, "Unknown item pose")
        };
    }
}
