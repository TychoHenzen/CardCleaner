using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Configures adjacency rules so that all non-auto-tiles (gap tiles) can be adjacent to each other.
/// This is necessary for the background layer of two-phase WFC where only gap tiles are used.
/// Without this, gap tiles can only be adjacent to auto-tiles (from transition definitions),
/// causing WFC to collapse everything to a single tile type.
/// </summary>
internal static class GapTileAdjacencyConfigurator
{
    /// <summary>
    /// Adds the gap-tile adjacencies. A tile filter limits which registry tiles take part; by default all do.
    /// </summary>
    internal static void Configure(
        WfcAdjacencyRules adjacencyRules,
        ITileRegistry tileRegistry,
        Func<TileDefinition, bool>? tileFilter = null,
        bool logSummary = true)
    {
        var gapTiles = new List<string>();
        var autoTiles = new List<string>();

        foreach (var tile in tileRegistry.GetAllTiles())
        {
            if (tileFilter != null && !tileFilter(tile))
                continue;

            if (!tile.HasAutoTileVariants)
                gapTiles.Add(tile.Id);
            else
                autoTiles.Add(tile.Id);
        }

        if (gapTiles.Count > 0)
            AllowGapTileAdjacencies(adjacencyRules, gapTiles, autoTiles, logSummary);

        // Add auto-tiles with self-adjacency if not already in rules
        foreach (var autoTile in autoTiles)
        {
            adjacencyRules.EnsureSelfAdjacency(autoTile);
        }
    }

    private static void AllowGapTileAdjacencies(
        WfcAdjacencyRules adjacencyRules,
        List<string> gapTiles,
        List<string> autoTiles,
        bool logSummary)
    {
        // Gap tiles can all be adjacent to each other and to any auto-tile
        adjacencyRules.AddMutualAdjacencies(gapTiles);

        foreach (var gapTile in gapTiles)
        {
            foreach (var autoTile in autoTiles)
            {
                adjacencyRules.AddAdjacency(gapTile, autoTile);
            }
        }

        if (logSummary)
        {
            GD.Print(
                $"[WFC] Configured {gapTiles.Count} gap tiles for adjacency " +
                $"(can be next to {autoTiles.Count} auto-tiles)");
        }
    }
}
