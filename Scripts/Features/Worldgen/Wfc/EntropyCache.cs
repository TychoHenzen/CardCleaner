using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Caches weighted entropy values for WFC cells.
/// Only recomputes entropy for cells that are affected by changes.
/// </summary>
public class EntropyCache
{
    private readonly Dictionary<Vector2I, float> _entropy = new();
    private readonly HashSet<Vector2I> _dirty = new();
    private readonly List<IEntropyInvalidator> _invalidators = new();
    private bool _needsFullRebuild = true;

    /// <summary>
    /// Registers a constraint that can invalidate cells beyond immediate neighbors.
    /// </summary>
    public void RegisterInvalidator(IEntropyInvalidator invalidator)
    {
        _invalidators.Add(invalidator);
    }

    /// <summary>
    /// Clears all invalidators. Call when setting up a new solve.
    /// </summary>
    public void ClearInvalidators()
    {
        _invalidators.Clear();
    }

    /// <summary>
    /// Resets the cache for a new solve.
    /// </summary>
    public void Reset()
    {
        _needsFullRebuild = true;
        _dirty.Clear();
        _entropy.Clear();
    }

    /// <summary>
    /// Marks cells as needing recalculation after a collapse.
    /// Automatically handles neighbors + queries registered invalidators.
    /// </summary>
    public void OnCellCollapsed(Vector2I collapsedPos, string collapsedTile, WfcGrid grid)
    {
        // Remove collapsed cell from cache
        _entropy.Remove(collapsedPos);
        _dirty.Remove(collapsedPos);

        // Mark all 8-way neighbors as dirty (most constraints depend on neighbors)
        foreach (var neighbor in grid.GetNeighbors8(collapsedPos))
        {
            if (!grid.GetCell(neighbor).IsCollapsed())
                _dirty.Add(neighbor);
        }

        // Query registered invalidators for additional cells
        foreach (var invalidator in _invalidators)
        {
            foreach (var pos in invalidator.GetInvalidatedCells(collapsedPos, collapsedTile, grid))
            {
                if (!grid.GetCell(pos).IsCollapsed())
                    _dirty.Add(pos);
            }
        }
    }

    /// <summary>
    /// Gets the frontier cell with lowest entropy.
    /// Only recomputes entropy for dirty cells.
    /// </summary>
    public Vector2I? GetLowestEntropyCell(
        WfcGrid grid,
        Func<Vector2I, float> computeEntropy,
        RandomNumberGenerator rng)
    {
        // First call: build everything
        if (_needsFullRebuild)
        {
            RebuildAll(grid, computeEntropy);
            _needsFullRebuild = false;
        }
        else
        {
            // Incremental: only update dirty cells
            UpdateDirty(grid, computeEntropy);
        }

        // Find minimum entropy among frontier cells
        var minEntropy = float.MaxValue;
        var candidates = new List<Vector2I>();

        foreach (var kvp in _entropy)
        {
            var pos = kvp.Key;
            var entropy = kvp.Value;

            var cell = grid.GetCell(pos);
            if (cell.IsExcludedFromSelection())
                continue;

            // Only consider frontier cells (adjacent to collapsed)
            if (!grid.HasCollapsedNeighbor(pos))
                continue;

            if (entropy < minEntropy)
            {
                minEntropy = entropy;
                candidates.Clear();
                candidates.Add(pos);
            }
            else if (Mathf.IsEqualApprox(entropy, minEntropy))
            {
                candidates.Add(pos);
            }
        }

        // Fallback: any uncollapsed cell if no frontier (start of generation)
        if (candidates.Count == 0)
        {
            foreach (var kvp in _entropy)
            {
                if (!grid.GetCell(kvp.Key).IsExcludedFromSelection())
                {
                    candidates.Add(kvp.Key);
                    break;
                }
            }
        }

        if (candidates.Count == 0)
            return null;

        return candidates[rng.RandiRange(0, candidates.Count - 1)];
    }

    private void RebuildAll(WfcGrid grid, Func<Vector2I, float> computeEntropy)
    {
        _entropy.Clear();
        _dirty.Clear();

        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var pos = new Vector2I(x, y);
                var cell = grid.GetCell(pos);
                if (!cell.IsExcludedFromSelection())
                {
                    _entropy[pos] = computeEntropy(pos);
                }
            }
        }
    }

    private void UpdateDirty(WfcGrid grid, Func<Vector2I, float> computeEntropy)
    {
        foreach (var pos in _dirty)
        {
            var cell = grid.GetCell(pos);
            if (cell.IsExcludedFromSelection())
            {
                _entropy.Remove(pos);
            }
            else
            {
                _entropy[pos] = computeEntropy(pos);
            }
        }
        _dirty.Clear();
    }
}
