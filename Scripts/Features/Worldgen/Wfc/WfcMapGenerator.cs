using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
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
    private readonly NoSolidFillConstraint? _noSolidFill;
    private readonly BitmaskValidityConstraint? _bitmaskValidity;
    private readonly TileProbabilityConstraint? _tileProbability;
    private readonly ITileRegistry? _tileRegistry;
    private IProfiler _profiler = new NoOpProfiler();

    // Selected variants for PerGeneration groups (e.g., "grass" → "grass2")
    private Dictionary<string, string>? _selectedVariants;

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
        _selectedVariants = selectedVariants;
        _tileProbability?.SetSelectedVariants(selectedVariants);
    }

    public WfcMapGenerator(CompiledTransitionResolver transitionResolver, ITileRegistry? tileRegistry = null)
    {
        _adjacencyRules = new WfcAdjacencyRules(transitionResolver);
        _tileRegistry = tileRegistry;
        _blobTracker = new BlobSizeTracker();
        _diminishingReturns = new DiminishingReturnsSoftModifier(_blobTracker);
        _novelty = new NoveltySoftModifier();
        _compactness = new CompactnessSoftModifier(tileRegistry);
        _spatialCoherence = new SpatialCoherenceConstraint(tileRegistry);
        _autoTileGap = tileRegistry != null ? new AutoTileGapConstraint(tileRegistry) : null;
        _noSolidFill = tileRegistry != null ? new NoSolidFillConstraint(tileRegistry) : null;
        _bitmaskValidity = tileRegistry != null ? new BitmaskValidityConstraint(tileRegistry) : null;
        _tileProbability = tileRegistry is TileRegistry concreteRegistry ? new TileProbabilityConstraint(concreteRegistry) : null;
        _selector = new WfcTileSelector();
        _adapter = new WfcMapDataAdapter();

        // Allow all non-auto-tiles to be adjacent to each other (for background layer WFC)
        if (tileRegistry != null)
        {
            ConfigureGapTileAdjacencies(tileRegistry);
        }
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
        _autoTileGap = tileRegistry != null ? new AutoTileGapConstraint(tileRegistry) : null;
        _noSolidFill = tileRegistry != null ? new NoSolidFillConstraint(tileRegistry) : null;
        _bitmaskValidity = tileRegistry != null ? new BitmaskValidityConstraint(tileRegistry) : null;
        _tileProbability = tileRegistry is TileRegistry concreteRegistry ? new TileProbabilityConstraint(concreteRegistry) : null;
        _selector = new WfcTileSelector();
        _adapter = new WfcMapDataAdapter();

        // Allow all non-auto-tiles to be adjacent to each other (for background layer WFC)
        if (tileRegistry != null)
        {
            ConfigureGapTileAdjacencies(tileRegistry);
        }
    }

    /// <summary>
    /// Configures adjacency rules so that all non-auto-tiles (gap tiles) can be adjacent to each other.
    /// This is necessary for the background layer of two-phase WFC where only gap tiles are used.
    /// Without this, gap tiles can only be adjacent to auto-tiles (from transition definitions),
    /// causing WFC to collapse everything to a single tile type.
    /// </summary>
    private void ConfigureGapTileAdjacencies(ITileRegistry tileRegistry)
    {
        var gapTiles = new List<string>();
        var autoTiles = new List<string>();

        foreach (var tile in tileRegistry.GetAllTiles())
        {
            if (!tile.HasAutoTileVariants)
            {
                gapTiles.Add(tile.Id);
            }
            else
            {
                autoTiles.Add(tile.Id);
            }
        }

        // Add all gap tiles to adjacency rules with mutual adjacency
        // (they can all be adjacent to each other and to any auto-tile)
        if (gapTiles.Count > 0)
        {
            _adjacencyRules.AddMutualAdjacencies(gapTiles);

            // Gap tiles can be adjacent to any auto-tile
            foreach (var gapTile in gapTiles)
            {
                foreach (var autoTile in autoTiles)
                {
                    _adjacencyRules.AddAdjacency(gapTile, autoTile);
                }
            }

            GD.Print($"[WFC] Configured {gapTiles.Count} gap tiles for adjacency (can be next to {autoTiles.Count} auto-tiles)");
        }

        // Add auto-tiles with self-adjacency if not already in rules
        foreach (var autoTile in autoTiles)
        {
            _adjacencyRules.EnsureSelfAdjacency(autoTile);
        }
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

        // Initialize spatial coherence for this map size
        _spatialCoherence.Reset(size.X, size.Y);

        var solver = CreateSolver();

        WfcGrid CreateGrid() => new WfcGrid(size.X, size.Y, initialTiles);

        WfcSolveResult solveResult;
        WfcGrid grid;
        using (_profiler.BeginScope("WfcSolve"))
        {
            var (result, topology) = solver.SolveWithRetry(CreateGrid, biome, seed, MaxRetries);
            solveResult = result;
            grid = (WfcGrid)topology;
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
        BaselineGradient? gradient = null,
        Func<TileDefinition, bool>? tileFilter = null)
    {
        var (allTiles, passableTiles) = DetermineMultiBiomeTiles(biomeRegistry, tileFilter);
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
            var (result, topology) = solver.SolveWithRetry(CreateGrid, defaultBiome, seed, MaxRetries);
            solveResult = result;
            grid = (WfcGrid)topology;
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
            solver = new WfcSolver(propagator, _selector, _blobTracker, _spatialCoherence, _tileRegistry);
        }
        else
        {
            var passabilityGraph = new PassabilityGraph();
            bool IsPassable(string tileId) => _tileRegistry.GetTile(tileId)?.IsPassable ?? false;

            _selector.AddConstraint(new ConnectivityConstraint(passabilityGraph, IsPassable));
            solver = new WfcSolver(propagator, _selector, _blobTracker, passabilityGraph, IsPassable, _spatialCoherence, _tileRegistry);
        }

        solver.SetProfiler(_profiler);
        return solver;
    }

    private void ConfigureConstraints()
    {
        _selector.ClearConstraints();

        if (EnableDiminishingReturns)
            _selector.AddConstraint(_diminishingReturns);

        if (EnableSpatialCoherence)
            _selector.AddConstraint(_spatialCoherence);

        // Encourage compact blob shapes (boosts corner/gap fills)
        if (EnableCompactness)
            _selector.AddConstraint(_compactness);

        // Enforce 1-tile gap between different auto-tile types (8-neighbor check)
        if (_autoTileGap != null)
            _selector.AddConstraint(_autoTileGap);

        // Prevent 2x2 solid regions for tilesets lacking bitmask 15 (solid fill)
        if (_noSolidFill != null)
            _selector.AddConstraint(_noSolidFill);

        // Prevent tile configurations that would create disallowed bitmask patterns
        if (_bitmaskValidity != null)
            _selector.AddConstraint(_bitmaskValidity);

        // Apply tile probability/density from TSX and variation groups
        if (_tileProbability != null)
            _selector.AddConstraint(_tileProbability);
    }

    private (HashSet<string> allTiles, HashSet<string> passableTiles) DetermineInitialTiles(BiomeDefinition biome)
    {
        var allTiles = new HashSet<string>();
        var passableTiles = new HashSet<string>();

        // Check each tile in adjacency rules for biome compatibility
        foreach (var tileId in _adjacencyRules.AllTileIds)
        {
            var tileDef = _tileRegistry?.GetTile(tileId);
            if (tileDef == null)
                continue;

            // Check if tile is allowed in this biome using TileDefinition.IsAllowedInBiome
            if (!tileDef.IsAllowedInBiome(biome.Id))
                continue;

            allTiles.Add(tileId);

            if (tileDef.IsPassable)
                passableTiles.Add(tileId);
        }

        return (allTiles, passableTiles);
    }

    private (HashSet<string> allTiles, HashSet<string> passableTiles) DetermineMultiBiomeTiles(
        BiomeRegistry registry,
        Func<TileDefinition, bool>? tileFilter = null)
    {
        var allTiles = new HashSet<string>();
        var passableTiles = new HashSet<string>();

        // Get all biome IDs for checking tile compatibility
        var biomeIds = registry.GetAllBiomeIds().ToList();

        // Check each tile in the adjacency rules against biome compatibility
        foreach (var tileId in _adjacencyRules.AllTileIds)
        {
            var tileDef = _tileRegistry?.GetTile(tileId);
            if (tileDef == null)
                continue;

            // Apply tile filter if provided
            if (tileFilter != null && !tileFilter(tileDef))
                continue;

            // Check if tile is allowed in ANY of the biomes
            // IsAllowedInBiome returns true if AllowedBiomes is null (universal) or contains the biome
            var isAllowedInAnyBiome = biomeIds.Any(biomeId => tileDef.IsAllowedInBiome(biomeId));
            if (!isAllowedInAnyBiome)
                continue;

            allTiles.Add(tileId);

            if (tileDef.IsPassable)
                passableTiles.Add(tileId);
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
