using System;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Connectivity;

/// <summary>
/// Keeps a <see cref="PassabilityGraph"/> in step with collapsed grid cells so that
/// connectivity constraints query the generated map rather than an empty graph.
/// </summary>
internal sealed class PassabilityGraphUpdater
{
    private readonly PassabilityGraph _graph;
    private readonly Func<string, bool> _isPassable;

    internal PassabilityGraphUpdater(PassabilityGraph graph, Func<string, bool> isPassable)
    {
        _graph = graph;
        _isPassable = isPassable;
    }

    /// <summary>
    /// Empties the graph so a new solve attempt starts without the previous attempt's cells,
    /// then re-adds the passable cells the topology already has collapsed.
    /// </summary>
    internal void Reset(IWfcTopology topology)
    {
        _graph.Clear();

        if (topology is not WfcGrid grid)
            return;

        foreach (var position in grid.GetAllPositions())
        {
            var tileId = grid.GetCollapsedTileAt(position);
            if (tileId != null)
                OnCellCollapsed(position, tileId, grid);
        }
    }

    /// <summary>
    /// Adds a collapsed passable cell to the graph and connects it to passable edge-sharing neighbors.
    /// </summary>
    internal void OnCellCollapsed(Vector2I position, string tileId, WfcGrid grid)
    {
        if (!_isPassable(tileId))
            return;

        _graph.AddNode(position);

        foreach (var neighborPos in grid.GetNeighbors(position))
        {
            var neighborTile = grid.GetCollapsedTileAt(neighborPos);
            if (neighborTile != null && _isPassable(neighborTile))
                _graph.AddEdge(position, neighborPos);
        }
    }
}
