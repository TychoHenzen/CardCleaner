using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>The size and allowed poses of each <see cref="ItemShape" />, in metres along x, y (up) and z.</summary>
public static class ItemShapes
{
    private static readonly ItemPose[] LyingPoses = [ItemPose.Flat, ItemPose.Turned];

    // A booster box stands on end first: eight fit a cardboard box that way (two across, four deep).
    private static readonly ItemPose[] BoxPoses =
        [ItemPose.OnEnd, ItemPose.OnEndTurned, ItemPose.Flat, ItemPose.Turned];

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
    ///     True for a booster, which a packer stacks straight on top of the booster put in just before it, so a run of
    ///     them forms one tidy stack. Cards have stack places of their own (<see cref="BoxPacker.CardStacks" />).
    /// </summary>
    public static bool Stacks(ItemShape shape) => shape == ItemShape.Booster;

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
