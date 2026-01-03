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
public class ConnectivityConstraint : IWfcConstraint
{
    private readonly PassabilityGraph _graph;
    private readonly Func<string, bool> _isPassable;

    /// <summary>
    /// Tolerance for corridor path detection. Higher values create wider corridors.
    /// Default 1 means positions adjacent to the path are also considered.
    /// </summary>
    public int CorridorTolerance { get; set; } = 1;

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
        // If there are disconnected passable regions, ban impassable tiles on the corridor path
        if (_graph.HasDisconnectedRegions() && _graph.IsOnCorridorPath(context.Position, CorridorTolerance))
        {
            // This position is on the path between disconnected regions - must be passable
            return 0.0f;
        }

        // === REACTIVE: Prevent disconnection at bridge positions ===
        var passableNeighbors = GetPassableNeighbors(context.Position, context.Grid);

        // 0-1 passable neighbors: can't be a bridge
        if (passableNeighbors.Count <= 1)
            return 1.0f;

        // 2+ passable neighbors: check if they're already connected
        if (AreAllNeighborsConnected(passableNeighbors))
            return 1.0f;

        // Passable neighbors are from different components - this is a bridge position
        return 0.0f;
    }

    private bool AreAllNeighborsConnected(List<Vector2I> neighbors)
    {
        if (neighbors.Count <= 1)
            return true;

        var first = neighbors[0];
        var targets = new HashSet<Vector2I>(neighbors);
        targets.Remove(first);

        var visited = new HashSet<Vector2I> { first };
        var queue = new Queue<Vector2I>();
        queue.Enqueue(first);

        while (queue.Count > 0 && targets.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var adjacent in _graph.GetNeighbors(current))
            {
                if (visited.Add(adjacent))
                {
                    targets.Remove(adjacent);
                    queue.Enqueue(adjacent);
                }
            }
        }

        return targets.Count == 0;
    }

    private List<Vector2I> GetPassableNeighbors(Vector2I position, WfcGrid grid)
    {
        var result = new List<Vector2I>();

        foreach (var neighborPos in grid.GetNeighbors(position))
        {
            var neighborTile = grid.GetCollapsedTileAt(neighborPos);
            if (neighborTile != null && _isPassable(neighborTile))
            {
                result.Add(neighborPos);
            }
        }

        return result;
    }
}
