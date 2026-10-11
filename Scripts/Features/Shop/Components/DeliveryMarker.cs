using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A delivery area for a room with a ceiling. Each footprint shape of a delivered item takes the slot cells it
///     covers from above, and a shape past the last row takes none. An item goes on the first free ground slot where
///     its shapes stay inside <see cref="FloorClearance" /> (when one is set) and overlap no shape of an item in the
///     same height range. Otherwise it goes on the lowest stack that still fits under <see cref="MaxStackHeight" />,
///     and is refused when none does. The footprint is the solid shapes plus the triggers under a node in
///     <see cref="FootprintGroup" />; other triggers take no space.
/// </summary>
public partial class DeliveryMarker : Marker3D
{
    /// <summary>Vertical gap left between a new item and the top of the item it is stacked on.</summary>
    public const float StackGap = 0.02f;

    /// <summary>Smallest height an item counts for, so a flat or shapeless item still occupies a stack level.</summary>
    public const float MinItemHeight = 0.1f;

    /// <summary>
    ///     Group of a node whose trigger areas count toward an item's footprint. Triggers take up no space otherwise.
    /// </summary>
    public const string FootprintGroup = "delivery_footprint";

    // Overlaps and height gaps shorter than this are touching, not overlapping, so a shared face refuses nothing.
    private const float OverlapEpsilon = 0.001f;

    private readonly List<Node3D> _delivered = [];

    /// <summary>
    ///     Clearance above the marker, in metres, that stacked deliveries must stay under. Set it to the room's
    ///     ceiling underside measured from the marker.
    /// </summary>
    [Export]
    public float MaxStackHeight { get; set; } = 3.9f;

    /// <summary>
    ///     Floor area, as X and Z offsets in metres from this marker, that every shape of a delivered item must stay
    ///     inside. An empty rectangle sets no limit.
    /// </summary>
    [Export]
    public Rect2 FloorClearance { get; set; }

    /// <summary>
    ///     Where the next item should go: the first free ground slot the item fits on, otherwise the lowest stack top
    ///     that still leaves room for <paramref name="item" />. Null when the area has no clearance left. Items that no
    ///     longer touch any cell are forgotten, so only live occupants are ever retained.
    /// </summary>
    /// <param name="slotCount">How many slots the area has.</param>
    /// <param name="slotOffset">Offset of a slot's centre from this marker.</param>
    /// <param name="cellSize">Width (X) and depth (Z) of one slot's cell.</param>
    /// <param name="item">
    ///     The item about to be delivered, not yet placed in the world. Its origin is taken to be at its feet.
    /// </param>
    public Placement? FindPlacement(int slotCount, Func<int, Vector3> slotOffset, Vector2 cellSize, Node3D item)
    {
        var stackTop = new float[slotCount];
        var occupants = new List<Occupant>();
        foreach (var delivered in _delivered.ToArray())
        {
            if (Absorb(delivered, stackTop, slotOffset, cellSize) is { } occupant)
                occupants.Add(occupant);
            else
                _delivered.Remove(delivered);
        }

        var local = LocalShapeBounds(item);
        var localBounds = Union(local);
        for (var slot = 0; slot < slotCount; slot++)
        {
            if (stackTop[slot] <= 0f && Fits(local, localBounds, slotOffset(slot), occupants))
                return new Placement(slot, 0f);
        }

        var height = Mathf.Max(localBounds.End.Y, MinItemHeight);
        foreach (var slot in Enumerable.Range(0, slotCount).Where(s => stackTop[s] > 0f).OrderBy(s => stackTop[s]))
        {
            var lift = stackTop[slot] + StackGap;
            if (lift + height > MaxStackHeight)
                return null;
            if (Fits(local, localBounds, slotOffset(slot) + Vector3.Up * lift, occupants))
                return new Placement(slot, lift);
        }

        return null;
    }

    /// <summary>Starts treating <paramref name="item" /> as an occupant of this area.</summary>
    public void Track(Node3D item) => _delivered.Add(item);

    /// <summary>
    ///     Raises the stack top of every cell that one of the occupant's shapes touches, and describes the occupant for
    ///     the incoming-shape check. Returns null when the occupant is gone or touches no cell any more.
    /// </summary>
    private Occupant? Absorb(Node3D occupant, float[] stackTop, Func<int, Vector3> slotOffset, Vector2 cellSize)
    {
        if (!IsLive(occupant))
            return null;

        var shapes = WorldShapeBounds(occupant);
        var touchesAnyCell = false;
        foreach (var box in shapes)
        {
            var footprint = new Rect2(box.Position.X, box.Position.Z, box.Size.X, box.Size.Z);
            var top = Mathf.Max(box.End.Y - GlobalPosition.Y, MinItemHeight);
            for (var slot = 0; slot < stackTop.Length; slot++)
            {
                if (!TouchesCell(footprint, GlobalPosition + slotOffset(slot), cellSize))
                    continue;
                stackTop[slot] = Mathf.Max(stackTop[slot], top);
                touchesAnyCell = true;
            }
        }

        if (!touchesAnyCell)
            return null;

        // ASSUMPTION: the occupant's height range is taken from its footprint shapes only, so a trigger that does not
        // count toward the footprint does not raise the item for the height check either.
        var lowest = shapes.Min(box => box.Position.Y) - GlobalPosition.Y;
        var highest = shapes.Max(box => box.End.Y) - GlobalPosition.Y;
        return new Occupant(shapes, lowest, highest);
    }

    private static bool IsLive(Node3D item) =>
        GodotObject.IsInstanceValid(item) && item.IsInsideTree() && !item.IsQueuedForDeletion();

    /// <summary>
    ///     Whether a footprint, seen from above, overlaps the slot cell centred on <paramref name="cellCentre" />.
    /// </summary>
    private static bool TouchesCell(Rect2 footprint, Vector3 cellCentre, Vector2 cellSize)
    {
        var left = cellCentre.X - cellSize.X / 2f;
        var back = cellCentre.Z - cellSize.Y / 2f;
        return footprint.Position.X < left + cellSize.X && footprint.End.X > left
               && footprint.Position.Y < back + cellSize.Y && footprint.End.Y > back;
    }

    /// <summary>
    ///     Whether an item with the given local shapes can stand at <paramref name="offset" /> from this marker. It is
    ///     refused when one of its shapes overlaps, seen from above, a shape of an occupant whose height range overlaps
    ///     the item's own. Heights are compared for each item as a whole, not shape by shape.
    /// </summary>
    private bool Fits(Aabb[] localShapes, Aabb localBounds, Vector3 offset, IReadOnlyList<Occupant> occupants)
    {
        var shapes = localShapes.Select(box => new Aabb(box.Position + GlobalPosition + offset, box.Size)).ToArray();
        if (FloorClearance.HasArea() && !shapes.All(WithinClearance))
            return false;

        var lowest = localBounds.Position.Y + offset.Y;
        var highest = localBounds.End.Y + offset.Y;
        foreach (var occupant in occupants)
        {
            var heightsOverlap = lowest < occupant.Top - OverlapEpsilon && occupant.Bottom < highest - OverlapEpsilon;
            if (heightsOverlap && shapes.Any(shape => occupant.Shapes.Any(other => ShapesOverlap(shape, other))))
                return false;
        }

        return true;
    }

    /// <summary>Whether a world-space box stays inside <see cref="FloorClearance" />, to within the epsilon.</summary>
    private bool WithinClearance(Aabb shape)
    {
        var area = new Rect2(
            GlobalPosition.X + FloorClearance.Position.X,
            GlobalPosition.Z + FloorClearance.Position.Y,
            FloorClearance.Size.X,
            FloorClearance.Size.Y);
        return shape.Position.X >= area.Position.X - OverlapEpsilon && shape.End.X <= area.End.X + OverlapEpsilon
               && shape.Position.Z >= area.Position.Y - OverlapEpsilon && shape.End.Z <= area.End.Y + OverlapEpsilon;
    }

    /// <summary>Whether two boxes overlap seen from above, by more than the epsilon on both X and Z.</summary>
    private static bool ShapesOverlap(Aabb a, Aabb b) =>
        Overlap1D(a.Position.X, a.End.X, b.Position.X, b.End.X)
        && Overlap1D(a.Position.Z, a.End.Z, b.Position.Z, b.End.Z);

    // ASSUMPTION: "overlap" means the shared span is longer than the epsilon. Measuring the spans shrunk by the epsilon
    // would count a point inside a box as overlapping it, and the zero-size boxes of a shapeless item must never do so.
    private static bool Overlap1D(float aMin, float aMax, float bMin, float bMax) =>
        Mathf.Min(aMax, bMax) - Mathf.Max(aMin, bMin) > OverlapEpsilon;

    /// <summary>
    ///     World bounds of each footprint shape under the item (see <see cref="FootprintShapes" />), one box per shape.
    ///     An item without any shape counts as a single point at its origin.
    /// </summary>
    private static Aabb[] WorldShapeBounds(Node3D item)
    {
        var boxes = FootprintShapes(item)
            .Select(collider => collider.GlobalTransform * ShapeBounds(collider.Shape))
            .ToArray();
        if (boxes.Length == 0)
            return [new Aabb(item.GlobalPosition, Vector3.Zero)];
        return boxes;
    }

    /// <summary>
    ///     Bounds of each footprint shape in the item's own space, usable before it enters the tree. An item without
    ///     any shape has none.
    /// </summary>
    private static Aabb[] LocalShapeBounds(Node3D item) =>
        FootprintShapes(item)
            .Select(collider => ToItem(collider, item) * ShapeBounds(collider.Shape))
            .ToArray();

    /// <summary>The smallest box holding every box given, or a zero box at the origin when there are none.</summary>
    private static Aabb Union(IEnumerable<Aabb> boxes)
    {
        Aabb? bounds = null;
        foreach (var box in boxes)
            bounds = bounds.HasValue ? bounds.Value.Merge(box) : box;
        return bounds ?? new Aabb(Vector3.Zero, Vector3.Zero);
    }

    // The transform from a collider's space to the item's, through every node in between.
    private static Transform3D ToItem(Node3D collider, Node3D item)
    {
        var toItem = Transform3D.Identity;
        for (Node? step = collider; step is Node3D spatial && spatial != item; step = step.GetParent())
            toItem = spatial.Transform * toItem;
        return toItem;
    }

    // Every enabled shape except an Area3D's: a trigger takes up no space, unless a node between it and the item is in
    // FootprintGroup (a holder the player must see as belonging to this item, such as an arcade cabinet's).
    private static IEnumerable<CollisionShape3D> FootprintShapes(Node3D item) =>
        item.FindChildren("*", nameof(CollisionShape3D), true, false)
            .OfType<CollisionShape3D>()
            .Where(collider => collider is { Disabled: false, Shape: not null }
                               && (collider.GetParent() is not Area3D || CountsAsFootprint(collider, item)));

    private static bool CountsAsFootprint(Node collider, Node item)
    {
        for (var step = collider.GetParent(); step != null && step != item; step = step.GetParent())
        {
            if (step.IsInGroup(FootprintGroup))
                return true;
        }

        return false;
    }

    private static Aabb ShapeBounds(Shape3D shape) =>
        shape is BoxShape3D box
            ? new Aabb(-box.Size / 2f, box.Size)
            : shape.GetDebugMesh().GetAabb();

    /// <summary>A delivered item as the incoming-shape check sees it.</summary>
    /// <param name="Shapes">Its footprint boxes in world space.</param>
    /// <param name="Bottom">The lowest point of the item, in metres above this marker.</param>
    /// <param name="Top">The highest point of the item, in metres above this marker.</param>
    private readonly record struct Occupant(IReadOnlyList<Aabb> Shapes, float Bottom, float Top);
}
