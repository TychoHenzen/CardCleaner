using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Connectivity;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Constraint that ensures passable tile connectivity during WFC generation.
/// Two-pronged approach:
/// 1. REACTIVE: Bans impassable tiles at bridge positions between disconnected passable regions
/// 2. PROACTIVE: When disconnected regions exist, bans impassable tiles on the corridor path between them
/// Movement is cardinal-only (no diagonals).
/// </summary>
public class ConnectivityConstraint : IWfcConstraint, IEntropyInvalidator
{
    private readonly PassabilityGraph _graph;
    private readonly Func<string, bool> _isPassable;

    /// <summary>
    /// Tolerance for corridor path detection. Higher values create wider corridors.
    /// Default 1 means positions adjacent to the path are also considered.
    /// </summary>
    public int CorridorTolerance { get; set; } = 1;

    /// <summary>Enables proactive corridor forcing between disconnected regions.</summary>
    public bool EnableProactiveCorridors { get; set; } = false;

    public ConnectivityConstraint(PassabilityGraph graph, Func<string, bool> isPassable)
    {
        _graph = graph;
        _isPassable = isPassable;
    }

    /// <inheritdoc />
    public float GetProbabilityModifier(WfcConstraintContext context)
    {
        // Passable tiles extend connectivity - never banned
        if (_isPassable(context.TileId))
            return 1.0f;

        // === PROACTIVE: Ensure corridor between disconnected regions ===
        // This is expensive (O(V²) closest pair calculation) so disabled by default.
        // Reactive bridge detection below is usually sufficient for connectivity.
        // Note: Only works for grid topologies (uses positions for corridor calculation)
        if (EnableProactiveCorridors && context.Topology is WfcGrid &&
            _graph.HasDisconnectedRegions() &&
            _graph.IsOnCorridorPath(context.Position, CorridorTolerance))
        {
            return 0.0f;
        }

        // === REACTIVE: Prevent disconnection at bridge positions ===
        var passableNeighbors = GetPassableNeighbors(context);

        // 0-1 passable neighbors: can't be a bridge
        if (passableNeighbors.Count <= 1)
            return 1.0f;

        // 2+ passable neighbors: check if they're already connected (O(α(n)) using union-find)
        if (AreAllNeighborsConnected(passableNeighbors))
            return 1.0f;

        // Passable neighbors are from different components - this is a bridge position
        return 0.0f;
    }

    private bool AreAllNeighborsConnected(List<Vector2I> neighbors)
    {
        if (neighbors.Count <= 1)
            return true;

        // Use O(α(n)) union-find instead of O(V) BFS
        var first = neighbors[0];
        for (var i = 1; i < neighbors.Count; i++)
        {
            if (!_graph.AreConnected(first, neighbors[i]))
                return false;
        }

        return true;
    }

    private List<Vector2I> GetPassableNeighbors(WfcConstraintContext context)
    {
        var result = new List<Vector2I>(4);

        // Only works for grid topologies (PassabilityGraph uses positions)
        if (context.Topology is not WfcGrid grid)
            return result;

        // Use precomputed neighbor info if available
        if (context.NeighborInfo.HasValue)
        {
            foreach (var kvp in context.NeighborInfo.Value.Neighbors)
            {
                if (_isPassable(kvp.Value))
                {
                    // Convert cell ID to position for PassabilityGraph
                    var neighborPos = grid.CellIdToPosition(kvp.Key);
                    result.Add(neighborPos);
                }
            }
            return result;
        }

        // Fallback: iterate neighbors directly using topology
        foreach (var neighborId in context.Topology.GetNeighbors(context.CellId))
        {
            var neighborTile = context.Topology.GetCollapsedTileAt(neighborId);
            if (neighborTile != null && _isPassable(neighborTile))
            {
                var neighborPos = grid.CellIdToPosition(neighborId);
                result.Add(neighborPos);
            }
        }

        return result;
    }

    /// <summary>
    /// Returns cells affected by connectivity changes beyond immediate neighbors.
    /// When a passable tile is collapsed, the graph changes, potentially affecting
    /// bridge detection at cells adjacent to the connected region.
    /// </summary>
    public IEnumerable<Vector2I> GetInvalidatedCells(Vector2I collapsedPos, string collapsedTile, WfcGrid grid)
    {
        // Only passable tiles affect the connectivity graph
        if (!_isPassable(collapsedTile))
            return Array.Empty<Vector2I>();

        // When a passable tile is placed, cells adjacent to the expanded region
        // might no longer be bridges. Mark a wider area as dirty.
        var invalidated = new HashSet<Vector2I>();

        // Mark all cells within 2 steps of the collapsed position
        // This captures cells that might have been bridges that are now resolved
        foreach (var neighbor in grid.GetNeighbors(collapsedPos))
        {
            if (!grid.GetCell(neighbor).IsCollapsed())
                invalidated.Add(neighbor);

            foreach (var twoHop in grid.GetNeighbors(neighbor))
            {
                if (twoHop != collapsedPos && !grid.GetCell(twoHop).IsCollapsed())
                    invalidated.Add(twoHop);
            }
        }

        return invalidated;
    }
}
