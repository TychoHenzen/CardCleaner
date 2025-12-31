using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Result of a propagation operation.
/// </summary>
public readonly struct PropagationResult
{
    public bool Success { get; }
    public Vector2I? ContradictionPosition { get; }
    public int CellsUpdated { get; }

    public PropagationResult(bool success, int cellsUpdated, Vector2I? contradictionPos = null)
    {
        Success = success;
        CellsUpdated = cellsUpdated;
        ContradictionPosition = contradictionPos;
    }

    public static PropagationResult Succeeded(int cellsUpdated) => new(true, cellsUpdated);
    public static PropagationResult Failed(Vector2I pos) => new(false, 0, pos);
}

/// <summary>
/// Propagates constraints through the WFC grid after a cell collapse.
/// Uses work queue to iteratively reduce neighbor possibilities.
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
    /// <param name="grid">The WFC grid</param>
    /// <param name="collapsedPos">Position of the cell that was just collapsed</param>
    /// <returns>Success if propagation completed, failure with contradiction position if not</returns>
    public PropagationResult Propagate(WfcGrid grid, Vector2I collapsedPos)
    {
        var workQueue = new Queue<Vector2I>();
        var inQueue = new HashSet<Vector2I>();
        var cellsUpdated = 0;

        // Start with neighbors of the collapsed cell
        foreach (var neighbor in grid.GetNeighbors(collapsedPos))
        {
            workQueue.Enqueue(neighbor);
            inQueue.Add(neighbor);
        }

        while (workQueue.Count > 0)
        {
            var currentPos = workQueue.Dequeue();
            inQueue.Remove(currentPos);

            var currentCell = grid.GetCell(currentPos);

            // Skip already collapsed cells
            if (currentCell.IsCollapsed())
                continue;

            // Compute valid tiles for this cell based on all collapsed neighbors
            var validTiles = ComputeValidTiles(grid, currentPos);

            // Intersect with current possibilities
            var changed = currentCell.IntersectWith(validTiles);

            if (currentCell.IsContradiction())
            {
                return PropagationResult.Failed(currentPos);
            }

            if (changed)
            {
                cellsUpdated++;

                // If this cell changed, its neighbors may need updating
                foreach (var neighbor in grid.GetNeighbors(currentPos))
                {
                    var neighborCell = grid.GetCell(neighbor);
                    if (!neighborCell.IsCollapsed() && !inQueue.Contains(neighbor))
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
    /// Propagates constraints for all cells in the grid.
    /// Useful after initial setup or when multiple cells need updating.
    /// </summary>
    public PropagationResult PropagateAll(WfcGrid grid)
    {
        var workQueue = new Queue<Vector2I>();
        var inQueue = new HashSet<Vector2I>();
        var cellsUpdated = 0;

        // Add all uncollapsed cells to work queue
        foreach (var pos in grid.GetAllPositions())
        {
            if (!grid.GetCell(pos).IsCollapsed())
            {
                workQueue.Enqueue(pos);
                inQueue.Add(pos);
            }
        }

        while (workQueue.Count > 0)
        {
            var currentPos = workQueue.Dequeue();
            inQueue.Remove(currentPos);

            var currentCell = grid.GetCell(currentPos);

            if (currentCell.IsCollapsed())
                continue;

            var validTiles = ComputeValidTiles(grid, currentPos);
            var changed = currentCell.IntersectWith(validTiles);

            if (currentCell.IsContradiction())
            {
                return PropagationResult.Failed(currentPos);
            }

            if (changed)
            {
                cellsUpdated++;

                foreach (var neighbor in grid.GetNeighbors(currentPos))
                {
                    var neighborCell = grid.GetCell(neighbor);
                    if (!neighborCell.IsCollapsed() && !inQueue.Contains(neighbor))
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
    /// Computes which tiles are valid at a position given its neighbors' states.
    /// A tile is valid if it can be adjacent to ALL collapsed neighbors.
    /// For uncollapsed neighbors, we use the union of valid neighbors.
    /// </summary>
    private HashSet<string> ComputeValidTiles(WfcGrid grid, Vector2I pos)
    {
        HashSet<string>? validTiles = null;

        foreach (var neighborPos in grid.GetNeighbors(pos))
        {
            var neighborCell = grid.GetCell(neighborPos);
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
