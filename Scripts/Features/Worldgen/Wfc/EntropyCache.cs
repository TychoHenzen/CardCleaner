using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Caches weighted entropy values for WFC cells.
/// Only recomputes entropy for cells that are affected by changes.
/// Supports both position-based (WfcGrid) and cell ID based (IWfcTopology) access.
/// </summary>
public class EntropyCache
{
    // Cell ID based cache (for topology-agnostic use)
    private readonly Dictionary<int, float> _entropyCellId = new();
    private readonly HashSet<int> _dirtyCellId = new();

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
        _dirtyCellId.Clear();
        _entropyCellId.Clear();
    }

    /// <summary>
    /// Marks cells as needing recalculation after a collapse.
    /// Topology-agnostic version using cell IDs.
    /// </summary>
    public void OnCellCollapsed(int collapsedCellId, string collapsedTile, IWfcTopology topology)
    {
        // Remove collapsed cell from cache
        _entropyCellId.Remove(collapsedCellId);
        _dirtyCellId.Remove(collapsedCellId);

        // Mark all neighbors as dirty (most constraints depend on neighbors)
        foreach (var neighborId in topology.GetWindowNeighbors(collapsedCellId))
        {
            if (!topology.GetCell(neighborId).IsCollapsed())
                _dirtyCellId.Add(neighborId);
        }

        // Query registered invalidators for additional cells
        // Note: invalidators need to be updated to support IWfcTopology
        // For now, only support grid-based invalidators when topology is WfcGrid
        if (topology is WfcGrid grid)
        {
            var pos = grid.CellIdToPosition(collapsedCellId);
            foreach (var invalidator in _invalidators)
            {
                foreach (var invalidatedPos in invalidator.GetInvalidatedCells(pos, collapsedTile, grid))
                {
                    var cellId = grid.PositionToCellId(invalidatedPos);
                    if (!grid.GetCell(invalidatedPos).IsCollapsed())
                        _dirtyCellId.Add(cellId);
                }
            }
        }
    }

    /// <summary>
    /// Gets the frontier cell with lowest entropy using cell IDs.
    /// Only recomputes entropy for dirty cells.
    /// </summary>
    public int? GetLowestEntropyCellId(
        IWfcTopology topology,
        Func<int, float> computeEntropy,
        RandomNumberGenerator rng)
    {
        RefreshEntropies(topology, computeEntropy);

        var candidates = FindLowestEntropyFrontierCells(topology);

        // Fallback: any uncollapsed cell if no frontier (start of generation)
        if (candidates.Count == 0)
            AddFirstSelectableCell(topology, candidates);

        if (candidates.Count == 0)
            return null;

        return candidates[rng.RandiRange(0, candidates.Count - 1)];
    }

    private void RefreshEntropies(IWfcTopology topology, Func<int, float> computeEntropy)
    {
        // First call: build everything
        if (_needsFullRebuild)
        {
            RebuildAllCellId(topology, computeEntropy);
            _needsFullRebuild = false;
            return;
        }

        // Incremental: only update dirty cells
        UpdateDirtyCellId(topology, computeEntropy);
    }

    /// <summary>
    /// Finds the cells tied for minimum entropy among frontier cells (adjacent to collapsed).
    /// </summary>
    private List<int> FindLowestEntropyFrontierCells(IWfcTopology topology)
    {
        var minEntropy = float.MaxValue;
        var candidates = new List<int>();

        foreach (var kvp in _entropyCellId)
        {
            var cellId = kvp.Key;
            var entropy = kvp.Value;

            if (topology.GetCell(cellId).IsExcludedFromSelection())
                continue;

            // Only consider frontier cells (adjacent to collapsed)
            if (!topology.HasCollapsedNeighbor(cellId))
                continue;

            if (entropy < minEntropy)
            {
                minEntropy = entropy;
                candidates.Clear();
                candidates.Add(cellId);
            }
            else if (Mathf.IsEqualApprox(entropy, minEntropy))
            {
                candidates.Add(cellId);
            }
        }

        return candidates;
    }

    private void AddFirstSelectableCell(IWfcTopology topology, List<int> candidates)
    {
        foreach (var cellId in _entropyCellId.Keys)
        {
            if (topology.GetCell(cellId).IsExcludedFromSelection())
                continue;

            candidates.Add(cellId);
            return;
        }
    }

    private void RebuildAllCellId(IWfcTopology topology, Func<int, float> computeEntropy)
    {
        _entropyCellId.Clear();
        _dirtyCellId.Clear();

        foreach (var cellId in topology.GetAllCellIds())
        {
            var cell = topology.GetCell(cellId);
            if (!cell.IsExcludedFromSelection())
            {
                _entropyCellId[cellId] = computeEntropy(cellId);
            }
        }
    }

    private void UpdateDirtyCellId(IWfcTopology topology, Func<int, float> computeEntropy)
    {
        foreach (var cellId in _dirtyCellId)
        {
            var cell = topology.GetCell(cellId);
            if (cell.IsExcludedFromSelection())
            {
                _entropyCellId.Remove(cellId);
            }
            else
            {
                _entropyCellId[cellId] = computeEntropy(cellId);
            }
        }
        _dirtyCellId.Clear();
    }
}
