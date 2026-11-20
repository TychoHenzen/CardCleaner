using Godot;

namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>
///     The space inside a cardboard box (0.80 x 0.669 x 0.86 m outside, 8 mm walls), in the box's own frame: its origin
///     is the middle of the box's base, so the inside floor lies <see cref="Floor" /> above it. It is tall enough for
///     four stacks of 128 cards (<see cref="BoxPacker" />), the 512 cards a sealed box holds as booster boxes.
/// </summary>
public static class CartonSpace
{
    /// <summary>The inner size: between the walls, and from the floor up to the closed flaps.</summary>
    public static readonly Vector3 Interior = new(0.784f, 0.645f, 0.844f);

    /// <summary>Height of the inside floor above the box's origin.</summary>
    public const float Floor = 0.008f;

    /// <summary>
    ///     Height of the closed box's top above its origin: the floor, the inside, and the two flaps that close it.
    /// </summary>
    public const float OutsideHeight = 0.669f;

    /// <summary>A packer for the inside of a cardboard box.</summary>
    public static BoxPacker Packer() => new(Interior);

    /// <summary>
    ///     The point in the box's own frame where the middle of <paramref name="item" />'s cell lies: the packer's
    ///     frame starts at the back left corner of the inside floor.
    /// </summary>
    public static Vector3 CenterOf(PackedItem item) =>
        item.Center + new Vector3(-Interior.X / 2f, Floor, -Interior.Z / 2f);
}
