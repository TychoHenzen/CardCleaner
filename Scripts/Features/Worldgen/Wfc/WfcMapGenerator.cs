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
    private readonly SpatialCoherenceConstraint _spatialCoherence;
    private readonly AutoTileGapConstraint? _autoTileGap;
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

    public float NoveltyBoost
    {
        get => _novelty.NoveltyBoost;
        set => _novelty.NoveltyBoost = value;
    }

    public bool EnableCompactness { get; set; } = true;

    public float SnakePenalty
    {
        get => _compactness.SnakePenalty;
        set => _compactness.SnakePenalty = value;
    }

    public float CompactBoost
    {
        get => _compactness.CompactBoost;
        set => _compactness.CompactBoost = value;
    }

    public bool EnableConnectivity { get; set; } = true;

    public void SetProfiler(IProfiler profiler)
    {
        _profiler = profiler;
    }

    public WfcMapGenerator(CompiledTransitionResolver transitionResolver, ITileRegistry? tileRegistry = null)
    {
        _adjacencyRules = new WfcAdjacencyRules(transitionResolver);
        _tileRegistry = tileRegistry;
        _blobTracker = new BlobSizeTracker();
        _diminishingReturns = new DiminishingReturnsSoftModifier(_blobTracker);
        _novelty = new NoveltySoftModifier();
        _compactness = new CompactnessSoftModifier();
        _spatialCoherence = new SpatialCoherenceConstraint();
        _autoTileGap = tileRegistry != null ? new AutoTileGapConstraint(tileRegistry) : null;
        _selector = new WfcTileSelector();
        _adapter = new WfcMapDataAdapter();
    }

    public WfcMapGenerator(WfcAdjacencyRules adjacencyRules, ITileRegistry? tileRegistry = null)
    {
        _adjacencyRules = adjacencyRules;
        _tileRegistry = tileRegistry;
        _blobTracker = new BlobSizeTracker();
        _diminishingReturns = new DiminishingReturnsSoftModifier(_blobTracker);
        _novelty = new NoveltySoftModifier();
        _compactness = new CompactnessSoftModifier();
        _spatialCoherence = new SpatialCoherenceConstraint();
        _autoTileGap = tileRegistry != null ? new AutoTileGapConstraint(tileRegistry) : null;
        _selector = new WfcTileSelector();
        _adapter = new WfcMapDataAdapter();
    }

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

        _spatialCoherence.Reset(size.X, size.Y);

        if (gradient != null)
        {
            ConfigureConstraints();
            var biomeStrengthGrid = new BiomeStrengthGrid(size, gradient, biomeRegistry);
            _selector.AddConstraint(new BiomeAffinityConstraint(biomeStrengthGrid, biomeRegistry));
        }

        var solver = CreateSolver(skipBaseConstraints: gradient != null, size);

        WfcGrid CreateGrid() => new WfcGrid(size.X, size.Y, allTiles);

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
            solver = new WfcSolver(propagator, _selector, _blobTracker, _spatialCoherence);
        }
        else
        {
            var passabilityGraph = new PassabilityGraph();
            bool IsPassable(string tileId) => _tileRegistry.GetTile(tileId)?.IsPassable ?? false;

            _selector.AddConstraint(new ConnectivityConstraint(passabilityGraph, IsPassable));
            solver = new WfcSolver(propagator, _selector, _blobTracker, passabilityGraph, IsPassable, _spatialCoherence);
        }

        solver.SetProfiler(_profiler);
        return solver;
    }

    private void ConfigureConstraints()
    {
        _selector.ClearConstraints();

        if (EnableDiminishingReturns)
            _selector.AddConstraint(_diminishingReturns);

        _selector.AddConstraint(_spatialCoherence);

        // Enforce 1-tile gap between different auto-tile types (8-neighbor check)
        if (_autoTileGap != null)
            _selector.AddConstraint(_autoTileGap);
    }

    private (HashSet<string> allTiles, HashSet<string> passableTiles) DetermineInitialTiles(BiomeDefinition biome)
    {
        var allTiles = new HashSet<string>();
        var passableTiles = new HashSet<string>();
        var adjacencyTiles = _adjacencyRules.AllTileIds;

        foreach (var tileId in biome.PassableTiles.GetAllTileIds())
        {
            if (!adjacencyTiles.Contains(tileId))
            {
                ILog.Print($"[WfcMapGenerator] Skipping tile '{tileId}': no adjacency rules defined");
                continue;
            }

            allTiles.Add(tileId);

            if (_tileRegistry != null)
            {
                var tile = _tileRegistry.GetTile(tileId);
                if (tile?.IsPassable == true)
                {
                    passableTiles.Add(tileId);
                }
                else
                {
                    ILog.Print($"[WfcMapGenerator] WARNING: Tile '{tileId}' is in PassableTiles but has IsPassable=false");
                }
            }
            else
            {
                passableTiles.Add(tileId);
            }
        }

        return (allTiles, passableTiles);
    }

    private (HashSet<string> allTiles, HashSet<string> passableTiles) DetermineMultiBiomeTiles(BiomeRegistry registry)
    {
        var allTiles = new HashSet<string>();
        var passableTiles = new HashSet<string>();

        foreach (var biome in registry.GetAllBiomes())
        {
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
