namespace CardCleaner.Scripts.Core.Interfaces;

using Godot;

/// <summary>
/// Map data over a regular grid: adds the conversions between cell ids, grid positions and world positions.
/// Cell ids are linearized grid positions (cellId = y * width + x).
/// </summary>
public interface IGridMapData : IMapData
{
    /// <summary>The grid dimensions (width, height) in cells.</summary>
    Vector2I Size { get; }

    /// <summary>Convert a cell ID to a grid position.</summary>
    Vector2I CellIdToPosition(int cellId);

    /// <summary>Convert a grid position to a cell ID.</summary>
    int PositionToCellId(Vector2I gridPos);

    /// <summary>Convert world coordinates to a grid position.</summary>
    Vector2I WorldToGrid(Vector2 worldPos);

    /// <summary>Convert a grid position to world coordinates (center of cell).</summary>
    Vector2 GridToWorld(Vector2I gridPos);
}
