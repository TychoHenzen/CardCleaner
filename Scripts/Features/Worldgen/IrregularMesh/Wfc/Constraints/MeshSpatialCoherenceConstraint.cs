using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Wfc.Constraints;

/// <summary>
/// Encourages spatial coherence by boosting tiles that match neighboring vertices.
/// Creates more natural-looking terrain with contiguous regions instead of
/// scattered random tiles.
/// </summary>
public class MeshSpatialCoherenceConstraint : IMeshWfcConstraint
{
    private readonly Dictionary<int, string> _collapsedTiles = new();
    private readonly Dictionary<int, int> _regionSizes = new();

    /// <summary>
    /// Factor controlling how much matching neighbors boost tile probability.
    /// With BoostFactor=5.0, a tile matching all neighbors gets 6x weight.
    /// </summary>
    public float BoostFactor { get; set; } = 5.0f;

    /// <summary>
    /// Target size for coherent regions. Regions below this get full boost;
    /// regions above start tapering to encourage diversity.
    /// </summary>
    public int TargetRegionSize { get; set; } = 30;

    /// <summary>
    /// Minimum weight modifier to prevent complete elimination.
    /// </summary>
    public float MinModifier { get; set; } = 0.1f;

    public float GetWeightModifier(string tileId, int vertexId, MeshWfcGrid grid)
    {
        // Count matching neighbors
        var matchingNeighbors = 0;
        var totalCollapsedNeighbors = 0;
        var largestMatchingRegion = 0;

        foreach (var neighborId in grid.GetNeighbors(vertexId))
        {
            if (!_collapsedTiles.TryGetValue(neighborId, out var neighborTile))
                continue;

            totalCollapsedNeighbors++;

            if (neighborTile == tileId)
            {
                matchingNeighbors++;

                // Track largest matching region for taper calculation
                if (_regionSizes.TryGetValue(neighborId, out var regionSize))
                {
                    if (regionSize > largestMatchingRegion)
                        largestMatchingRegion = regionSize;
                }
            }
        }

        // No collapsed neighbors = neutral
        if (totalCollapsedNeighbors == 0)
            return 1.0f;

        // No matching neighbors = slight penalty
        if (matchingNeighbors == 0)
            return MinModifier;

        // Calculate boost based on matching proportion and region size
        var matchRatio = (float)matchingNeighbors / totalCollapsedNeighbors;
        float modifier;

        if (largestMatchingRegion >= TargetRegionSize)
        {
            // Taper off for oversized regions to encourage diversity
            var oversize = (float)largestMatchingRegion / TargetRegionSize;
            var taperStrength = System.Math.Max(0.0f, 1.0f - (oversize - 1.0f) * 0.5f);
            modifier = 1.0f + matchRatio * BoostFactor * taperStrength;
        }
        else
        {
            // Full boost for regions below target size
            modifier = 1.0f + matchRatio * BoostFactor;
        }

        return System.Math.Max(MinModifier, modifier);
    }

    public void OnTileCollapsed(int vertexId, string tileId, MeshWfcGrid grid)
    {
        _collapsedTiles[vertexId] = tileId;

        // Calculate region size via flood-fill of same-type neighbors
        var regionSize = CalculateRegionSize(vertexId, tileId, grid);

        // Update all vertices in this region with new size
        UpdateRegionSizes(vertexId, tileId, regionSize, grid);
    }

    public void Reset()
    {
        _collapsedTiles.Clear();
        _regionSizes.Clear();
    }

    private int CalculateRegionSize(int startVertex, string tileId, MeshWfcGrid grid)
    {
        var visited = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(startVertex);
        visited.Add(startVertex);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var neighborId in grid.GetNeighbors(current))
            {
                if (visited.Contains(neighborId))
                    continue;

                if (!_collapsedTiles.TryGetValue(neighborId, out var neighborTile))
                    continue;

                if (neighborTile != tileId)
                    continue;

                visited.Add(neighborId);
                queue.Enqueue(neighborId);
            }
        }

        return visited.Count;
    }

    private void UpdateRegionSizes(int startVertex, string tileId, int regionSize, MeshWfcGrid grid)
    {
        var visited = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(startVertex);
        visited.Add(startVertex);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            _regionSizes[current] = regionSize;

            foreach (var neighborId in grid.GetNeighbors(current))
            {
                if (visited.Contains(neighborId))
                    continue;

                if (!_collapsedTiles.TryGetValue(neighborId, out var neighborTile))
                    continue;

                if (neighborTile != tileId)
                    continue;

                visited.Add(neighborId);
                queue.Enqueue(neighborId);
            }
        }
    }
}
