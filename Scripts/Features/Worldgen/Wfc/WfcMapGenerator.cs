using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Connectivity;
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
    private readonly ITileRegistry? _tileRegistry;
    private IProfiler _profiler = new NoOpProfiler();

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
    /// Enable or disable the connectivity constraint (prevents disconnected passable regions).
    /// Default is true for WFC-native connectivity enforcement.
    /// </summary>
    public bool EnableConnectivity { get; set; } = true;

    public void SetProfiler(IProfiler profiler)
    {
        _profiler = profiler;
    }

    /// <summary>
    /// Creates a WFC map generator using the given transition resolver.
    /// </summary>
    /// <param name="transitionResolver">Transition resolver for adjacency rules.</param>
    /// <param name="tileRegistry">Tile registry for passability lookups. Required for connectivity constraint.</param>
    public WfcMapGenerator(CompiledTransitionResolver transitionResolver, ITileRegistry? tileRegistry = null)
    {
        _adjacencyRules = new WfcAdjacencyRules(transitionResolver);
        _tileRegistry = tileRegistry;
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
    /// <param name="adjacencyRules">Custom adjacency rules.</param>
    /// <param name="tileRegistry">Tile registry for passability lookups. Required for connectivity constraint.</param>
    public WfcMapGenerator(WfcAdjacencyRules adjacencyRules, ITileRegistry? tileRegistry = null)
    {
        _adjacencyRules = adjacencyRules;
        _tileRegistry = tileRegistry;
        _blobTracker = new BlobSizeTracker();
        _diminishingReturns = new DiminishingReturnsSoftModifier(_blobTracker);
        _novelty = new NoveltySoftModifier();
        _compactness = new CompactnessSoftModifier();
        _selector = new WfcTileSelector();
        _adapter = new WfcMapDataAdapter();
    }

    /// <summary>
    /// Generates a terrain map using WFC algorithm with a single biome.
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
        var (initialTiles, passableSet) = DetermineInitialTiles(biome);
        if (initialTiles.Count == 0)
        {
            return WfcGenerationResult.Failed(
                "No valid tiles found: biome tiles have no overlap with adjacency rules");
        }

        var solver = CreateSolver();

        WfcGrid CreateGrid() => new WfcGrid(size.X, size.Y, initialTiles);

        WfcSolveResult solveResult;
        WfcGrid grid;
        using (_profiler.BeginScope("WfcSolve"))
        {
            (solveResult, grid) = solver.SolveWithRetry(CreateGrid, biome, seed, MaxRetries);
        }

        if (!solveResult.Success)
        {
            return WfcGenerationResult.Failed(solveResult.ErrorMessage ?? "Unknown error");
        }

        SimpleMapData mapData;
        using (_profiler.BeginScope("MapDataConversion"))
        {
            mapData = _adapter.ToSimpleMapData(grid, biome, passableSet);
        }
        return WfcGenerationResult.Succeeded(mapData, solveResult.Iterations);
    }

    /// <summary>
    /// Generates a terrain map with multiple biomes based on a gradient.
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
        var (allTiles, passableTiles) = DetermineMultiBiomeTiles(biomeRegistry);
        if (allTiles.Count == 0)
        {
            return WfcGenerationResult.Failed("No valid tiles across all biomes");
        }

        // Register biome affinity constraint if gradient provided
        if (gradient != null)
        {
            ConfigureConstraints(); // Must call before adding affinity constraint
            var biomeStrengthGrid = new BiomeStrengthGrid(size, gradient, biomeRegistry);
            _selector.AddConstraint(new BiomeAffinityConstraint(biomeStrengthGrid, biomeRegistry));
        }

        var solver = CreateSolver(skipBaseConstraints: gradient != null);

        WfcGrid CreateGrid() => new WfcGrid(size.X, size.Y, allTiles);

        // Use first biome as default for solve weighting
        BiomeDefinition? defaultBiome = null;
        foreach (var b in biomeRegistry.GetAllBiomes())
        {
            defaultBiome = b;
            break;
        }

        WfcSolveResult solveResult;
        WfcGrid grid;
        using (_profiler.BeginScope("MultiBiomeWfcSolve"))
        {
            (solveResult, grid) = solver.SolveWithRetry(CreateGrid, defaultBiome, seed, MaxRetries);
        }

        if (!solveResult.Success)
        {
            return WfcGenerationResult.Failed(solveResult.ErrorMessage ?? "Unknown error");
        }

        SimpleMapData mapData;
        using (_profiler.BeginScope("MultiBiomeMapDataConversion"))
        {
            var biomeMap = BuildBiomeMap(size, getBiomeAt);
            mapData = _adapter.ToSimpleMapData(grid, biomeMap, passableTiles);
        }
        return WfcGenerationResult.Succeeded(mapData, solveResult.Iterations);
    }

    /// <summary>
    /// Creates a configured WfcSolver with all enabled constraints.
    /// </summary>
    /// <param name="skipBaseConstraints">If true, assumes ConfigureConstraints was already called.</param>
    private WfcSolver CreateSolver(bool skipBaseConstraints = false)
    {
        if (!skipBaseConstraints)
        {
            ConfigureConstraints();
        }

        var propagator = new WfcPropagator(_adjacencyRules, _tileRegistry);

        WfcSolver solver;
        if (!EnableConnectivity || _tileRegistry == null)
        {
            solver = new WfcSolver(propagator, _selector, _blobTracker);
        }
        else
        {
            var passabilityGraph = new PassabilityGraph();
            // Use TileRegistry as single source of truth for passability
            bool IsPassable(string tileId) => _tileRegistry.GetTile(tileId)?.IsPassable ?? false;

            _selector.AddConstraint(new ConnectivityConstraint(passabilityGraph, IsPassable));
            solver = new WfcSolver(propagator, _selector, _blobTracker, passabilityGraph, IsPassable);
        }

        solver.SetProfiler(_profiler);
        return solver;
    }

    /// <summary>
    /// Configures base constraints (diminishing returns, novelty, compactness) based on enable flags.
    /// </summary>
    private void ConfigureConstraints()
    {
        _selector.ClearConstraints();

        if (EnableDiminishingReturns)
            _selector.AddConstraint(_diminishingReturns);

        if (EnableNovelty)
            _selector.AddConstraint(_novelty);

        if (EnableCompactness)
            _selector.AddConstraint(_compactness);
    }

    /// <summary>
    /// Determines which tiles to include for a single biome.
    /// Uses PassableTiles from biome pools for candidates (controls obstacle density).
    /// Uses TileRegistry.IsPassable as the source of truth for ConnectivityConstraint.
    /// </summary>
    private (HashSet<string> allTiles, HashSet<string> passableTiles) DetermineInitialTiles(BiomeDefinition biome)
    {
        var allTiles = new HashSet<string>();
        var passableTiles = new HashSet<string>();
        var adjacencyTiles = _adjacencyRules.AllTileIds;

        // Only use PassableTiles from biome pool as candidates (blocked tiles handled separately)
        foreach (var tileId in biome.PassableTiles.GetAllTileIds())
        {
            if (!adjacencyTiles.Contains(tileId))
            {
                ILog.Print($"[WfcMapGenerator] Skipping tile '{tileId}': no adjacency rules defined");
                continue;
            }

            allTiles.Add(tileId);

            // Use TileRegistry as source of truth for passability (detects miscategorized tiles)
            if (_tileRegistry != null)
            {
                var tile = _tileRegistry.GetTile(tileId);
                if (tile?.IsPassable == true)
                {
                    passableTiles.Add(tileId);
                }
                else
                {
                    // Log warning: tile is in PassableTiles pool but IsPassable=false
                    ILog.Print($"[WfcMapGenerator] WARNING: Tile '{tileId}' is in PassableTiles but has IsPassable=false");
                }
            }
            else
            {
                // Fallback: assume tiles in PassableTiles pool are passable
                passableTiles.Add(tileId);
            }
        }

        return (allTiles, passableTiles);
    }

    /// <summary>
    /// Determines tiles for multi-biome generation.
    /// Uses biome pools for tile candidates (until data migration to tile-declared biomes).
    /// Uses TileRegistry.IsPassable as the source of truth for passability.
    /// </summary>
    private (HashSet<string> allTiles, HashSet<string> passableTiles) DetermineMultiBiomeTiles(BiomeRegistry registry)
    {
        var allTiles = new HashSet<string>();
        var passableTiles = new HashSet<string>();

        foreach (var biome in registry.GetAllBiomes())
        {
            // Collect all tiles from biome pools (both passable and blocked)
            var biomeTileIds = new HashSet<string>();
            foreach (var tileId in biome.PassableTiles.GetAllTileIds())
                biomeTileIds.Add(tileId);
            foreach (var tileId in biome.BlockedTiles.GetAllTileIds())
                biomeTileIds.Add(tileId);

            foreach (var tileId in biomeTileIds)
            {
                if (!_adjacencyRules.AllTileIds.Contains(tileId))
                    continue;

                allTiles.Add(tileId);

                // Use TileRegistry as source of truth for passability
                if (_tileRegistry != null)
                {
                    var tile = _tileRegistry.GetTile(tileId);
                    if (tile?.IsPassable == true)
                    {
                        passableTiles.Add(tileId);
                    }
                }
                else
                {
                    // Fallback: assume tiles in PassableTiles pool are passable
                    var passablePool = new HashSet<string>(biome.PassableTiles.GetAllTileIds());
                    if (passablePool.Contains(tileId))
                    {
                        passableTiles.Add(tileId);
                    }
                }
            }
        }

        return (allTiles, passableTiles);
    }

    /// <summary>
    /// Builds a biome ID map for the given size.
    /// </summary>
    private static string[,] BuildBiomeMap(Vector2I size, Func<Vector2I, BiomeDefinition> getBiomeAt)
    {
        var biomeMap = new string[size.Y, size.X];
        for (var y = 0; y < size.Y; y++)
        {
            for (var x = 0; x < size.X; x++)
            {
                biomeMap[y, x] = getBiomeAt(new Vector2I(x, y)).Id;
            }
        }
        return biomeMap;
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
