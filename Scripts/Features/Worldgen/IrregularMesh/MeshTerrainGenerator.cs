using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshTerrain;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Generates irregular mesh terrain using two-pass WFC.
/// </summary>
public class MeshTerrainGenerator
{
    private readonly MeshTerrainWfcRunner _terrainWfc;

    /// <summary>
    /// Creates a two-pass terrain generator over a solver and the registry its catalog wraps.
    /// </summary>
    internal MeshTerrainGenerator(MeshTerrainWfcSetup setup, Dictionary<string, int> tileToTerrainType)
    {
        _terrainWfc = new MeshTerrainWfcRunner(setup, tileToTerrainType);
    }

    private BiomeRegistry? _biomeRegistry;

    /// <summary>
    /// Sets the biome registry for card-based terrain generation.
    /// </summary>
    public void SetBiomeRegistry(BiomeRegistry registry)
    {
        _biomeRegistry = registry;
    }

    /// <summary>
    /// Generates terrain using card signatures to influence biome distribution.
    /// </summary>
    public IrregularMesh GenerateWithCards(
        int rings,
        CardSignature[] inputCards,
        int seed,
        int relaxationIterations = 15,
        bool useConstraints = true)
    {
        var mesh = CreateMesh(rings, seed, relaxationIterations);
        var biomeRegistry = _biomeRegistry;
        if (biomeRegistry == null || biomeRegistry.Count == 0)
        {
            GD.PrintErr("[MeshTerrainGen] No biomes registered, using fallback terrain");
            _terrainWfc.ApplyFallbackTerrain(mesh);
            return mesh;
        }

        if (inputCards.Length == 0)
        {
            GD.PrintErr("[MeshTerrainGen] No input cards, using fallback terrain");
            _terrainWfc.ApplyFallbackTerrain(mesh);
            return mesh;
        }

        var bounds = mesh.Bounds;
        var effectiveSize = GetEffectiveSize(bounds);
        var rng = new RandomNumberGenerator { Seed = (ulong)seed };
        var gradient = new CardBasedGradient(inputCards, rng);
        var getBiomeAt = CreateBiomeSelector(gradient, effectiveSize, biomeRegistry);

        GD.Print(
            $"[MeshTerrainGen] Generating with {inputCards.Length} cards, " +
            $"{biomeRegistry.Count} biomes, effective size: {effectiveSize}");

        return _terrainWfc.Generate(
            mesh,
            biomeRegistry,
            getBiomeAt,
            effectiveSize,
            bounds,
            (ulong)seed);
    }

    /// <summary>
    /// Generates a mesh with the fallback terrain, for a map started without cards.
    /// </summary>
    internal IrregularMesh GenerateFallback(int rings, int seed, int relaxationIterations)
    {
        var mesh = CreateMesh(rings, seed, relaxationIterations);
        _terrainWfc.ApplyFallbackTerrain(mesh);
        return mesh;
    }

    private static IrregularMesh CreateMesh(int rings, int seed, int relaxationIterations)
    {
        var config = new MeshGenerator.GenerationConfig
        {
            Rings = rings,
            Seed = seed,
            RelaxationIterations = relaxationIterations
        };
        return MeshGenerator.Generate(config);
    }

    private static Vector2I GetEffectiveSize((Vector2 Min, Vector2 Max) bounds)
    {
        return new Vector2I(
            (int)Math.Ceiling(bounds.Max.X - bounds.Min.X),
            (int)Math.Ceiling(bounds.Max.Y - bounds.Min.Y));
    }

    private static Func<Vector2I, BiomeDefinition> CreateBiomeSelector(
        CardBasedGradient gradient,
        Vector2I effectiveSize,
        BiomeRegistry biomeRegistry)
    {
        return position =>
        {
            var signature = gradient.GetSignatureAt(position, effectiveSize);
            return biomeRegistry.FindClosestBySignature(signature)
                ?? biomeRegistry.GetAllBiomes().First();
        };
    }
}
