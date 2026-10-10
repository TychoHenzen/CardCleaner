using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Connectivity;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Contracts;
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
    private readonly WfcTileSetBuilder _tileSetBuilder;
    private readonly BlobSizeTracker _blobTracker;
    private readonly DiminishingReturnsSoftModifier _diminishingReturns;
    private readonly NoveltySoftModifier _novelty;
    private readonly CompactnessSoftModifier _compactness;
    private readonly SpatialCoherenceConstraint _spatialCoherence;
    private readonly WfcConstraintSet _constraintSet;
    private readonly IWfcTileCatalog? _tileCatalog;
    private IProfiler _profiler = new NoOpProfiler();

    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// How many derived seeds Generate tries before it reports a disconnected passable region. Values below 1 act as 1.
    /// </summary>
    public int MaxConnectivityAttempts { get; set; } = 5;

    public float NonBiomeTilePenalty
    {
        get => _selector.NonBiomeTilePenalty;
        set => _selector.NonBiomeTilePenalty = value;
    }

    public float DiminishingReturnsDecay
    {
        get => _diminishingReturns.DecayFactor;
        set => _diminishingReturns.DecayFactor = value;
    }

    public bool EnableDiminishingReturns { get; set; } = true;
    public bool EnableNovelty { get; set; } = true;

    public bool EnableSpatialCoherence { get; set; } = true;

    public float NoveltyBoost
    {
        get => _novelty.NoveltyBoost;
        set => _novelty.NoveltyBoost = value;
    }

    public bool EnableCompactness { get; set; } = true;

    public float CornerBoost
    {
        get => _compactness.CornerBoost;
        set => _compactness.CornerBoost = value;
    }

    public float GapFillBoost
    {
        get => _compactness.GapFillBoost;
        set => _compactness.GapFillBoost = value;
    }

    public float ContinuityBiasMultiplier
    {
        get => _selector.ContinuityBiasMultiplier;
        set => _selector.ContinuityBiasMultiplier = value;
    }

    public bool EnableConnectivity { get; set; } = true;

    public void SetProfiler(IProfiler profiler)
    {
        _profiler = profiler;
    }

    /// <summary>
    /// Sets the selected variants for PerGeneration variation groups.
    /// Must be called before Generate/GenerateMultiBiome to ensure
    /// only the selected variant from each group appears on the map.
    /// </summary>
    /// <param name="selectedVariants">Mapping of group base name to selected tile ID.</param>
    public void SetSelectedVariants(Dictionary<string, string>? selectedVariants)
    {
        _constraintSet.SetSelectedVariants(selectedVariants);
    }

    public WfcMapGenerator(WfcAdjacencyRules adjacencyRules, IWfcTileCatalog? tileCatalog = null)
    {
        _adjacencyRules = adjacencyRules;
        _tileCatalog = tileCatalog;
        _blobTracker = new BlobSizeTracker();
        _diminishingReturns = new DiminishingReturnsSoftModifier(_blobTracker);
        _novelty = new NoveltySoftModifier();
        _compactness = new CompactnessSoftModifier(_tileCatalog);
        _spatialCoherence = new SpatialCoherenceConstraint(_tileCatalog);
        _constraintSet = new WfcConstraintSet(_diminishingReturns, _spatialCoherence, _compactness, _tileCatalog);
        _selector = new WfcTileSelector();
        _tileSetBuilder = new WfcTileSetBuilder(adjacencyRules, tileCatalog);

        // Allow all non-auto-tiles to be adjacent to each other (for background layer WFC)
        if (tileCatalog != null)
        {
            GapTileAdjacencyConfigurator.Configure(_adjacencyRules, tileCatalog);
        }
    }

    public WfcGenerationResult Generate(
        BiomeDefinition biome,
        Vector2I size,
        ulong seed,
        CardSignature? signature = null)
    {
        var initialTileSets = _tileSetBuilder.ForBiome(biome);
        var initialTiles = initialTileSets.AllTiles;
        if (initialTiles.Count == 0)
        {
            return WfcGenerationResult.Failed(
                "No valid tiles found: biome tiles have no overlap with adjacency rules");
        }

        var attemptCap = Math.Max(1, MaxConnectivityAttempts);
        for (var attempt = 0; attempt < attemptCap; attempt++)
        {
            var attemptSeed = attempt == 0 ? seed : unchecked(seed + (ulong)attempt * 0x9E3779B97F4A7C15UL);
            // ASSUMPTION: GenerateOnce builds a new solver, grid and passability graph, so a rejected attempt
            // leaves no state behind for the next one.
            var result = GenerateOnce(biome, size, attemptSeed, initialTiles);
            if (!result.Success || !EnableConnectivity || _tileCatalog == null)
            {
                return result;
            }

            var graph = PassabilityGraph.FromTileIds(result.TileIds!, _tileCatalog.IsPassable);
            if (!graph.HasDisconnectedRegions())
            {
                return result;
            }
        }

        return WfcGenerationResult.Failed($"Passable cells disconnected after {attemptCap} attempts (seed {seed})");
    }

    private WfcGenerationResult GenerateOnce(
        BiomeDefinition biome,
        Vector2I size,
        ulong seed,
        IReadOnlyCollection<string> initialTiles)
    {
        // Initialize spatial coherence for this map size
        _spatialCoherence.Reset(size.X, size.Y);

        var solver = CreateSolver();

        WfcGrid CreateGrid() => new WfcGrid(size.X, size.Y, initialTiles);

        var attempt = SolveWithProfiling("WfcSolve", solver, CreateGrid, biome, seed);
        var solveResult = attempt.Result;
        if (!solveResult.Success)
        {
            return WfcGenerationResult.Failed(solveResult.ErrorMessage ?? "Unknown error");
        }

        var grid = (WfcGrid)attempt.Topology;

        string[,] biomeMap;
        string[,] tileIds;
        using (_profiler.BeginScope("MapDataConversion"))
        {
            biomeMap = BuildBiomeMap(size, _ => biome);
            tileIds = WfcGridTileIds.ToTileIds(grid);
        }
        return WfcGenerationResult.Succeeded(
            new Vector2I(grid.Width, grid.Height), tileIds, biomeMap, solveResult.Iterations);
    }

    public WfcGenerationResult GenerateMultiBiome(
        BiomeRegistry biomeRegistry,
        Func<Vector2I, BiomeDefinition> getBiomeAt,
        Vector2I size,
        ulong seed,
        BaselineGradient? gradient = null,
        Func<string, bool>? tileFilter = null)
    {
        var tileSets = _tileSetBuilder.ForAllBiomes(biomeRegistry, tileFilter);
        var allTiles = tileSets.AllTiles;
        if (allTiles.Count == 0)
        {
            return WfcGenerationResult.Failed("No valid tiles across all biomes");
        }

        _spatialCoherence.Reset(size.X, size.Y);

        if (gradient != null)
        {
            ConfigureConstraints();
            var biomeStrengthGrid = new BiomeStrengthGrid(size, gradient, biomeRegistry);
            _selector.AddConstraint(new BiomeAffinityConstraint(biomeStrengthGrid, biomeRegistry, _tileCatalog));
        }

        var solver = CreateSolver(skipBaseConstraints: gradient != null, size);

        WfcGrid CreateGrid() => new WfcGrid(size.X, size.Y, allTiles);

        var defaultBiome = biomeRegistry.GetAllBiomes().FirstOrDefault();

        var attempt = SolveWithProfiling("MultiBiomeWfcSolve", solver, CreateGrid, defaultBiome, seed);
        var solveResult = attempt.Result;
        if (!solveResult.Success)
        {
            return WfcGenerationResult.Failed(solveResult.ErrorMessage ?? "Unknown error");
        }

        var grid = (WfcGrid)attempt.Topology;

        string[,] biomeMap;
        string[,] tileIds;
        using (_profiler.BeginScope("MultiBiomeMapDataConversion"))
        {
            biomeMap = BuildBiomeMap(size, getBiomeAt);
            tileIds = WfcGridTileIds.ToTileIds(grid);
        }
        return WfcGenerationResult.Succeeded(
            new Vector2I(grid.Width, grid.Height), tileIds, biomeMap, solveResult.Iterations);
    }

    private WfcSolveAttempt SolveWithProfiling(
        string scopeName,
        WfcSolver solver,
        Func<IWfcTopology> createGrid,
        BiomeDefinition? biome,
        ulong seed)
    {
        using (_profiler.BeginScope(scopeName))
        {
            return WfcRetrySolver.SolveWithRetry(solver, createGrid, biome, seed, MaxRetries);
        }
    }

    private WfcSolver CreateSolver(bool skipBaseConstraints = false, Vector2I? size = null)
    {
        if (!skipBaseConstraints)
        {
            ConfigureConstraints();
        }

        var propagator = new WfcPropagator(_adjacencyRules);

        WfcSolver solver;
        if (!EnableConnectivity || _tileCatalog == null)
        {
            solver = new WfcSolver(propagator, _selector, _blobTracker, _spatialCoherence, _tileCatalog);
        }
        else
        {
            var passabilityGraph = new PassabilityGraph();
            bool IsPassable(string tileId) => _tileCatalog.IsPassable(tileId);

            _selector.AddConstraint(new ConnectivityConstraint(passabilityGraph, IsPassable));
            solver = new WfcSolver(
                propagator,
                _selector,
                _blobTracker,
                passabilityGraph,
                IsPassable,
                _spatialCoherence,
                _tileCatalog);
        }

        solver.SetProfiler(_profiler);
        return solver;
    }

    private void ConfigureConstraints()
    {
        _constraintSet.ApplyTo(
            _selector,
            new WfcConstraintToggles(EnableDiminishingReturns, EnableSpatialCoherence, EnableCompactness));
    }

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
