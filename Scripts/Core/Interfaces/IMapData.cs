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

/// <summary>
/// Extension methods for IMapData.
/// </summary>
public static class MapDataExtensions
{
    /// <summary>
    /// Get the player start position in world coordinates.
    /// </summary>
    public static Vector2? GetPlayerStartPosition(this IMapData mapData)
    {
        var cell = mapData.PlayerStartCell;
        return cell.HasValue ? mapData.GetCellCenter(cell.Value) : null;
    }

    /// <summary>
    /// Get all enemy spawn positions in world coordinates.
    /// </summary>
    public static IEnumerable<Vector2> GetEnemySpawnPositions(this IMapData mapData)
    {
        foreach (var cellId in mapData.EnemySpawnCells)
        {
            yield return mapData.GetCellCenter(cellId);
        }
    }

    /// <summary>
    /// Check if a path exists between two cells (simple BFS check).
    /// </summary>
    public static bool CanReach(this IMapData mapData, int fromCell, int toCell)
    {
        if (!mapData.IsValidCell(fromCell) || !mapData.IsValidCell(toCell))
            return false;

        if (fromCell == toCell)
            return true;

        var visited = new HashSet<int> { fromCell };
        var queue = new Queue<int>();
        queue.Enqueue(fromCell);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var neighbor in mapData.GetAdjacentCells(current))
            {
                if (neighbor == toCell)
                    return true;

                if (visited.Add(neighbor) && mapData.IsPassable(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Estimate distance between two cells (Euclidean distance between centers).
    /// </summary>
    public static float EstimateDistance(this IMapData mapData, int fromCell, int toCell)
    {
        var fromPos = mapData.GetCellCenter(fromCell);
        var toPos = mapData.GetCellCenter(toCell);
        return fromPos.DistanceTo(toPos);
    }

    /// <summary>
    /// Get all passable cells in the map.
    /// </summary>
    public static IEnumerable<int> GetPassableCells(this IMapData mapData)
    {
        for (int i = 0; i < mapData.CellCount; i++)
        {
            if (mapData.IsPassable(i))
                yield return i;
        }
    }
}
