using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Contracts;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Chooses which adjacency-rule tiles take part in a generation, and which of them are passable.
/// </summary>
internal sealed class WfcTileSetBuilder
{
    private readonly WfcAdjacencyRules _adjacencyRules;
    private readonly ITileRegistry? _tileRegistry;

    internal WfcTileSetBuilder(WfcAdjacencyRules adjacencyRules, ITileRegistry? tileRegistry)
    {
        _adjacencyRules = adjacencyRules;
        _tileRegistry = tileRegistry;
    }

    internal WfcTileSets ForBiome(BiomeDefinition biome)
    {
        return Collect(tileDef => tileDef.IsAllowedInBiome(biome.Id));
    }

    internal WfcTileSets ForAllBiomes(BiomeRegistry registry, Func<TileDefinition, bool>? tileFilter = null)
    {
        // Get all biome IDs for checking tile compatibility
        var biomeIds = registry.GetAllBiomeIds().ToList();

        return Collect(tileDef =>
        {
            // Apply tile filter if provided
            if (tileFilter != null && !tileFilter(tileDef))
                return false;

            // Allowed in ANY biome. IsAllowedInBiome returns true if AllowedBiomes is null (universal)
            return biomeIds.Any(biomeId => tileDef.IsAllowedInBiome(biomeId));
        });
    }

    private WfcTileSets Collect(Func<TileDefinition, bool> isIncluded)
    {
        var allTiles = new HashSet<string>();
        var passableTiles = new HashSet<string>();

        foreach (var tileId in _adjacencyRules.AllTileIds)
        {
            var tileDef = _tileRegistry?.GetTile(tileId);
            if (tileDef == null || !isIncluded(tileDef))
                continue;

            allTiles.Add(tileId);

            if (tileDef.IsPassable)
                passableTiles.Add(tileId);
        }

        return new WfcTileSets(allTiles, passableTiles);
    }
}
