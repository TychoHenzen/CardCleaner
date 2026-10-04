using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshTerrain;

/// <summary>
/// Chooses which registry tiles direct mesh WFC may place on a vertex: terrain-layer tiles allowed
/// in at least one biome. Structures, decorations and effects are placed by their own systems.
/// </summary>
internal static class MeshForegroundTileCandidates
{
    internal static bool IsCandidate(TileDefinition tile) => tile.Layer == TileLayer.Terrain;

    internal static HashSet<string> Collect(ITileRegistry tileRegistry, BiomeRegistry biomeRegistry)
    {
        var biomeIds = biomeRegistry.GetAllBiomeIds().ToList();

        return tileRegistry.GetAllTiles()
            .Where(IsCandidate)
            .Where(tile => biomeIds.Any(tile.IsAllowedInBiome))
            .Select(tile => tile.Id)
            .ToHashSet();
    }
}
