using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshTerrain;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Generates irregular mesh terrain using two-pass WFC.
/// </summary>
public class MeshTerrainGenerator : IMeshTerrainGenerator
{
    private readonly MeshTerrainWfcRunner _terrainWfc;
    private readonly BiomeSet _biomes;

    /// <summary>
    /// Creates a two-pass terrain generator over a solver, the registry its catalog wraps, and the biomes the cards
    /// choose between.
    /// </summary>
    internal MeshTerrainGenerator(
        MeshTerrainWfcSetup setup,
        Dictionary<string, int> tileToTerrainType,
        BiomeSet biomes)
    {
        _terrainWfc = new MeshTerrainWfcRunner(setup, tileToTerrainType);
        _biomes = biomes ?? throw new ArgumentNullException(nameof(biomes));
    }

    /// <summary>
    /// Generates terrain using a signature sampler to influence biome distribution. Without a sampler or biomes the
    /// mesh gets the fallback terrain.
    /// </summary>
    public IrregularMesh GenerateWithSampler(
        int rings,
        Func<Vector2I, Vector2I, CardSignature>? signatureAt,
        int seed,
        int relaxationIterations = 15,
        bool useConstraints = true)
    {
        var mesh = CreateMesh(rings, seed, relaxationIterations);
        if (_biomes.Count == 0)
        {
            ILog.Error("[MeshTerrainGen] No biomes registered, using fallback terrain");
            _terrainWfc.ApplyFallbackTerrain(mesh);
            return mesh;
        }

        if (signatureAt == null)
        {
            ILog.Error("[MeshTerrainGen] No signature sampler, using fallback terrain");
            _terrainWfc.ApplyFallbackTerrain(mesh);
            return mesh;
        }

        var bounds = mesh.Bounds;
        var effectiveSize = GetEffectiveSize(bounds);
        var getBiomeAt = CreateBiomeSelector(signatureAt, effectiveSize, _biomes);

        ILog.Print(
            $"[MeshTerrainGen] Generating with {_biomes.Count} biomes, effective size: {effectiveSize}");

        return _terrainWfc.Generate(
            mesh,
            _biomes,
            getBiomeAt,
            effectiveSize,
            bounds,
            (ulong)seed);
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
        Func<Vector2I, Vector2I, CardSignature> signatureAt,
        Vector2I effectiveSize,
        BiomeSet biomes)
    {
        return position =>
        {
            var signature = signatureAt(position, effectiveSize);
            return biomes.FindClosestBySignature(signature)
                ?? biomes.Biomes[0];
        };
    }
}
