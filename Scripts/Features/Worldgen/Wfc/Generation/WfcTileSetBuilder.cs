using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Contracts;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Chooses which adjacency-rule tiles take part in a generation.
/// </summary>
internal sealed class WfcTileSetBuilder
{
    private readonly WfcAdjacencyRules _adjacencyRules;
    private readonly IWfcTileCatalog? _tileCatalog;

    internal WfcTileSetBuilder(WfcAdjacencyRules adjacencyRules, IWfcTileCatalog? tileCatalog)
    {
        _adjacencyRules = adjacencyRules;
        _tileCatalog = tileCatalog;
    }

    internal WfcTileSets ForBiome(BiomeDefinition biome)
    {
        return Collect((catalog, tileId) => catalog.IsAllowedInBiome(tileId, biome.Id));
    }

    internal WfcTileSets ForAllBiomes(BiomeRegistry registry, Func<string, bool>? tileFilter = null)
    {
        // Get all biome IDs for checking tile compatibility
        var biomeIds = registry.GetAllBiomeIds().ToList();

        return Collect((catalog, tileId) =>
        {
            // Apply tile filter if provided
            if (tileFilter != null && !tileFilter(tileId))
                return false;

            // Allowed in ANY biome. IsAllowedInBiome returns true if AllowedBiomes is null (universal)
            return biomeIds.Any(biomeId => catalog.IsAllowedInBiome(tileId, biomeId));
        });
    }

    private WfcTileSets Collect(Func<IWfcTileCatalog, string, bool> isIncluded)
    {
        var allTiles = new HashSet<string>();

        foreach (var tileId in _adjacencyRules.AllTileIds)
        {
            if (_tileCatalog == null || !_tileCatalog.Contains(tileId) || !isIncluded(_tileCatalog, tileId))
                continue;

            allTiles.Add(tileId);
        }

        return new WfcTileSets(allTiles);
    }
}
