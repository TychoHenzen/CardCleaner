namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

using Godot;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.CellQueries;

/// <summary>
/// IMapData implementation that wraps an IrregularMesh.
/// Cells are quad IDs, positions are quad centroids.
/// </summary>
public class IrregularMeshMapData : IMapData
{
    private readonly IrregularMesh _mesh;
    private readonly Dictionary<int, string> _terrainTypes = new();
    private readonly HashSet<int> _structureCells = new();

    private int? _playerStartCell;
    private readonly List<int> _enemySpawnCells = new();

    /// <summary>
    /// Terrain type considered passable (default: terrain type 1).
    /// </summary>
    public int PassableTerrainType { get; set; } = 1;

    /// <summary>
    /// Scale factor for converting mesh coordinates to world coordinates.
    /// </summary>
    public float WorldScale { get; set; } = 1f;

    /// <summary>
    /// Offset for converting mesh coordinates to world coordinates.
    /// </summary>
    public Vector2 WorldOffset { get; set; } = Vector2.Zero;

    public IrregularMeshMapData(IrregularMesh mesh)
    {
        _mesh = mesh;
    }

    #region IMapData Implementation

    public int CellCount => _mesh.Quads.Count;

    public Rect2 WorldBounds
    {
        get
        {
            var bounds = _mesh.Bounds;
            var min = bounds.Min * WorldScale + WorldOffset;
            var max = bounds.Max * WorldScale + WorldOffset;
            return new Rect2(min, max - min);
        }
    }

    public bool IsValidCell(int cellId)
    {
        return cellId >= 0 && cellId < _mesh.Quads.Count;
    }

    public Vector2 GetCellCenter(int cellId)
    {
        if (!IsValidCell(cellId))
            return Vector2.Zero;

        return _mesh.Quads[cellId].Centroid * WorldScale + WorldOffset;
    }

    public float GetCellArea(int cellId)
    {
        if (!IsValidCell(cellId))
            return 0f;

        return _mesh.Quads[cellId].Area * WorldScale * WorldScale;
    }

    public string GetTerrainType(int cellId)
    {
        if (_terrainTypes.TryGetValue(cellId, out var terrainType))
            return terrainType;

        // Default: derive from vertex terrain
        if (IsValidCell(cellId))
        {
            var quad = _mesh.Quads[cellId];
            // Use majority vote from corner vertices
            // NOTE: Terrain type > 0 is passable, terrain type 0 is impassable
            int filledCount = QuadCornerCounter.CountFilledCorners(_mesh, quad);
            return filledCount >= 2 ? "terrain_filled" : "terrain_empty";
        }

        return "unknown";
    }

    public bool IsPassable(int cellId)
    {
        if (!IsValidCell(cellId))
            return false;

        if (_structureCells.Contains(cellId))
            return false;

        // A structure on any corner vertex blocks the quad, as MeshQuad.IsPassable does
        if (QuadCornerCounter.HasStructureCorner(_mesh, _mesh.Quads[cellId]))
            return false;

        // Cell is passable if MAJORITY (at least 2 of 4) corner vertices have passable terrain
        // This allows traversal near terrain edges and provides better connectivity
        // NOTE: Terrain type 0 = impassable, any terrain type > 0 = passable
        // Changed from == PassableTerrainType to > 0 because terrain generator
        // assigns unique types (1, 2, 3, etc.) to different passable terrain tiles
        int passableCount = QuadCornerCounter.CountPassableCorners(_mesh, _mesh.Quads[cellId]);
        return passableCount >= 2;
    }

    public bool IsTransparent(int cellId)
    {
        if (!IsValidCell(cellId))
            return false;

        // Terrain is transparent; cell structures and walls on a corner vertex block line of sight
        // (fences and other vertex structures block movement only)
        if (_structureCells.Contains(cellId))
            return false;

        return !QuadCornerCounter.HasSightBlockingCorner(_mesh, _mesh.Quads[cellId]);
    }

    public bool HasStructure(int cellId)
    {
        return _structureCells.Contains(cellId);
    }

    public IEnumerable<int> GetAdjacentCells(int cellId)
    {
        if (!IsValidCell(cellId))
            yield break;

        foreach (var adjacentId in _mesh.Quads[cellId].AdjacentQuadIds)
        {
            yield return adjacentId;
        }
    }

    public float GetMovementCost(int fromCell, int toCell)
    {
        if (!IsValidCell(fromCell) || !IsValidCell(toCell))
            return float.PositiveInfinity;

        if (!IsPassable(toCell))
            return float.PositiveInfinity;

        // Cost is Euclidean distance between centroids
        var fromPos = _mesh.Quads[fromCell].Centroid;
        var toPos = _mesh.Quads[toCell].Centroid;
        return fromPos.DistanceTo(toPos) * WorldScale;
    }

    public int? GetCellAtPosition(Vector2 worldPos)
    {
        // Convert world position to mesh coordinates
        var meshPos = (worldPos - WorldOffset) / WorldScale;

        var quad = _mesh.GetQuadAtPosition(meshPos);
        return quad?.Id;
    }

    public IEnumerable<int> GetCellsInRadius(Vector2 center, float radius)
    {
        // Convert to mesh coordinates
        var meshCenter = (center - WorldOffset) / WorldScale;
        var meshRadius = radius / WorldScale;
        float radiusSq = meshRadius * meshRadius;

        foreach (var quad in _mesh.Quads)
        {
            if (quad.Centroid.DistanceSquaredTo(meshCenter) <= radiusSq)
            {
                yield return quad.Id;
            }
        }
    }

    public IEnumerable<int> GetCellsInRect(Rect2 rect)
    {
        // Convert to mesh coordinates
        var meshRect = new Rect2(
            (rect.Position - WorldOffset) / WorldScale,
            rect.Size / WorldScale
        );

        foreach (var quad in _mesh.GetQuadsIntersectingRect(meshRect))
        {
            yield return quad.Id;
        }
    }

    public int? PlayerStartCell => _playerStartCell;

    public IReadOnlyList<int> EnemySpawnCells => _enemySpawnCells;

    #endregion

    #region Map Setup Methods

    /// <summary>
    /// Set the player start cell.
    /// </summary>
    public void SetPlayerStart(int cellId)
    {
        if (IsValidCell(cellId))
            _playerStartCell = cellId;
    }

    /// <summary>
    /// Set the player start from a world position.
    /// </summary>
    public void SetPlayerStartFromPosition(Vector2 worldPos)
    {
        var cell = GetCellAtPosition(worldPos);
        if (cell.HasValue)
            _playerStartCell = cell.Value;
    }

    /// <summary>
    /// Add an enemy spawn cell.
    /// </summary>
    public void AddEnemySpawn(int cellId)
    {
        if (IsValidCell(cellId) && !_enemySpawnCells.Contains(cellId))
            _enemySpawnCells.Add(cellId);
    }

    /// <summary>
    /// Add an enemy spawn from a world position.
    /// </summary>
    public void AddEnemySpawnFromPosition(Vector2 worldPos)
    {
        var cell = GetCellAtPosition(worldPos);
        if (cell.HasValue)
            AddEnemySpawn(cell.Value);
    }

    /// <summary>
    /// Remove an enemy spawn cell.
    /// </summary>
    /// <returns>True if the enemy was removed, false if not found.</returns>
    public bool RemoveEnemySpawn(int cellId)
    {
        return _enemySpawnCells.Remove(cellId);
    }

    /// <summary>
    /// Place a structure at a cell, blocking movement.
    /// </summary>
    public void PlaceStructure(int cellId)
    {
        if (IsValidCell(cellId))
            _structureCells.Add(cellId);
    }

    /// <summary>
    /// Remove a structure from a cell.
    /// </summary>
    public void RemoveStructure(int cellId)
    {
        _structureCells.Remove(cellId);
    }

    /// <summary>
    /// Set a custom terrain type for a cell.
    /// </summary>
    public void SetTerrainType(int cellId, string terrainType)
    {
        if (IsValidCell(cellId))
            _terrainTypes[cellId] = terrainType;
    }

    /// <summary>
    /// Get the underlying mesh (for rendering and advanced operations).
    /// </summary>
    public IrregularMesh GetMesh() => _mesh;

    /// <summary>
    /// Find a random passable cell.
    /// </summary>
    public int? FindRandomPassableCell(int seed) => PassableCellFinder.FindRandom(this, seed);

    /// <summary>
    /// Find the nearest passable cell to a world position.
    /// </summary>
    public int? FindNearestPassableCell(Vector2 worldPos)
    {
        var meshPos = (worldPos - WorldOffset) / WorldScale;
        return PassableCellFinder.FindNearest(this, _mesh, meshPos);
    }

    #endregion
}
