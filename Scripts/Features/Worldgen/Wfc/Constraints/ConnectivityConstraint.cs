using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Connectivity;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Constraint that prevents tile placements which would disconnect passable regions.
/// Uses articulation point detection to identify positions that, if made impassable,
/// would split the passable region into disconnected components.
/// </summary>
/// <remarks>
/// Return value semantics:
/// <list type="bullet">
///   <item><description>1.0: Tile can be placed (passable tile, or impassable at non-critical position)</description></item>
///   <item><description>0.0: Hard ban (impassable tile at position critical for connectivity)</description></item>
/// </list>
///
/// The constraint works by temporarily adding the candidate position to the passability graph,
/// connecting it to any collapsed passable neighbors, checking if it would be an articulation point,
/// then cleaning up. If the position IS an articulation point when treated as passable, making it
/// impassable would disconnect the graph - so we ban impassable tiles at such positions.
/// </remarks>
public class ConnectivityConstraint : IWfcConstraint
{
    private readonly PassabilityGraph _graph;
    private readonly Func<string, bool> _isPassable;

    /// <summary>
    /// Creates a connectivity constraint.
    /// </summary>
    /// <param name="graph">The passability graph tracking collapsed passable tiles.</param>
    /// <param name="isPassable">Function that returns true if a tile ID is passable.</param>
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

        // Impassable tile being considered at this position
        // Check if this position would be critical for connectivity if it were passable

        // First, find collapsed passable neighbors
        var passableNeighbors = GetPassableNeighbors(context.Position, context.Grid);

        // CRITICAL: No passable neighbors means this position is isolated from the passable region.
        // We MUST ban impassable tiles here, otherwise they create barriers that fragment the map
        // into disconnected islands. Impassable tiles ONLY allowed at the edge of passable region.
        if (passableNeighbors.Count == 0)
            return 0.0f;

        // Temporarily add this position as passable to check if it's an articulation point
        var wasInGraph = _graph.ContainsNode(context.Position);
        if (!wasInGraph)
        {
            _graph.AddNode(context.Position);
            foreach (var neighbor in passableNeighbors)
            {
                _graph.AddEdge(context.Position, neighbor);
            }
        }

        var isArticulation = _graph.IsArticulationPoint(context.Position);

        // Clean up temporary addition
        if (!wasInGraph)
        {
            _graph.RemoveNode(context.Position);
        }

        // If this position is an articulation point when passable, ban impassable tiles here
        return isArticulation ? 0.0f : 1.0f;
    }

    /// <summary>
    /// Gets all collapsed passable neighbors of a position.
    /// </summary>
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
