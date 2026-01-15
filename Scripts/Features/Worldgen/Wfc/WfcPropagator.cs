using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Result of a propagation operation.
/// </summary>
public readonly struct PropagationResult
{
    public bool Success { get; }
    public int? ContradictionCellId { get; }
    public int CellsUpdated { get; }

    public PropagationResult(bool success, int cellsUpdated, int? contradictionCellId = null)
    {
        Success = success;
        CellsUpdated = cellsUpdated;
        ContradictionCellId = contradictionCellId;
    }

    public static PropagationResult Succeeded(int cellsUpdated) => new(true, cellsUpdated);
    public static PropagationResult Failed(int cellId) => new(false, 0, cellId);
}

/// <summary>
/// Propagates constraints through the WFC topology after a cell collapse.
/// Uses work queue to iteratively reduce neighbor possibilities.
/// Topology-agnostic: works with rectangular grids, irregular meshes, or any IWfcTopology.
/// </summary>
public class WfcPropagator
{
    private readonly WfcAdjacencyRules _rules;

    public WfcPropagator(WfcAdjacencyRules rules)
    {
        _rules = rules;
    }

    /// <summary>
    /// Propagates constraints starting from a collapsed cell.
    /// Removes invalid options from neighbors based on adjacency rules.
    /// </summary>
    /// <param name="topology">The WFC topology</param>
    /// <param name="collapsedCellId">Cell ID that was just collapsed</param>
    /// <returns>Success if propagation completed, failure with contradiction cell if not</returns>
    public PropagationResult Propagate(IWfcTopology topology, int collapsedCellId)
    {
        var workQueue = new Queue<int>();
        var inQueue = new HashSet<int>();
        var cellsUpdated = 0;

        // Start with all neighbors of the collapsed cell
        foreach (var neighbor in topology.GetNeighbors(collapsedCellId))
        {
            workQueue.Enqueue(neighbor);
            inQueue.Add(neighbor);
        }

        while (workQueue.Count > 0)
        {
            var currentCellId = workQueue.Dequeue();
            inQueue.Remove(currentCellId);

            var currentCell = topology.GetCell(currentCellId);

            // Skip already collapsed or reserved cells
            if (currentCell.IsCollapsed() || currentCell.IsReserved)
                continue;

            // Compute valid tiles for this cell based on all collapsed neighbors
            var validTiles = ComputeValidTiles(topology, currentCellId);

            // Intersect with current possibilities
            var changed = currentCell.IntersectWith(validTiles);

            if (currentCell.IsContradiction())
            {
                return PropagationResult.Failed(currentCellId);
            }

            if (changed)
            {
                cellsUpdated++;

                // If this cell changed, all neighbors may need updating
                foreach (var neighbor in topology.GetNeighbors(currentCellId))
                {
                    var neighborCell = topology.GetCell(neighbor);
                    if (!neighborCell.IsCollapsed() && !neighborCell.IsReserved && !inQueue.Contains(neighbor))
                    {
                        workQueue.Enqueue(neighbor);
                        inQueue.Add(neighbor);
                    }
                }
            }
        }

        return PropagationResult.Succeeded(cellsUpdated);
    }

    /// <summary>
    /// Propagates constraints for all cells in the topology.
    /// Useful after initial setup or when multiple cells need updating.
    /// </summary>
    public PropagationResult PropagateAll(IWfcTopology topology)
    {
        var workQueue = new Queue<int>();
        var inQueue = new HashSet<int>();
        var cellsUpdated = 0;

        // Add all uncollapsed and unreserved cells to work queue
        foreach (var cellId in topology.GetAllCellIds())
        {
            var cell = topology.GetCell(cellId);
            if (!cell.IsCollapsed() && !cell.IsReserved)
            {
                workQueue.Enqueue(cellId);
                inQueue.Add(cellId);
            }
        }

        while (workQueue.Count > 0)
        {
            var currentCellId = workQueue.Dequeue();
            inQueue.Remove(currentCellId);

            var currentCell = topology.GetCell(currentCellId);

            // Skip collapsed or reserved cells
            if (currentCell.IsCollapsed() || currentCell.IsReserved)
                continue;

            var validTiles = ComputeValidTiles(topology, currentCellId);
            var changed = currentCell.IntersectWith(validTiles);

            if (currentCell.IsContradiction())
            {
                return PropagationResult.Failed(currentCellId);
            }

            if (changed)
            {
                cellsUpdated++;

                foreach (var neighbor in topology.GetNeighbors(currentCellId))
                {
                    var neighborCell = topology.GetCell(neighbor);
                    if (!neighborCell.IsCollapsed() && !neighborCell.IsReserved && !inQueue.Contains(neighbor))
                    {
                        workQueue.Enqueue(neighbor);
                        inQueue.Add(neighbor);
                    }
                }
            }
        }

        return PropagationResult.Succeeded(cellsUpdated);
    }

    /// <summary>
    /// Computes which tiles are valid at a cell given its neighbors' states.
    /// A tile is valid if it can be adjacent to ALL collapsed neighbors.
    /// For uncollapsed neighbors, we use the union of valid neighbors.
    /// </summary>
    private HashSet<string> ComputeValidTiles(IWfcTopology topology, int cellId)
    {
        HashSet<string>? validTiles = null;

        foreach (var neighborId in topology.GetNeighbors(cellId))
        {
            var neighborCell = topology.GetCell(neighborId);
            HashSet<string> neighborConstraint;

            if (neighborCell.IsCollapsed())
            {
                // Collapsed neighbor: only tiles that can be adjacent to it are valid
                var collapsedTile = neighborCell.GetCollapsedTile();
                neighborConstraint = new HashSet<string>(_rules.GetValidNeighbors(collapsedTile));
            }
            else
            {
                // Uncollapsed neighbor: union of valid neighbors for all its possibilities
                neighborConstraint = new HashSet<string>();
                foreach (var possibleTile in neighborCell.GetPossibleTiles())
                {
                    foreach (var valid in _rules.GetValidNeighbors(possibleTile))
                    {
                        neighborConstraint.Add(valid);
                    }
                }
            }

            if (validTiles == null)
            {
                validTiles = neighborConstraint;
            }
            else
            {
                validTiles.IntersectWith(neighborConstraint);
            }
        }

        // If no neighbors (shouldn't happen in practice), all tiles are valid
        return validTiles ?? new HashSet<string>(_rules.AllTileIds);
    }
}
