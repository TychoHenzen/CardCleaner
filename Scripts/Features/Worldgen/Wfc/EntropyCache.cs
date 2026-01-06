using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Fast cell selection using simple entropy (tile count) for selection,
/// avoiding expensive constraint evaluation during cell selection phase.
/// Full constraint weights are only computed for the selected cell during tile selection.
/// </summary>
public class EntropyCache
{
    /// <summary>
    /// Gets the frontier cell with lowest simple entropy (fewest possible tiles).
    /// Uses O(1) tile count instead of O(tiles × constraints) weighted entropy.
    /// This is a valid WFC heuristic - cells with fewer options should be collapsed first.
    /// </summary>
    public Vector2I? GetLowestEntropyCell(WfcGrid grid, RandomNumberGenerator rng)
    {
        var minTileCount = int.MaxValue;
        var candidates = new List<Vector2I>();
        Vector2I? anyUncollapsed = null;

        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var pos = new Vector2I(x, y);
                var cell = grid.GetCell(pos);

                if (cell.IsCollapsed())
                    continue;

                anyUncollapsed ??= pos;

                // Only consider frontier cells
                if (!grid.HasCollapsedNeighbor(pos))
                    continue;

                var tileCount = cell.GetPossibleTiles().Count;

                if (tileCount < minTileCount)
                {
                    minTileCount = tileCount;
                    candidates.Clear();
                    candidates.Add(pos);
                }
                else if (tileCount == minTileCount)
                {
                    candidates.Add(pos);
                }
            }
        }

        // Fallback: any uncollapsed cell if no frontier
        if (candidates.Count == 0)
            return anyUncollapsed;

        return candidates[rng.RandiRange(0, candidates.Count - 1)];
    }

    // Reset is now a no-op since we don't cache anything
    public void Reset() { }

    // MarkNeighborsDirty is now a no-op since we recalculate each time
    public void MarkNeighborsDirty(Vector2I collapsedPos, WfcGrid grid) { }
}
