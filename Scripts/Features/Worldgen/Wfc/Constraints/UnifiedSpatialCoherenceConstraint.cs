using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Unified spatial coherence constraint that works with any IWfcGrid implementation.
/// Encourages tiles that match neighboring collapsed cells to create contiguous regions.
/// </summary>
public class UnifiedSpatialCoherenceConstraint : IUnifiedWfcConstraint
{
    private readonly Dictionary<int, string> _collapsedTiles = new();
    private readonly Dictionary<int, int> _regionSizes = new();

    /// <summary>
    /// Target size for coherent regions. Regions below this get full boost;
    /// regions above start tapering to encourage diversity.
    /// </summary>
    public int TargetRegionSize { get; set; } = 50;

    /// <summary>
    /// Factor controlling how much matching neighbors boost tile probability.
    /// Higher values create larger, more coherent regions.
    /// </summary>
    public float BoostFactor { get; set; } = 10.0f;

    /// <summary>
    /// Minimum weight modifier to prevent complete elimination.
    /// </summary>
    public float MinModifier { get; set; } = 0.1f;

    public float GetWeightModifier(int cellId, string tileId, IWfcGrid grid)
    {
        // Count matching neighbors
        var matchingNeighbors = 0;
        var totalCollapsedNeighbors = 0;
        var largestMatchingRegion = 0;

        foreach (var neighborId in grid.GetNeighbors(cellId))
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
            var taperStrength = Mathf.Max(0.0f, 1.0f - (oversize - 1.0f) * 0.5f);
            modifier = 1.0f + matchRatio * BoostFactor * taperStrength;
        }
        else
        {
            // Full boost for regions below target size
            modifier = 1.0f + matchRatio * BoostFactor;
        }

        return Mathf.Max(MinModifier, modifier);
    }

    public void OnTileCollapsed(int cellId, string tileId, IWfcGrid grid)
    {
        _collapsedTiles[cellId] = tileId;

        // Calculate region size via flood-fill of same-type neighbors
        var regionSize = CalculateRegionSize(cellId, tileId, grid);

        // Update all cells in this region with new size
        UpdateRegionSizes(cellId, tileId, regionSize, grid);
    }

    public void Reset()
    {
        _collapsedTiles.Clear();
        _regionSizes.Clear();
    }

    private int CalculateRegionSize(int startCellId, string tileId, IWfcGrid grid)
    {
        var visited = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(startCellId);
        visited.Add(startCellId);

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

    private void UpdateRegionSizes(int startCellId, string tileId, int regionSize, IWfcGrid grid)
    {
        var visited = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(startCellId);
        visited.Add(startCellId);

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
