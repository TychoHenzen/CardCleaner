using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Picks the lowest weighted-entropy cell among frontier cells (adjacent to collapsed).
/// Only computes expensive entropy for frontier cells, dramatically reducing work.
/// </summary>
internal static class WfcFrontierEntropySelector
{
    /// <summary>
    /// Falls back to any uncollapsed cell only when no frontier exists (start of generation).
    /// </summary>
    /// <returns>Position of cell with lowest weighted entropy, or null if all collapsed</returns>
    internal static Vector2I? Select(
        WfcGrid grid,
        Func<Vector2I, IReadOnlyDictionary<string, float>> getWeightsAt,
        RandomNumberGenerator rng)
    {
        // First pass: find frontier cells (adjacent to collapsed)
        var scan = ScanFrontier(grid);

        // If no frontier (start of generation), pick any uncollapsed cell
        if (scan.FrontierCells.Count == 0)
            return scan.AnyUncollapsed;

        // Second pass: compute entropy ONLY for frontier cells
        var candidates = FindLowestEntropyCandidates(grid, scan.FrontierCells, getWeightsAt);
        return candidates[rng.RandiRange(0, candidates.Count - 1)];
    }

    private static FrontierScan ScanFrontier(WfcGrid grid)
    {
        var frontierCells = new List<Vector2I>();
        Vector2I? anyUncollapsed = null;

        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                if (grid.GetCell(x, y).IsExcludedFromSelection())
                    continue;

                var pos = new Vector2I(x, y);
                anyUncollapsed ??= pos;

                if (grid.HasCollapsedNeighbor(pos))
                    frontierCells.Add(pos);
            }
        }

        return new FrontierScan(frontierCells, anyUncollapsed);
    }

    private static List<Vector2I> FindLowestEntropyCandidates(
        WfcGrid grid,
        List<Vector2I> frontierCells,
        Func<Vector2I, IReadOnlyDictionary<string, float>> getWeightsAt)
    {
        var lowestEntropy = float.MaxValue;
        var candidates = new List<Vector2I>();

        foreach (var pos in frontierCells)
        {
            var weights = getWeightsAt(pos);
            var entropy = grid.GetCell(pos).GetWeightedEntropy(weights);

            if (entropy < lowestEntropy)
            {
                lowestEntropy = entropy;
                candidates.Clear();
                candidates.Add(pos);
            }
            else if (Mathf.IsEqualApprox(entropy, lowestEntropy))
            {
                candidates.Add(pos);
            }
        }

        return candidates;
    }
}
