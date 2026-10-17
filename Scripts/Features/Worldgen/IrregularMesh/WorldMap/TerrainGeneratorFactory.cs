using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshTerrain;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;

/// <summary>
/// Builds the WFC-based mesh terrain generator from the compiled transition data and the registries it is given.
/// </summary>
internal static class TerrainGeneratorFactory
{
    internal static MeshTerrainGenerator Create(
        ITileRegistry tileRegistry,
        BiomeRegistry biomeRegistry,
        TransitionPairs transitionPairs)
    {
        // The solver and the mesh code share the registry: it drives the background and foreground solves and
        // decides passability in the terrain-type map.
        var setup = MeshTerrainWfcSetup.ForRegistry(transitionPairs.Pairs, tileRegistry);
        var tileToTerrainType = AssignTerrainTypes(setup.Solver.RuleTileIds, tileRegistry);
        return new MeshTerrainGenerator(setup, tileToTerrainType, biomeRegistry);
    }

    /// <summary>
    /// The signature sampler a map's seed cards define: a gradient over the cards, seeded from the map seed. Returns
    /// null for no cards, which makes the generator use its fallback terrain.
    /// </summary>
    internal static Func<Vector2I, Vector2I, CardSignature>? CardSampler(CardSignature[] inputCards, int seed)
    {
        ArgumentNullException.ThrowIfNull(inputCards);
        if (inputCards.Length == 0)
            return null;

        // The generator used to seed its own stream with exactly this cast; keep it so large seeds stay the same.
        var rng = RandomSources.Create((ulong)seed);
        return new CardBasedGradient(inputCards, rng).GetSignatureAt;
    }

    /// <summary>
    /// Passable tiles get unique terrain types starting at 1; impassable tiles get 0.
    /// </summary>
    private static Dictionary<string, int> AssignTerrainTypes(
        IReadOnlyList<string> allTerrainIds,
        ITileRegistry tileRegistry)
    {
        var tileToTerrainType = new Dictionary<string, int>();
        var nextTerrainType = 1;

        foreach (var terrainId in allTerrainIds)
        {
            var tileDef = tileRegistry.GetTile(terrainId);
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
