using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using CardCleaner.Scripts.Features.Shop.Furniture;

namespace CardCleaner.Scripts.Features.Shop.Ordering;

/// <summary>
///     A delivery area for a room with a ceiling. Each footprint shape of a delivered item takes the slot cells it
///     covers from above, and a shape past the last row takes none. An item fits a slot when its shapes stay inside
///     <see cref="FloorClearance" /> (when one is set) and overlap no shape of an item in the same height range. It
///     goes on the first free ground slot it fits, otherwise on the lowest stack it fits that keeps it under
///     <see cref="MaxStackHeight" /> and holds up its whole footprint, and is refused when there is none. The
///     footprint is the solid shapes plus the triggers under a node in <see cref="FootprintGroup" />, less every
///     shape under a node in <see cref="NoFootprintGroup" />; other triggers take no space.
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

    /// <summary>
    ///     Group of a node whose shapes never take footprint, such as a shelf's solid price tag. Beats FootprintGroup.
    /// </summary>
    public const string NoFootprintGroup = "delivery_no_footprint";

    // Overlaps and height gaps shorter than this are touching, not overlapping, so a shared face refuses nothing.
    private const float OverlapEpsilon = 0.001f;

    // How far a stacked item may overhang its support, and how far a support's top may sit below the stack top, in
    // metres. Delivered bodies settle by a few millimetres, so equal items still stack on each other.
    private const float SupportTolerance = 0.05f;

    /// <summary>Slot columns along X of a marker that sets no grid of its own.</summary>
    public const int DefaultColumns = 3;

    /// <summary>Slot rows along Z of a marker that sets no grid of its own.</summary>
    public const int DefaultRows = 2;

    /// <summary>
    ///     Cell width (X) of a marker that sets no grid of its own: fits the 2.1 m shelf in one column. A wider
    ///     footprint, such as an arcade cabinet with its holders, takes the next slot that clears every shape.
    /// </summary>
    public const float DefaultCellWidth = 2.3f;

    /// <summary>
    ///     Cell depth (Z) of a marker that sets no grid of its own: fits the 1 m deep shelf in one row. A deeper
    ///     footprint takes the next slot that clears every shape.
    /// </summary>
    public const float DefaultCellDepth = 1.5f;

    private readonly List<Node3D> _delivered = [];

    /// <summary>Slot columns along X, centred on the marker.</summary>
    [Export]
    public int Columns { get; set; } = DefaultColumns;

    /// <summary>Slot rows along Z, centred on the marker.</summary>
    [Export]
    public int Rows { get; set; } = DefaultRows;

    /// <summary>
    ///     Width (X) and depth (Z) of one slot's cell, in metres. Size it to the items this area receives: every item
    ///     takes a whole ground cell, so cells sized for shelves leave no room for small stock.
    /// </summary>
    [Export]
    public Vector2 CellSize { get; set; } = new(DefaultCellWidth, DefaultCellDepth);

    /// <summary>How many ground slots the grid has.</summary>
    public int SlotCount => Columns * Rows;

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

    /// <summary>Offset from this marker of slot <paramref name="index" />'s centre in this marker's own grid.</summary>
    public Vector3 SlotOffset(int index) => GridOffset(index, Columns, Rows, CellSize);

    /// <summary>
    ///     Offset of slot <paramref name="index" /> in a grid of <paramref name="columns" /> by
    ///     <paramref name="rows" /> cells centred on the marker, filled row by row.
    /// </summary>
    public static Vector3 GridOffset(int index, int columns, int rows, Vector2 cellSize)
    {
        var column = index % columns;
        var row = index / columns;
        return new Vector3(
            (column - (columns - 1) / 2f) * cellSize.X,
            0f,
            (row - (rows - 1) / 2f) * cellSize.Y);
    }

    /// <summary>Where the next item should go in this marker's own grid; see the overload below.</summary>
    public Placement? FindPlacement(Node3D item) => FindPlacement(SlotCount, SlotOffset, CellSize, item);

    /// <summary>
    ///     Where the next item should go: the first free ground slot <paramref name="item" /> fits on, otherwise the
    ///     lowest stack it fits on under <see cref="MaxStackHeight" />. Null when no slot is left. Items that no longer
    ///     touch any cell are forgotten, so only live occupants are ever retained.
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
            var offset = slotOffset(slot) + Vector3.Up * lift;
            if (Supported(local, offset, stackTop[slot], occupants) && Fits(local, localBounds, offset, occupants))
                return new Placement(slot, lift);
        }

        return null;
    }

    /// <summary>
    ///     Whether an item stacked at <paramref name="offset" /> would rest on something under all of it: seen from
    ///     above, each of its shapes lies inside a shape whose top is the stack top it rests on, give or take
    ///     <see cref="SupportTolerance" />. So a shelf box is never stacked on a booster, and nothing hangs in the air.
    /// </summary>
    private bool Supported(Aabb[] localShapes, Vector3 offset, float stackTop, IReadOnlyList<Occupant> occupants)
    {
        var supports = occupants
            .SelectMany(occupant => occupant.Shapes)
            .Where(shape => Mathf.Abs(Mathf.Max(shape.End.Y - GlobalPosition.Y, MinItemHeight) - stackTop)
                            <= SupportTolerance)
            .ToArray();
        return localShapes
            .Select(box => new Aabb(box.Position + GlobalPosition + offset, box.Size))
            .All(shape => supports.Any(support => Holds(support, shape)));
    }

    // Seen from above, the support's top covers the shape, letting the shape overhang by the tolerance on each side.
    private static bool Holds(Aabb support, Aabb shape) =>
        shape.Position.X >= support.Position.X - SupportTolerance && shape.End.X <= support.End.X + SupportTolerance
        && shape.Position.Z >= support.Position.Z - SupportTolerance
        && shape.End.Z <= support.End.Z + SupportTolerance;

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
        // count toward the footprint, or a shape in NoFootprintGroup, does not raise the item for the height check
        // either.
        var lowest = ItemBottom(shapes, occupant.GlobalPosition.Y) - GlobalPosition.Y;
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
    ///     refused when one of its shapes leaves <see cref="FloorClearance" /> (when one is set), or overlaps, seen
    ///     from above, a shape of an occupant whose height range overlaps the item's own. Heights are compared for
    ///     each item as a whole, not shape by shape, and no item's range, incoming or delivered, starts below its feet.
    /// </summary>
    private bool Fits(Aabb[] localShapes, Aabb localBounds, Vector3 offset, IReadOnlyList<Occupant> occupants)
    {
        var shapes = localShapes.Select(box => new Aabb(box.Position + GlobalPosition + offset, box.Size)).ToArray();
        if (FloorClearance.HasArea() && !shapes.All(WithinClearance))
            return false;

        var lowest = ItemBottom(localShapes, 0f) + offset.Y;
        var highest = localBounds.End.Y + offset.Y;
        foreach (var occupant in occupants)
        {
            var heightsOverlap = lowest < occupant.Top - OverlapEpsilon && occupant.Bottom < highest - OverlapEpsilon;
            if (heightsOverlap && shapes.Any(shape => occupant.Shapes.Any(other => ShapesOverlap(shape, other))))
                return false;
        }

        return true;
    }

    // ASSUMPTION: an item rests on its origin (FindPlacement takes the origin to be its feet), so shape bounds below
    // the origin are no real reach downward. The workshop conveyor's tilted belt has such bounds; counting them would
    // put a stacked conveyor's lowest point under the top it rests on and refuse a free stack. Delivered items are read
    // the same way, so an item's reach below its feet is the same once placed as it was while arriving.
    // An item with no shapes has no bound at all, so its bottom is its feet.
    private static float ItemBottom(IEnumerable<Aabb> shapes, float feetY) =>
        Mathf.Max(shapes.Select(box => box.Position.Y).DefaultIfEmpty(feetY).Min(), feetY);

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

    // Every enabled shape except an Area3D's, which takes up no space, and except any shape under a node in
    // NoFootprintGroup. A trigger under a node in FootprintGroup counts after all (a holder the player must see as
    // belonging to this item, such as an arcade cabinet's).
    private static IEnumerable<CollisionShape3D> FootprintShapes(Node3D item) =>
        item.FindChildren("*", nameof(CollisionShape3D), true, false)
            .OfType<CollisionShape3D>()
            .Where(collider => collider is { Disabled: false, Shape: not null }
                               && !IsUnder(collider, item, NoFootprintGroup)
                               && (collider.GetParent() is not Area3D || IsUnder(collider, item, FootprintGroup)));

    /// <summary>
    ///     Whether a node between <paramref name="collider" /> and <paramref name="item" /> is in
    ///     <paramref name="group" />. The collider's own body counts; the item root does not.
    /// </summary>
    private static bool IsUnder(Node collider, Node item, string group)
    {
        for (var step = collider.GetParent(); step != null && step != item; step = step.GetParent())
        {
            if (step.IsInGroup(group))
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
    /// <param name="Bottom">The lowest point of the item, never below its feet, in metres above this marker.</param>
    /// <param name="Top">The highest point of the item, in metres above this marker.</param>
    private readonly record struct Occupant(IReadOnlyList<Aabb> Shapes, float Bottom, float Top);
}
