using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;

/// <summary>
/// Builds the WFC-based mesh terrain generator from the compiled transition data.
/// </summary>
internal static class TerrainGeneratorFactory
{
    internal static MeshTerrainGenerator Create(ITileRegistry? tileRegistry)
    {
        // The generator's registry drives the background and foreground solves. The caller's registry only
        // decides passability in the terrain-type map.
        var generatorRegistry = new TileRegistry();
        var solver = IWfcTerrainSolver.Create(null, generatorRegistry);
        var tileToTerrainType = AssignTerrainTypes(solver.RuleTileIds, tileRegistry);
        var generator = new MeshTerrainGenerator(solver, tileToTerrainType, generatorRegistry);

        // Create and register biome registry for card-based generation
        var biomeRegistry = new BiomeRegistry();
        biomeRegistry.RegisterDefaultBiomes();
        generator.SetBiomeRegistry(biomeRegistry);

        return generator;
    }

    /// <summary>
    /// Passable tiles get unique terrain types starting at 1; impassable tiles get 0.
    /// </summary>
    private static Dictionary<string, int> AssignTerrainTypes(
        IReadOnlyList<string> allTerrainIds,
        ITileRegistry? tileRegistry)
    {
        var tileToTerrainType = new Dictionary<string, int>();
        var nextTerrainType = 1;

        foreach (var terrainId in allTerrainIds)
        {
            var tileDef = tileRegistry?.GetTile(terrainId);
            bool isPassable = tileDef?.IsPassable ?? InferPassability(terrainId);

            tileToTerrainType[terrainId] = isPassable ? nextTerrainType++ : 0;
        }

        return tileToTerrainType;
    }

    private static bool InferPassability(string terrainId)
    {
        var lower = terrainId.ToLowerInvariant();
        return !lower.Contains("rock") &&
               !lower.Contains("wall") &&
               !lower.Contains("water") &&
               !lower.Contains("hedge") &&
               !lower.Contains("lava");
    }
}
