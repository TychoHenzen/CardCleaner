namespace CardCleaner.Scripts.Core.Interfaces;

using Godot;
using System.Collections.Generic;

/// <summary>
/// Abstract interface for map data that supports both regular grids and irregular meshes.
/// Cells are identified by integer IDs, and the interface provides spatial queries,
/// adjacency information, and terrain properties.
/// </summary>
public interface IMapData
{
    #region Spatial Properties

    /// <summary>
    /// Total number of cells in the map.
    /// </summary>
    int CellCount { get; }

    /// <summary>
    /// Bounding box of the map in world coordinates.
    /// </summary>
    Rect2 WorldBounds { get; }

    /// <summary>
    /// Check if a cell ID is valid.
    /// </summary>
    bool IsValidCell(int cellId);

    /// <summary>
    /// Get the center position of a cell in world coordinates.
    /// </summary>
    Vector2 GetCellCenter(int cellId);

    /// <summary>
    /// Get the approximate area of a cell (for cost calculations).
    /// </summary>
    float GetCellArea(int cellId);

    #endregion

    #region Terrain Properties

    /// <summary>
    /// Get the terrain type ID at a cell.
    /// </summary>
    string GetTerrainType(int cellId);

    /// <summary>
    /// Check if a cell is passable (can be walked through).
    /// </summary>
    bool IsPassable(int cellId);

    /// <summary>
    /// Check if a cell is transparent (doesn't block line of sight).
    /// </summary>
    bool IsTransparent(int cellId);

    /// <summary>
    /// Check if a cell has a structure that blocks movement.
    /// </summary>
    bool HasStructure(int cellId);

    #endregion

    #region Adjacency

    /// <summary>
    /// Get all cells adjacent to the given cell (for pathfinding).
    /// </summary>
    IEnumerable<int> GetAdjacentCells(int cellId);

    /// <summary>
    /// Get the movement cost from one cell to an adjacent cell.
    /// Returns float.PositiveInfinity if movement is impossible.
    /// </summary>
    float GetMovementCost(int fromCell, int toCell);

    #endregion

    #region Spatial Lookup

    /// <summary>
    /// Get the cell containing a world position, or null if outside map bounds.
    /// </summary>
    int? GetCellAtPosition(Vector2 worldPos);

    /// <summary>
    /// Get all cells within a radius of a world position.
    /// </summary>
    IEnumerable<int> GetCellsInRadius(Vector2 center, float radius);

    /// <summary>
    /// Get all cells that intersect a rectangle (for structure placement).
    /// </summary>
    IEnumerable<int> GetCellsInRect(Rect2 rect);

    #endregion

    #region Spawn Points

    /// <summary>
    /// Get the player start position as a cell ID.
    /// </summary>
    int? PlayerStartCell { get; }

    /// <summary>
    /// Get enemy spawn positions as cell IDs.
    /// </summary>
    IReadOnlyList<int> EnemySpawnCells { get; }

    #endregion
}
