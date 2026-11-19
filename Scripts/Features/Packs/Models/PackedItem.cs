using Godot;

namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>
///     Where one item lies in a box: its shape, its pose, and the cell it fills, given by the cell's low corner and
///     size in the box's inner frame (the floor at y 0, the back left corner at x 0 and z 0). A cell is the posed
///     item plus <see cref="BoxPacker.Clearance" /> on each side across the floor, so neighbours never touch.
/// </summary>
public readonly record struct PackedItem(ItemShape Shape, ItemPose Pose, Vector3 Min, Vector3 Size)
{
    public Vector3 Max => Min + Size;

    public Vector3 Center => Min + Size / 2f;
}
