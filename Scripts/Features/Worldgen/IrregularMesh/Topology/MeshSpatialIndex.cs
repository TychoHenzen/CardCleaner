using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Topology;

/// <summary>
/// Spatial hash grid giving O(1) average candidate-quad lookup for a world position.
/// </summary>
internal sealed class MeshSpatialIndex
{
    private readonly Dictionary<(int, int), List<int>> _cells = new();
    private float _cellSize = 50f; // Default cell size, adjusted based on mesh

    /// <summary>
    /// Rebuild the grid from the quads, sizing cells from the average quad area.
    /// </summary>
    internal void Rebuild(IReadOnlyList<MeshQuad> quads)
    {
        _cells.Clear();

        if (quads.Count == 0)
            return;

        float totalArea = quads.Sum(q => q.Area);
        float avgArea = totalArea / quads.Count;
        _cellSize = Mathf.Max(10f, Mathf.Sqrt(avgArea) * 2f);

        foreach (var quad in quads)
        {
            InsertQuad(quad);
        }
    }

    /// <summary>
    /// Returns the ids of quads registered in the grid cell holding the position, or null if the cell is empty.
    /// </summary>
    internal List<int>? GetCandidates(Vector2 worldPos)
    {
        int gridX = (int)Mathf.Floor(worldPos.X / _cellSize);
        int gridY = (int)Mathf.Floor(worldPos.Y / _cellSize);

        return _cells.TryGetValue((gridX, gridY), out var quadIds) ? quadIds : null;
    }

    /// <summary>
    /// Insert the quad into all grid cells its bounding box overlaps.
    /// </summary>
    private void InsertQuad(MeshQuad quad)
    {
        var corners = quad.GetCornerPositions();
        float minX = corners.Min(c => c.X);
        float maxX = corners.Max(c => c.X);
        float minY = corners.Min(c => c.Y);
        float maxY = corners.Max(c => c.Y);

        int startGridX = (int)Mathf.Floor(minX / _cellSize);
        int endGridX = (int)Mathf.Floor(maxX / _cellSize);
        int startGridY = (int)Mathf.Floor(minY / _cellSize);
        int endGridY = (int)Mathf.Floor(maxY / _cellSize);

        for (int gx = startGridX; gx <= endGridX; gx++)
        {
            for (int gy = startGridY; gy <= endGridY; gy++)
            {
                var cell = (gx, gy);
                if (!_cells.ContainsKey(cell))
                    _cells[cell] = new List<int>();
                _cells[cell].Add(quad.Id);
            }
        }
    }
}
