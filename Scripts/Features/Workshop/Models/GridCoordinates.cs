using Godot;

namespace CardCleaner.Scripts.Features.Workshop.Models;

/// <summary>
///     Conversion between positions in a grid's local space and its integer cells, for a grid whose cells
///     are centred on every axis (the GridMap default). It matches <c>GridMap.LocalToMap</c> and
///     <c>GridMap.MapToLocal</c> exactly: a position belongs to the cell whose half-open box contains it,
///     so a point on a boundary belongs to the cell on its positive side, also for negative cells.
///     Later placement code converts world to local with the grid node's transform, then uses this.
/// </summary>
public readonly struct GridCoordinates(Vector3 cellSize)
{
    /// <summary>Edge lengths of one cell.</summary>
    public Vector3 CellSize { get; } = cellSize;

    /// <summary>The cell that contains <paramref name="local" />.</summary>
    public Vector3I LocalToCell(Vector3 local)
    {
        return new Vector3I(
            Mathf.FloorToInt(local.X / CellSize.X),
            Mathf.FloorToInt(local.Y / CellSize.Y),
            Mathf.FloorToInt(local.Z / CellSize.Z));
    }

    /// <summary>The centre of <paramref name="cell" />.</summary>
    public Vector3 CellToLocal(Vector3I cell)
    {
        return new Vector3(
            (cell.X + 0.5f) * CellSize.X,
            (cell.Y + 0.5f) * CellSize.Y,
            (cell.Z + 0.5f) * CellSize.Z);
    }

    /// <summary>The corner of <paramref name="cell" /> with the lowest coordinates.</summary>
    public Vector3 CellMinCorner(Vector3I cell)
    {
        return new Vector3(cell.X * CellSize.X, cell.Y * CellSize.Y, cell.Z * CellSize.Z);
    }
}
