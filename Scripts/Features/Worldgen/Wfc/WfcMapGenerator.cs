using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
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
    private readonly WfcMapDataAdapter _adapter;
    private readonly WfcTileSetBuilder _tileSetBuilder;
    private readonly BlobSizeTracker _blobTracker;
    private readonly DiminishingReturnsSoftModifier _diminishingReturns;
    private readonly NoveltySoftModifier _novelty;
    private readonly CompactnessSoftModifier _compactness;
    private readonly SpatialCoherenceConstraint _spatialCoherence;
    private readonly WfcConstraintSet _constraintSet;
    private readonly ITileRegistry? _tileRegistry;
    private IProfiler _profiler = new NoOpProfiler();

    public int MaxRetries { get; set; } = 3;

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

    public WfcMapGenerator(CompiledTransitionResolver transitionResolver, ITileRegistry? tileRegistry = null)
        : this(new WfcAdjacencyRules(transitionResolver), tileRegistry)
    {
    }

    public WfcMapGenerator(WfcAdjacencyRules adjacencyRules, ITileRegistry? tileRegistry = null)
    {
        _adjacencyRules = adjacencyRules;
        _tileRegistry = tileRegistry;
        _blobTracker = new BlobSizeTracker();
        _diminishingReturns = new DiminishingReturnsSoftModifier(_blobTracker);
        _novelty = new NoveltySoftModifier();
        _compactness = new CompactnessSoftModifier(tileRegistry);
        _spatialCoherence = new SpatialCoherenceConstraint(tileRegistry);
        _constraintSet = new WfcConstraintSet(_diminishingReturns, _spatialCoherence, _compactness, tileRegistry);
        _selector = new WfcTileSelector();
        _adapter = new WfcMapDataAdapter();
        _tileSetBuilder = new WfcTileSetBuilder(adjacencyRules, tileRegistry);

        // Allow all non-auto-tiles to be adjacent to each other (for background layer WFC)
        if (tileRegistry != null)
        {
            GapTileAdjacencyConfigurator.Configure(_adjacencyRules, tileRegistry);
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
        var passableSet = initialTileSets.PassableTiles;
        if (initialTiles.Count == 0)
        {
            return WfcGenerationResult.Failed(
                "No valid tiles found: biome tiles have no overlap with adjacency rules");
        }

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

        SimpleMapData mapData;
        using (_profiler.BeginScope("MapDataConversion"))
        {
            mapData = _adapter.ToSimpleMapData(grid, biome, passableSet);
        }
        return WfcGenerationResult.Succeeded(mapData, solveResult.Iterations);
    }

    public WfcGenerationResult GenerateMultiBiome(
        BiomeRegistry biomeRegistry,
        Func<Vector2I, BiomeDefinition> getBiomeAt,
        Vector2I size,
        ulong seed,
        BaselineGradient? gradient = null,
        Func<TileDefinition, bool>? tileFilter = null)
    {
        var tileSets = _tileSetBuilder.ForAllBiomes(biomeRegistry, tileFilter);
        var allTiles = tileSets.AllTiles;
        var passableTiles = tileSets.PassableTiles;
        if (allTiles.Count == 0)
        {
            return WfcGenerationResult.Failed("No valid tiles across all biomes");
        }

        _spatialCoherence.Reset(size.X, size.Y);

        if (gradient != null)
        {
            ConfigureConstraints();
            var biomeStrengthGrid = new BiomeStrengthGrid(size, gradient, biomeRegistry);
            _selector.AddConstraint(new BiomeAffinityConstraint(biomeStrengthGrid, biomeRegistry, _tileRegistry));
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

        SimpleMapData mapData;
        using (_profiler.BeginScope("MultiBiomeMapDataConversion"))
        {
            var biomeMap = BuildBiomeMap(size, getBiomeAt);
            mapData = _adapter.ToSimpleMapData(grid, biomeMap, passableTiles);
        }
        return WfcGenerationResult.Succeeded(mapData, solveResult.Iterations);
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
        if (!EnableConnectivity || _tileRegistry == null)
        {
            solver = new WfcSolver(propagator, _selector, _blobTracker, _spatialCoherence, _tileRegistry);
        }
        else
        {
            var passabilityGraph = new PassabilityGraph();
            bool IsPassable(string tileId) => _tileRegistry.GetTile(tileId)?.IsPassable ?? false;

            _selector.AddConstraint(new ConnectivityConstraint(passabilityGraph, IsPassable));
            solver = new WfcSolver(
                propagator,
                _selector,
                _blobTracker,
                passabilityGraph,
                IsPassable,
                _spatialCoherence,
                _tileRegistry);
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
