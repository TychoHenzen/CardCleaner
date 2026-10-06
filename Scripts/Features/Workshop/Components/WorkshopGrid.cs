using CardCleaner.Scripts.Features.Workshop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Workshop.Components;

/// <summary>
///     The workshop's open build space: a GridMap of small gray cells holding rooms and hallways. It adds
///     world-space cell conversion for later placement code, so conveyors and devices can snap to the same
///     grid the walls are built on. The grid node's transform is honoured, so the grid may be moved.
/// </summary>
public partial class WorkshopGrid : GridMap
{
    /// <summary>Cell conversion for this grid's current cell size.</summary>
    public GridCoordinates Coordinates => new(CellSize);

    /// <summary>The cell that contains the world position <paramref name="world" />.</summary>
    public Vector3I WorldToCell(Vector3 world)
    {
        return Coordinates.LocalToCell(ToLocal(world));
    }

    /// <summary>The world position of the centre of <paramref name="cell" />.</summary>
    public Vector3 CellToWorld(Vector3I cell)
    {
        return ToGlobal(Coordinates.CellToLocal(cell));
    }
}
