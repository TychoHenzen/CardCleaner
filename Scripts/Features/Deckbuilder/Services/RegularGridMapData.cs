namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

using Godot;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// IMapData adapter that wraps SimpleMapData for regular grid maps.
/// Cell IDs are linearized grid positions: cellId = y * width + x
/// </summary>
public class RegularGridMapData : IMapData
{
    private readonly SimpleMapData _simpleMapData;
    private readonly float _tileSize;

    /// <summary>
    /// Create a map data adapter for a regular grid.
    /// </summary>
    /// <param name="simpleMapData">The underlying SimpleMapData.</param>
    /// <param name="tileSize">Size of each tile in world units (default: 16).</param>
    public RegularGridMapData(SimpleMapData simpleMapData, float tileSize = 16f)
    {
        _simpleMapData = simpleMapData;
        _tileSize = tileSize;
    }

    #region Coordinate Conversion

    /// <summary>
    /// Convert a grid position to a cell ID.
    /// </summary>
    public int PositionToCellId(Vector2I gridPos)
    {
        return gridPos.Y * _simpleMapData.Size.X + gridPos.X;
    }

    /// <summary>
    /// Convert a cell ID to a grid position.
    /// </summary>
    public Vector2I CellIdToPosition(int cellId)
    {
        int x = cellId % _simpleMapData.Size.X;
        int y = cellId / _simpleMapData.Size.X;
        return new Vector2I(x, y);
    }

    /// <summary>
    /// Convert a grid position to world coordinates (center of cell).
    /// </summary>
    public Vector2 GridToWorld(Vector2I gridPos)
    {
        return new Vector2(
            (gridPos.X + 0.5f) * _tileSize,
            (gridPos.Y + 0.5f) * _tileSize
        );
    }

    /// <summary>
    /// Convert world coordinates to grid position.
    /// </summary>
    public Vector2I WorldToGrid(Vector2 worldPos)
    {
        return new Vector2I(
            Mathf.FloorToInt(worldPos.X / _tileSize),
            Mathf.FloorToInt(worldPos.Y / _tileSize)
        );
    }

    #endregion

    #region IMapData Implementation

    public int CellCount => _simpleMapData.Size.X * _simpleMapData.Size.Y;

    public Rect2 WorldBounds => new(
        Vector2.Zero,
        new Vector2(_simpleMapData.Size.X * _tileSize, _simpleMapData.Size.Y * _tileSize)
    );

    public bool IsValidCell(int cellId)
    {
        if (cellId < 0 || cellId >= CellCount)
            return false;

        var pos = CellIdToPosition(cellId);
        return pos.X >= 0 && pos.X < _simpleMapData.Size.X &&
               pos.Y >= 0 && pos.Y < _simpleMapData.Size.Y;
    }

    public Vector2 GetCellCenter(int cellId)
    {
        var pos = CellIdToPosition(cellId);
        return GridToWorld(pos);
    }

    public float GetCellArea(int cellId)
    {
        return _tileSize * _tileSize;
    }

    public string GetTerrainType(int cellId)
    {
        var pos = CellIdToPosition(cellId);
        return _simpleMapData.GetTileId(pos);
    }

    public bool IsPassable(int cellId)
    {
        var pos = CellIdToPosition(cellId);
        return _simpleMapData.IsPassable(pos);
    }

    public bool IsTransparent(int cellId)
    {
        var pos = CellIdToPosition(cellId);
        return _simpleMapData.IsTransparent(pos);
    }

    public bool HasStructure(int cellId)
    {
        // SimpleMapData doesn't explicitly track structures separately
        // A cell with impassable terrain is effectively a structure
        return !IsPassable(cellId) && IsValidCell(cellId);
    }

    public IEnumerable<int> GetAdjacentCells(int cellId)
    {
        var pos = CellIdToPosition(cellId);

        // 4-directional neighbors (cardinal only)
        var neighbors = new Vector2I[]
        {
            new(pos.X + 1, pos.Y),     // East
            new(pos.X - 1, pos.Y),     // West
            new(pos.X, pos.Y + 1),     // South
            new(pos.X, pos.Y - 1),     // North
        };

        foreach (var neighbor in neighbors)
        {
            if (neighbor.X >= 0 && neighbor.X < _simpleMapData.Size.X &&
                neighbor.Y >= 0 && neighbor.Y < _simpleMapData.Size.Y)
            {
                yield return PositionToCellId(neighbor);
            }
        }
    }

    public float GetMovementCost(int fromCell, int toCell)
    {
        if (!IsValidCell(fromCell) || !IsValidCell(toCell))
            return float.PositiveInfinity;

        if (!IsPassable(toCell))
            return float.PositiveInfinity;

        // All cardinal moves have the same cost
        return _tileSize;
    }

    public int? GetCellAtPosition(Vector2 worldPos)
    {
        var gridPos = WorldToGrid(worldPos);

        if (gridPos.X < 0 || gridPos.X >= _simpleMapData.Size.X ||
            gridPos.Y < 0 || gridPos.Y >= _simpleMapData.Size.Y)
        {
            return null;
        }

        return PositionToCellId(gridPos);
    }

    public IEnumerable<int> GetCellsInRadius(Vector2 center, float radius)
    {
        var gridCenter = WorldToGrid(center);
        int gridRadius = Mathf.CeilToInt(radius / _tileSize);
        float radiusSq = radius * radius;

        for (int dy = -gridRadius; dy <= gridRadius; dy++)
        {
            for (int dx = -gridRadius; dx <= gridRadius; dx++)
            {
                var gridPos = new Vector2I(gridCenter.X + dx, gridCenter.Y + dy);

                if (gridPos.X < 0 || gridPos.X >= _simpleMapData.Size.X ||
                    gridPos.Y < 0 || gridPos.Y >= _simpleMapData.Size.Y)
                {
                    continue;
                }

                var cellCenter = GridToWorld(gridPos);
                if (cellCenter.DistanceSquaredTo(center) <= radiusSq)
                {
                    yield return PositionToCellId(gridPos);
                }
            }
        }
    }

    public IEnumerable<int> GetCellsInRect(Rect2 rect)
    {
        var minGrid = WorldToGrid(rect.Position);
        var exclusiveMax = rect.Position + rect.Size;
        var maxGrid = new Vector2I(
            Mathf.Max(minGrid.X, Mathf.CeilToInt(exclusiveMax.X / _tileSize) - 1),
            Mathf.Max(minGrid.Y, Mathf.CeilToInt(exclusiveMax.Y / _tileSize) - 1)
        );

        minGrid = new Vector2I(
            Mathf.Max(0, minGrid.X),
            Mathf.Max(0, minGrid.Y)
        );
        maxGrid = new Vector2I(
            Mathf.Min(_simpleMapData.Size.X - 1, maxGrid.X),
            Mathf.Min(_simpleMapData.Size.Y - 1, maxGrid.Y)
        );

        for (int y = minGrid.Y; y <= maxGrid.Y; y++)
        {
            for (int x = minGrid.X; x <= maxGrid.X; x++)
            {
                yield return PositionToCellId(new Vector2I(x, y));
            }
        }
    }

    public int? PlayerStartCell
    {
        get
        {
            var pos = _simpleMapData.PlayerStart;
            if (pos.X >= 0 && pos.X < _simpleMapData.Size.X &&
                pos.Y >= 0 && pos.Y < _simpleMapData.Size.Y)
            {
                return PositionToCellId(pos);
            }
            return null;
        }
    }

    public IReadOnlyList<int> EnemySpawnCells
    {
        get
        {
            var cells = new List<int>();
            foreach (var pos in _simpleMapData.EnemyPositions)
            {
                if (pos.X >= 0 && pos.X < _simpleMapData.Size.X &&
                    pos.Y >= 0 && pos.Y < _simpleMapData.Size.Y)
                {
                    cells.Add(PositionToCellId(pos));
                }
            }
            return cells;
        }
    }

    #endregion

    #region Additional Methods

    /// <summary>
    /// Get the underlying SimpleMapData.
    /// </summary>
    public SimpleMapData GetSimpleMapData() => _simpleMapData;

    /// <summary>
    /// Get the tile size in world units.
    /// </summary>
    public float TileSize => _tileSize;

    /// <summary>
    /// The grid dimensions (width, height) in cells.
    /// </summary>
    public Vector2I Size => _simpleMapData.Size;

    /// <summary>
    /// Convert a Vector2I position to cell ID (convenience method).
    /// </summary>
    public int GetCellId(Vector2I gridPos) => PositionToCellId(gridPos);

    /// <summary>
    /// Convert a cell ID to Vector2I position (convenience method).
    /// </summary>
    public Vector2I GetGridPosition(int cellId) => CellIdToPosition(cellId);

    #endregion
}
