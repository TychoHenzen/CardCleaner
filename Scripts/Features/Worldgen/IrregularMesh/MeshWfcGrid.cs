using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.Wfc;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// WFC grid adapter for irregular mesh terrain generation.
/// Uses vertex IDs instead of 2D grid positions.
/// </summary>
public class MeshWfcGrid
{
    private readonly IrregularMesh _mesh;
    private readonly Dictionary<int, WfcCellState> _cells = new();

    /// <summary>
    /// Number of vertices (cells) in the grid.
    /// </summary>
    public int CellCount => _mesh.Vertices.Count;

    /// <summary>
    /// The underlying mesh.
    /// </summary>
    public IrregularMesh Mesh => _mesh;

    /// <summary>
    /// Creates a WFC grid from an irregular mesh.
    /// </summary>
    /// <param name="mesh">The mesh to assign terrain to.</param>
    /// <param name="initialTiles">Initial tile possibilities for all vertices.</param>
    public MeshWfcGrid(IrregularMesh mesh, IEnumerable<string> initialTiles)
    {
        _mesh = mesh;
        var tileList = new List<string>(initialTiles);

        foreach (var vertex in mesh.Vertices)
        {
            _cells[vertex.Id] = new WfcCellState(tileList);
        }
    }

    /// <summary>
    /// Gets the cell state at the given vertex ID.
    /// </summary>
    public WfcCellState GetCell(int vertexId) => _cells[vertexId];

    /// <summary>
    /// Checks if a vertex ID is valid.
    /// </summary>
    public bool IsValidVertex(int vertexId) => _cells.ContainsKey(vertexId);

    /// <summary>
    /// Gets adjacent vertex IDs for a given vertex.
    /// </summary>
    public IEnumerable<int> GetNeighbors(int vertexId)
    {
        return _mesh.Vertices[vertexId].AdjacentVertexIds;
    }

    /// <summary>
    /// Checks if all cells have collapsed to a single tile.
    /// </summary>
    public bool IsFullyCollapsed()
    {
        return _cells.Values.All(cell => cell.IsCollapsed());
    }

    /// <summary>
    /// Checks if any cell is in contradiction state (no valid options).
    /// </summary>
    public bool HasContradiction()
    {
        return _cells.Values.Any(cell => cell.IsContradiction());
    }

    /// <summary>
    /// Checks if a vertex has at least one collapsed neighbor.
    /// </summary>
    public bool HasCollapsedNeighbor(int vertexId)
    {
        foreach (var neighborId in GetNeighbors(vertexId))
        {
            if (GetCell(neighborId).IsCollapsed())
                return true;
        }
        return false;
    }

    /// <summary>
    /// Gets all vertex IDs.
    /// </summary>
    public IEnumerable<int> GetAllVertexIds()
    {
        return _cells.Keys;
    }

    /// <summary>
    /// Gets the collapsed tile ID at a vertex, or null if not collapsed.
    /// </summary>
    public string? GetCollapsedTileAt(int vertexId)
    {
        var cell = _cells[vertexId];
        return cell.IsCollapsed() ? cell.GetCollapsedTile() : null;
    }

    /// <summary>
    /// Applies the collapsed WFC results to the mesh vertex terrain types.
    /// </summary>
    /// <param name="tileToTerrainType">Mapping from tile ID to terrain type int.</param>
    public void ApplyToMesh(Dictionary<string, int> tileToTerrainType)
    {
        foreach (var vertex in _mesh.Vertices)
        {
            var cell = _cells[vertex.Id];
            if (cell.IsCollapsed())
            {
                var tileId = cell.GetCollapsedTile();
                if (tileToTerrainType.TryGetValue(tileId, out var terrainType))
                {
                    vertex.TerrainType = terrainType;
                }
            }
        }

        // Recompute quad bitmasks after terrain assignment
        _mesh.UpdateAllCachedProperties();
    }

    /// <summary>
    /// Creates a deep copy of this grid for backtracking.
    /// </summary>
    public MeshWfcGrid Clone()
    {
        var clone = new MeshWfcGrid(_mesh, new List<string>());
        foreach (var (vertexId, cell) in _cells)
        {
            clone._cells[vertexId] = new WfcCellState(cell);
        }
        return clone;
    }
}
