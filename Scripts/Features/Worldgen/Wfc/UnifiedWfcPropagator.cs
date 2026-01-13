using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Result of a unified propagation operation.
/// </summary>
public readonly struct UnifiedPropagationResult
{
    public bool Success { get; }
    public int? ContradictionCellId { get; }
    public int CellsUpdated { get; }

    public UnifiedPropagationResult(bool success, int cellsUpdated, int? contradictionCellId = null)
    {
        Success = success;
        CellsUpdated = cellsUpdated;
        ContradictionCellId = contradictionCellId;
    }

    public static UnifiedPropagationResult Succeeded(int cellsUpdated) => new(true, cellsUpdated);
    public static UnifiedPropagationResult Failed(int cellId) => new(false, 0, cellId);
}

/// <summary>
/// Interface for adjacency rules that can work with any grid type.
/// </summary>
public interface IAdjacencyRules
{
    /// <summary>
    /// Gets all tiles that can be adjacent to the given tile.
    /// </summary>
    IReadOnlySet<string> GetValidNeighbors(string tileId);

    /// <summary>
    /// Gets all tile IDs known to the rules.
    /// </summary>
    IReadOnlySet<string> AllTileIds { get; }
}

/// <summary>
/// Adapter to make WfcAdjacencyRules implement IAdjacencyRules.
/// </summary>
public class AdjacencyRulesAdapter : IAdjacencyRules
{
    private readonly WfcAdjacencyRules _rules;

    public AdjacencyRulesAdapter(WfcAdjacencyRules rules)
    {
        _rules = rules;
    }

    public IReadOnlySet<string> GetValidNeighbors(string tileId) => _rules.GetValidNeighbors(tileId);
    public IReadOnlySet<string> AllTileIds => _rules.AllTileIds;
}

/// <summary>
/// Permissive adjacency rules that allow any tile to neighbor any other.
/// Useful for WFC without hard adjacency constraints.
/// </summary>
public class PermissiveAdjacencyRules : IAdjacencyRules
{
    private readonly HashSet<string> _allTileIds;

    public PermissiveAdjacencyRules(IEnumerable<string> tileIds)
    {
        _allTileIds = new HashSet<string>(tileIds);
    }

    public IReadOnlySet<string> GetValidNeighbors(string tileId) => _allTileIds;
    public IReadOnlySet<string> AllTileIds => _allTileIds;
}

/// <summary>
/// Unified WFC propagator that works with any IWfcGrid implementation.
/// Uses work queue to iteratively reduce neighbor possibilities.
/// </summary>
public class UnifiedWfcPropagator
{
    private readonly IAdjacencyRules _rules;
    private readonly bool _use8WayPropagation;

    /// <summary>
    /// Creates a unified propagator.
    /// </summary>
    /// <param name="rules">Adjacency rules defining valid tile neighbors</param>
    /// <param name="use8WayPropagation">
    /// If true and grid supports it (IWfcGrid8Way), propagates to 8-directional neighbors.
    /// Important for dual-grid rendering where diagonal cells share 2x2 windows.
    /// </param>
    public UnifiedWfcPropagator(IAdjacencyRules rules, bool use8WayPropagation = true)
    {
        _rules = rules;
        _use8WayPropagation = use8WayPropagation;
    }

    /// <summary>
    /// Propagates constraints starting from a collapsed cell.
    /// </summary>
    public UnifiedPropagationResult Propagate(IWfcGrid grid, int collapsedCellId)
    {
        var workQueue = new Queue<int>();
        var inQueue = new HashSet<int>();
        var cellsUpdated = 0;

        // Start with neighbors of collapsed cell
        foreach (var neighbor in GetNeighborsForPropagation(grid, collapsedCellId))
        {
            workQueue.Enqueue(neighbor);
            inQueue.Add(neighbor);
        }

        while (workQueue.Count > 0)
        {
            var currentCellId = workQueue.Dequeue();
            inQueue.Remove(currentCellId);

            var currentCell = grid.GetCell(currentCellId);

            // Skip already collapsed cells
            if (currentCell.IsCollapsed())
                continue;

            // Compute valid tiles for this cell based on all collapsed neighbors
            var validTiles = ComputeValidTiles(grid, currentCellId);

            // Intersect with current possibilities
            var changed = currentCell.IntersectWith(validTiles);

            if (currentCell.IsContradiction())
            {
                return UnifiedPropagationResult.Failed(currentCellId);
            }

            if (changed)
            {
                cellsUpdated++;

                // If this cell changed, neighbors may need updating
                foreach (var neighbor in GetNeighborsForPropagation(grid, currentCellId))
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

        return UnifiedPropagationResult.Succeeded(cellsUpdated);
    }

    /// <summary>
    /// Propagates constraints for all cells in the grid.
    /// </summary>
    public UnifiedPropagationResult PropagateAll(IWfcGrid grid)
    {
        var workQueue = new Queue<int>();
        var inQueue = new HashSet<int>();
        var cellsUpdated = 0;

        // Add all uncollapsed cells to work queue
        foreach (var cellId in grid.GetAllCellIds())
        {
            var cell = grid.GetCell(cellId);
            if (!cell.IsCollapsed())
            {
                workQueue.Enqueue(cellId);
                inQueue.Add(cellId);
            }
        }

        while (workQueue.Count > 0)
        {
            var currentCellId = workQueue.Dequeue();
            inQueue.Remove(currentCellId);

            var currentCell = grid.GetCell(currentCellId);

            // Skip collapsed cells
            if (currentCell.IsCollapsed())
                continue;

            var validTiles = ComputeValidTiles(grid, currentCellId);
            var changed = currentCell.IntersectWith(validTiles);

            if (currentCell.IsContradiction())
            {
                return UnifiedPropagationResult.Failed(currentCellId);
            }

            if (changed)
            {
                cellsUpdated++;

                foreach (var neighbor in GetNeighborsForPropagation(grid, currentCellId))
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

        return UnifiedPropagationResult.Succeeded(cellsUpdated);
    }

    /// <summary>
    /// Gets neighbors for propagation.
    /// Uses 8-way if enabled and grid supports it, otherwise 4-way.
    /// </summary>
    private IEnumerable<int> GetNeighborsForPropagation(IWfcGrid grid, int cellId)
    {
        if (_use8WayPropagation && grid is IWfcGrid8Way grid8Way)
        {
            return grid8Way.GetNeighbors8(cellId);
        }
        return grid.GetNeighbors(cellId);
    }

    /// <summary>
    /// Computes which tiles are valid at a cell given its neighbors' states.
    /// A tile is valid if it can be adjacent to ALL collapsed neighbors.
    /// </summary>
    private HashSet<string> ComputeValidTiles(IWfcGrid grid, int cellId)
    {
        HashSet<string>? validTiles = null;

        // Use 4-way neighbors for adjacency constraint (orthogonal adjacency)
        foreach (var neighborId in grid.GetNeighbors(cellId))
        {
            var neighborCell = grid.GetCell(neighborId);
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

        // If no neighbors, all tiles are valid
        return validTiles ?? new HashSet<string>(_rules.AllTileIds);
    }
}
