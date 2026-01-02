using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// High-level WFC map generator that produces terrain maps using Wave Function Collapse.
/// Enforces hard constraints (valid adjacencies from transition map) and
/// soft constraints (biome preferences via weighted selection).
/// </summary>
public class WfcMapGenerator
{
    private readonly WfcAdjacencyRules _adjacencyRules;
    private readonly WfcTileSelector _selector;
    private readonly WfcMapDataAdapter _adapter;
    private readonly BlobSizeTracker _blobTracker;
    private readonly DiminishingReturnsSoftModifier _diminishingReturns;
    private readonly NoveltySoftModifier _novelty;
    private readonly CompactnessSoftModifier _compactness;

    /// <summary>
    /// Number of retry attempts when contradiction occurs (default 3).
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Penalty multiplier for tiles not in the biome's preferred set (default 0.1).
    /// </summary>
    public float NonBiomeTilePenalty
    {
        get => _selector.NonBiomeTilePenalty;
        set => _selector.NonBiomeTilePenalty = value;
    }

    /// <summary>
    /// Decay factor for diminishing returns modifier.
    /// Higher values = faster decay, smaller blobs.
    /// Default 0.5 targets ~8-10 tile blobs before continuity becomes penalty.
    /// </summary>
    public float DiminishingReturnsDecay
    {
        get => _diminishingReturns.DecayFactor;
        set => _diminishingReturns.DecayFactor = value;
    }

    /// <summary>
    /// Enable or disable the diminishing returns modifier.
    /// </summary>
    public bool EnableDiminishingReturns { get; set; } = true;

    /// <summary>
    /// Enable or disable the novelty modifier (boosts tiles starting new blobs).
    /// </summary>
    public bool EnableNovelty { get; set; } = true;

    /// <summary>
    /// Boost multiplier for tiles with no same-type neighbors.
    /// Default 3.0 helps new terrain types establish against dominant blobs.
    /// </summary>
    public float NoveltyBoost
    {
        get => _novelty.NoveltyBoost;
        set => _novelty.NoveltyBoost = value;
    }

    /// <summary>
    /// Enable or disable the compactness modifier (penalizes snake shapes).
    /// </summary>
    public bool EnableCompactness { get; set; } = true;

    /// <summary>
    /// Penalty for snake-like extensions (1 same-type neighbor).
    /// Default 0.3 means snakes are 70% less likely.
    /// </summary>
    public float SnakePenalty
    {
        get => _compactness.SnakePenalty;
        set => _compactness.SnakePenalty = value;
    }

    /// <summary>
    /// Boost for compact fills (3-4 same-type neighbors).
    /// Default 1.5 means filling gaps is 50% more likely.
    /// </summary>
    public float CompactBoost
    {
        get => _compactness.CompactBoost;
        set => _compactness.CompactBoost = value;
    }

    /// <summary>
    /// Creates a WFC map generator using the given transition resolver.
    /// </summary>
    public WfcMapGenerator(CompiledTransitionResolver transitionResolver)
    {
        _adjacencyRules = new WfcAdjacencyRules(transitionResolver);
        _blobTracker = new BlobSizeTracker();
        _diminishingReturns = new DiminishingReturnsSoftModifier(_blobTracker);
        _novelty = new NoveltySoftModifier();
        _compactness = new CompactnessSoftModifier();
        _selector = new WfcTileSelector();
        _adapter = new WfcMapDataAdapter();
    }

    /// <summary>
    /// Creates a WFC map generator with custom adjacency rules.
    /// Useful for testing or custom rule sets.
    /// </summary>
    public WfcMapGenerator(WfcAdjacencyRules adjacencyRules)
    {
        _adjacencyRules = adjacencyRules;
        _blobTracker = new BlobSizeTracker();
        _diminishingReturns = new DiminishingReturnsSoftModifier(_blobTracker);
        _novelty = new NoveltySoftModifier();
        _compactness = new CompactnessSoftModifier();
        _selector = new WfcTileSelector();
        _adapter = new WfcMapDataAdapter();
    }

    /// <summary>
    /// Generates a terrain map using WFC algorithm.
    /// </summary>
    /// <param name="biome">Biome definition for tile selection weights</param>
    /// <param name="size">Map dimensions (width, height)</param>
    /// <param name="seed">Random seed for reproducibility</param>
    /// <param name="signature">Optional card signature (reserved for future gradient support)</param>
    /// <returns>Result containing the generated map or error details</returns>
    public WfcGenerationResult Generate(
        BiomeDefinition biome,
        Vector2I size,
        ulong seed,
        CardSignature? signature = null)
    {
        // Determine initial tile set: intersection of biome tiles and adjacency-supported tiles
        var initialTiles = DetermineInitialTiles(biome);

        if (initialTiles.Count == 0)
        {
            return WfcGenerationResult.Failed(
                "No valid tiles found: biome tiles have no overlap with adjacency rules");
        }

        // Configure constraints
        ConfigureConstraints();

        // Create WFC components
        var propagator = new WfcPropagator(_adjacencyRules);
        var solver = new WfcSolver(propagator, _selector, _blobTracker);

        // Grid factory for retry support
        WfcGrid CreateGrid() => new WfcGrid(size.X, size.Y, initialTiles);

        // Solve with retry
        var (solveResult, grid) = solver.SolveWithRetry(CreateGrid, biome, seed, MaxRetries);

        if (!solveResult.Success)
        {
            return WfcGenerationResult.Failed(solveResult.ErrorMessage ?? "Unknown error");
        }

        // Convert to SimpleMapData
        var passableSet = new HashSet<string>(biome.PassableTiles.GetAllTileIds());
        var mapData = _adapter.ToSimpleMapData(grid, biome, passableSet);

        return WfcGenerationResult.Succeeded(mapData, solveResult.Iterations);
    }

    /// <summary>
    /// Generates with multiple biomes using a biome map.
    /// </summary>
    /// <param name="biomeRegistry">Registry of all available biomes</param>
    /// <param name="getBiomeAt">Function to get biome at each position</param>
    /// <param name="size">Map dimensions (width, height)</param>
    /// <param name="seed">Random seed for reproducibility</param>
    /// <param name="gradient">Optional gradient for biome strength calculation. If provided, BiomeAffinityConstraint is registered.</param>
    public WfcGenerationResult GenerateMultiBiome(
        BiomeRegistry biomeRegistry,
        Func<Vector2I, BiomeDefinition> getBiomeAt,
        Vector2I size,
        ulong seed,
        BaselineGradient? gradient = null)
    {
        // For multi-biome, we need the union of all tiles that might appear
        var allTiles = new HashSet<string>();
        var passableTiles = new HashSet<string>();

        foreach (var biome in biomeRegistry.GetAllBiomes())
        {
            foreach (var tileId in biome.PassableTiles.GetAllTileIds())
            {
                if (_adjacencyRules.AllTileIds.Contains(tileId))
                {
                    allTiles.Add(tileId);
                    passableTiles.Add(tileId);
                }
            }
        }

        if (allTiles.Count == 0)
        {
            return WfcGenerationResult.Failed("No valid tiles across all biomes");
        }

        // Configure base constraints (diminishing returns, novelty, compactness)
        ConfigureConstraints();

        // If gradient is provided, create BiomeStrengthGrid and register BiomeAffinityConstraint
        if (gradient != null)
        {
            var biomeStrengthGrid = new BiomeStrengthGrid(size, gradient, biomeRegistry);
            var affinityConstraint = new BiomeAffinityConstraint(biomeStrengthGrid, biomeRegistry);
            _selector.AddConstraint(affinityConstraint);
        }
        var propagator = new WfcPropagator(_adjacencyRules);
        var solver = new WfcSolver(propagator, _selector, _blobTracker);

        WfcGrid CreateGrid() => new WfcGrid(size.X, size.Y, allTiles);

        // Use the first biome as default (multi-biome support is simplified for now)
        BiomeDefinition? defaultBiome = null;
        foreach (var b in biomeRegistry.GetAllBiomes())
        {
            defaultBiome = b;
            break;
        }

        var (solveResult, grid) = solver.SolveWithRetry(CreateGrid, defaultBiome, seed, MaxRetries);

        if (!solveResult.Success)
        {
            return WfcGenerationResult.Failed(solveResult.ErrorMessage ?? "Unknown error");
        }

        // Build biome map
        var biomeMap = new string[size.Y, size.X];
        for (var y = 0; y < size.Y; y++)
        {
            for (var x = 0; x < size.X; x++)
            {
                biomeMap[y, x] = getBiomeAt(new Vector2I(x, y)).Id;
            }
        }

        var mapData = _adapter.ToSimpleMapData(grid, biomeMap, passableTiles);

        return WfcGenerationResult.Succeeded(mapData, solveResult.Iterations);
    }

    /// <summary>
    /// Configures all constraints based on current enable flags.
    /// </summary>
    private void ConfigureConstraints()
    {
        _selector.ClearConstraints();

        if (EnableDiminishingReturns)
        {
            _selector.AddConstraint(_diminishingReturns);
        }

        if (EnableNovelty)
        {
            _selector.AddConstraint(_novelty);
        }

        if (EnableCompactness)
        {
            _selector.AddConstraint(_compactness);
        }
    }

    /// <summary>
    /// Determines which tiles to include in the initial WFC grid.
    /// Uses intersection of biome-defined tiles and adjacency-supported tiles.
    /// </summary>
    private HashSet<string> DetermineInitialTiles(BiomeDefinition biome)
    {
        var tiles = new HashSet<string>();
        var adjacencyTiles = _adjacencyRules.AllTileIds;

        // Add passable tiles that have adjacency support
        foreach (var tileId in biome.PassableTiles.GetAllTileIds())
        {
            if (adjacencyTiles.Contains(tileId))
            {
                tiles.Add(tileId);
            }
            else
            {
                ILog.Print($"[WfcMapGenerator] Skipping tile '{tileId}': no adjacency rules defined");
            }
        }

        return tiles;
    }
}

/// <summary>
/// Result of WFC map generation.
/// </summary>
public readonly struct WfcGenerationResult
{
    public bool Success { get; }
    public SimpleMapData? MapData { get; }
    public int Iterations { get; }
    public string? ErrorMessage { get; }

    private WfcGenerationResult(bool success, SimpleMapData? mapData, int iterations, string? error)
    {
        Success = success;
        MapData = mapData;
        Iterations = iterations;
        ErrorMessage = error;
    }

    public static WfcGenerationResult Succeeded(SimpleMapData mapData, int iterations) =>
        new(true, mapData, iterations, null);

    public static WfcGenerationResult Failed(string error) =>
        new(false, null, 0, error);
}
