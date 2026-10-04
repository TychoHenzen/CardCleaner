using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.Wfc;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// WFC topology implementation for irregular mesh.
/// Cells are mesh vertices. Neighbors are all vertices sharing any quad with a vertex.
/// </summary>
public class IrregularMeshWfcTopology : IWfcTopology
{
    private readonly IrregularMesh _mesh;
    private readonly WfcCellState[] _cells;

    /// <summary>
    /// Precomputed neighbor arrays for each vertex (for fast non-alloc access).
    /// Neighbors = all vertices sharing any quad with this vertex (excluding self).
    /// </summary>
    private readonly int[][] _neighbors;

    /// <summary>
    /// Maximum neighbor count across all vertices.
    /// </summary>
    private int _maxNeighborCount;

    /// <summary>
    /// Creates a WFC topology backed by an irregular mesh.
    /// </summary>
    /// <param name="mesh">The mesh providing vertex/quad structure.</param>
    /// <param name="initialTiles">Initial possible tiles for all cells.</param>
    public IrregularMeshWfcTopology(IrregularMesh mesh, IEnumerable<string> initialTiles)
    {
        _mesh = mesh;
        var tileList = initialTiles.ToList();

        // Initialize cell states for each vertex
        _cells = new WfcCellState[mesh.Vertices.Count];
        for (var i = 0; i < mesh.Vertices.Count; i++)
        {
            _cells[i] = new WfcCellState(tileList);
        }

        // Precompute neighbors for each vertex as arrays (for non-alloc access)
        _neighbors = new int[mesh.Vertices.Count][];
        _maxNeighborCount = 0;

        for (var i = 0; i < mesh.Vertices.Count; i++)
        {
            var neighborSet = ComputeNeighborSet(i);
            _neighbors[i] = neighborSet.ToArray();
            _maxNeighborCount = Math.Max(_maxNeighborCount, _neighbors[i].Length);
        }
    }

    /// <summary>
    /// Computes all vertices that share any quad with the given vertex.
    /// This includes edge-adjacent vertices AND corner-adjacent vertices
    /// (any vertex in any quad that has this vertex as a corner).
    /// </summary>
    private HashSet<int> ComputeNeighborSet(int vertexId)
    {
        var neighbors = new HashSet<int>();
        var vertex = _mesh.Vertices[vertexId];

        // For each quad that has this vertex as a corner
        foreach (var quadId in vertex.AdjacentQuadIds)
        {
            var quad = _mesh.Quads[quadId];

            // Add all other vertices of this quad as neighbors
            foreach (var otherVertexId in quad.VertexIds)
            {
                if (otherVertexId != vertexId)
                {
                    neighbors.Add(otherVertexId);
                }
            }
        }

        return neighbors;
    }

    /// <inheritdoc />
    public int CellCount => _cells.Length;

    /// <inheritdoc />
    public WfcCellState GetCell(int cellId) => _cells[cellId];

    /// <inheritdoc />
    public bool IsValidCell(int cellId) => cellId >= 0 && cellId < _cells.Length;

    /// <inheritdoc />
    public IEnumerable<int> GetAllCellIds()
    {
        for (var i = 0; i < _cells.Length; i++)
        {
            yield return i;
        }
    }

    /// <inheritdoc />
    public IEnumerable<int> GetNeighbors(int cellId)
    {
        if (cellId < 0 || cellId >= _neighbors.Length)
            yield break;

        foreach (var neighbor in _neighbors[cellId])
        {
            yield return neighbor;
        }
    }

    /// <inheritdoc />
    public int GetNeighborsNonAlloc(int cellId, Span<int> output)
    {
        if (cellId < 0 || cellId >= _neighbors.Length)
            return 0;

        var neighbors = _neighbors[cellId];
        var count = Math.Min(neighbors.Length, output.Length);

        for (var i = 0; i < count; i++)
        {
            output[i] = neighbors[i];
        }

        return count;
    }

    /// <inheritdoc />
    public int MaxNeighborCount => _maxNeighborCount;

    /// <inheritdoc />
    public bool HasCollapsedNeighbor(int cellId)
    {
        foreach (var neighborId in GetNeighbors(cellId))
        {
            if (_cells[neighborId].IsCollapsed())
                return true;
        }
        return false;
    }

    /// <inheritdoc />
    public string? GetCollapsedTileAt(int cellId)
    {
        if (cellId < 0 || cellId >= _cells.Length)
            return null;

        var cell = _cells[cellId];
        return cell.IsCollapsed() ? cell.GetCollapsedTile() : null;
    }

    /// <inheritdoc />
    public bool IsFullyCollapsed()
    {
        foreach (var cell in _cells)
        {
            if (!cell.IsCollapsed())
                return false;
        }
        return true;
    }

    /// <inheritdoc />
    public bool HasContradiction()
    {
        foreach (var cell in _cells)
        {
            if (cell.IsContradiction())
                return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the mesh this topology is based on.
    /// </summary>
    public IrregularMesh Mesh => _mesh;

    /// <summary>
    /// Gets the vertex corresponding to a cell ID.
    /// </summary>
    public MeshVertex GetVertex(int cellId) => _mesh.Vertices[cellId];
}
