using CardCleaner.Scripts.Features.Workshop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Workshop.Components;

/// <summary>
///     The workshop's walls: a GridMap of small gray cells baked from <see cref="WorkshopHallLayout" />. The cells
///     are stored in the scene, so the layout shows in the editor; the inspector's "Bake Layout" button rewrites
///     them after the layout changes. It adds world-space cell conversion for later placement code, so conveyors
///     and devices can snap to the same grid the walls are built on. The grid node's transform is honoured.
/// </summary>
[Tool]
public partial class WorkshopGrid : GridMap
{
    /// <summary>Editor button: replaces the stored cells with <see cref="WorkshopHallLayout.Default" />.</summary>
    [ExportToolButton("Bake Layout")]
    public Callable BakeLayoutButton => Callable.From(Bake);

    /// <summary>Cell conversion for this grid's current cell size.</summary>
    public GridCoordinates Coordinates => new(CellSize);

    /// <summary>Replaces every cell with the walls of the default layout.</summary>
    public void Bake() => Build(WorkshopHallLayout.Default);

    /// <summary>Replaces every cell with the walls of <paramref name="layout" />.</summary>
    public void Build(WorkshopHallLayout layout)
    {
        Clear();
        foreach (var cell in layout.Walls)
            SetCellItem(new Vector3I(cell.X, WorkshopHallLayout.WallLayer, cell.Y), WorkshopHallLayout.WallItem);
    }

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
