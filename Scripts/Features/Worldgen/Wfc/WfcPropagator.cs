using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
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
    private readonly ITileRegistry? _tileRegistry;

    public WfcPropagator(WfcAdjacencyRules rules, ITileRegistry? tileRegistry = null)
    {
        _rules = rules;
        _tileRegistry = tileRegistry;
    }

    /// <summary>
    /// Propagates constraints starting from a collapsed cell.
    /// Removes invalid options from neighbors based on adjacency rules.
    /// Uses 8-directional neighbors because the 2x2 window constraint affects diagonal cells.
    /// </summary>
    /// <param name="grid">The WFC grid</param>
    /// <param name="collapsedPos">Position of the cell that was just collapsed</param>
    /// <returns>Success if propagation completed, failure with contradiction position if not</returns>
    public PropagationResult Propagate(WfcGrid grid, Vector2I collapsedPos)
    {
        var workQueue = new Queue<Vector2I>();
        var inQueue = new HashSet<Vector2I>();
        var cellsUpdated = 0;

        // Start with ALL 8 neighbors of the collapsed cell
        // We need 8-directional because diagonal cells share 2x2 windows
        foreach (var neighbor in grid.GetNeighbors8(collapsedPos))
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

                // If this cell changed, ALL 8 neighbors may need updating
                // because they share 2x2 windows with this cell
                foreach (var neighbor in grid.GetNeighbors8(currentPos))
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

                // Use 8-directional for 2x2 window constraint propagation
                foreach (var neighbor in grid.GetNeighbors8(currentPos))
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
    /// Also applies transition spacing constraint to prevent rapid type changes.
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
        var result = validTiles ?? new HashSet<string>(_rules.AllTileIds);

        // Apply transition spacing constraint to prevent A-B-C patterns
        var spacingConstraint = ComputeTransitionSpacingConstraint(grid, pos);
        if (spacingConstraint != null)
        {
            result.IntersectWith(spacingConstraint);
        }

        return result;
    }

    /// <summary>
    /// Computes the transition spacing constraint for a position.
    /// Prevents 3+ distinct AUTO-TILE types in any 2x2 cell window that this cell participates in.
    /// A visual tile (dual-grid) samples 4 data cells at its corners - if those 4 cells have
    /// 3+ distinct auto-tile types, auto-tiling breaks because transitions only support 2 types.
    /// Non-auto-tile types can mix freely and don't contribute to this constraint.
    ///
    /// KEY INSIGHT: As soon as ANY 2 cells in a 2x2 window are collapsed with DIFFERENT auto-tile types,
    /// the remaining cells in that window are constrained to those 2 auto-tile types UNION all non-auto-tiles.
    /// We don't wait for 3 cells - by then it's too late.
    /// </summary>
    private HashSet<string>? ComputeTransitionSpacingConstraint(WfcGrid grid, Vector2I pos)
    {
        // This cell participates in 4 different 2x2 windows.
        // For each window, check if there are already 2 distinct AUTO-TILE types among collapsed cells.
        // If so, constrain this cell to those auto-tiles UNION all non-auto-tiles.
        // Non-auto-tiles can always mix freely regardless of window contents.

        // Pre-compute the set of all non-auto-tiles (reused across windows)
        var nonAutoTiles = new HashSet<string>();
        foreach (var tileId in _rules.AllTileIds)
        {
            if (!IsAutoTile(tileId))
                nonAutoTiles.Add(tileId);
        }

        HashSet<string>? constraint = null;

        // Check all 4 windows this cell participates in
        var windowOffsets = new Vector2I[][]
        {
            // Window where pos is SE corner (other cells: NW, NE, SW)
            new[] { new Vector2I(-1, -1), new Vector2I(0, -1), new Vector2I(-1, 0) },
            // Window where pos is SW corner (other cells: NE, NW, SE)
            new[] { new Vector2I(0, -1), new Vector2I(1, -1), new Vector2I(1, 0) },
            // Window where pos is NE corner (other cells: SW, NW, SE)
            new[] { new Vector2I(-1, 0), new Vector2I(-1, 1), new Vector2I(0, 1) },
            // Window where pos is NW corner (other cells: SE, NE, SW)
            new[] { new Vector2I(1, 0), new Vector2I(0, 1), new Vector2I(1, 1) }
        };

        foreach (var offsets in windowOffsets)
        {
            var windowTypes = new HashSet<string>();
            var windowValid = true;

            foreach (var offset in offsets)
            {
                var otherPos = pos + offset;
                if (!grid.IsInBounds(otherPos))
                {
                    // Window extends outside grid - skip this window entirely
                    windowValid = false;
                    break;
                }

                var otherCell = grid.GetCell(otherPos);
                if (otherCell.IsCollapsed())
                {
                    var tileId = otherCell.GetCollapsedTile();
                    // Only count auto-tiles for the constraint
                    if (IsAutoTile(tileId))
                    {
                        windowTypes.Add(tileId);
                    }
                }
            }

            if (!windowValid)
                continue;

            // If this window has 2+ distinct auto-tile types, constrain to those auto-tiles UNION all non-auto-tiles
            if (windowTypes.Count >= 2)
            {
                // Build constraint: windowTypes (auto-tiles) UNION nonAutoTiles
                var constraintSet = new HashSet<string>(windowTypes);
                constraintSet.UnionWith(nonAutoTiles);

                if (constraint == null)
                {
                    constraint = constraintSet;
                }
                else
                {
                    // Multiple windows constraining us - intersect
                    constraint.IntersectWith(constraintSet);
                }
            }
        }

        return constraint;
    }

    /// <summary>
    /// Checks if a tile ID represents an auto-tile type.
    /// Auto-tiles have variants for different neighbor configurations.
    /// </summary>
    private bool IsAutoTile(string tileId) => _tileRegistry?.GetTile(tileId)?.HasAutoTileVariants ?? false;
}
