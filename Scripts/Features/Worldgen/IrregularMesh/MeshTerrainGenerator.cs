using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshTerrain;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Generates irregular mesh terrain using two-pass WFC.
/// </summary>
public class MeshTerrainGenerator
{
    private readonly MeshTerrainWfcRunner _terrainWfc;
    private readonly RuleDrivenMeshTerrain _ruleTerrain;

    /// <summary>
    /// Creates a two-pass terrain generator over a solver. The registry must be the one the solver's catalog wraps,
    /// or null when the solver was built without one.
    /// </summary>
    internal MeshTerrainGenerator(
        IWfcTerrainSolver solver,
        ITileRegistry? tileRegistry,
        Dictionary<string, int> tileToTerrainType)
    {
        _terrainWfc = new MeshTerrainWfcRunner(solver, tileRegistry, tileToTerrainType);
        _ruleTerrain = new RuleDrivenMeshTerrain(solver, tileToTerrainType);
    }

    /// <summary>
    /// Even older backward-compatible constructor using raw dictionaries.
    /// </summary>
    public MeshTerrainGenerator(
        Dictionary<string, HashSet<string>> adjacencyRules,
        Dictionary<string, int> tileToTerrainType)
        : this(
            IWfcTerrainSolver.Create(
                new CompiledTransitionResolver().GetAllTransitionPairs().ToList(),
                adjacencyRules,
                null),
            null,
            tileToTerrainType)
    {
        GD.PrintErr("[MeshTerrainGen] Using legacy constructor without tile registry - two-pass WFC will not work!");
    }

    /// <summary>
    /// Generates terrain using two-pass WFC matching SimpleMapGenerator.
    /// </summary>
    public IrregularMesh Generate(
        int rings,
        BiomeRegistry biomeRegistry,
        Func<Vector2I, BiomeDefinition> getBiomeAt,
        ulong seed,
        int relaxationIterations = 15)
    {
        var mesh = CreateMesh(rings, (int)seed, relaxationIterations);
        var bounds = mesh.Bounds;
        var effectiveSize = GetEffectiveSize(bounds);
        LogMeshSummary(mesh, effectiveSize);
        return _terrainWfc.Generate(
            mesh,
            biomeRegistry,
            getBiomeAt,
            effectiveSize,
            bounds,
            seed);
    }

    /// <summary>
    /// Backward-compatible Generate method for old code (single biome).
    /// </summary>
    public IrregularMesh Generate(
        int rings,
        BiomeDefinition? biome = null,
        int seed = 0,
        int relaxationIterations = 15)
    {
        if (biome == null)
            return GenerateFallback(rings, seed, relaxationIterations);

        var registry = new BiomeRegistry();
        return Generate(rings, registry, _ => biome, (ulong)seed, relaxationIterations);
    }

    /// <summary>
    /// Generates terrain by running WFC over the adjacency rules and tile mapping given at construction.
    /// Uses fallback terrain when the generator has no rules or the solve fails.
    /// </summary>
    internal IrregularMesh GenerateFromRules(int rings, int seed, int relaxationIterations = 15)
    {
        var mesh = CreateMesh(rings, seed, relaxationIterations);
        if (_ruleTerrain.Generate(mesh, (ulong)seed))
            return mesh;

        _terrainWfc.ApplyFallbackTerrain(mesh);
        return mesh;
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

    private IrregularMesh GenerateFallback(int rings, int seed, int relaxationIterations)
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

    private static void LogMeshSummary(IrregularMesh mesh, Vector2I effectiveSize)
    {
        GD.Print(
            $"[MeshTerrainGen] Mesh: {mesh.Vertices.Count} vertices, " +
            $"{mesh.Quads.Count} quads, effective size: {effectiveSize}");
    }
}
