using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
/// Enforces hard constraints (valid adjacencies from the transition map) and soft constraints (weights from the
/// constraints registered on the tile selector). Biome affinity comes from <see cref="GenerateMultiBiome"/> with a
/// gradient.
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

    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// How many attempts Generate makes, the caller's seed first and then derived seeds, before it reports a
    /// disconnected passable region. Values below 1 act as 1.
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

    /// <summary>
    /// Generates one map for a single biome. The tiles are the adjacency-rule tiles that the tile catalog contains and
    /// allows in the biome (WfcTileSetBuilder.ForBiome). Each candidate gets a uniform base weight, so biome pool
    /// weights do not choose the tile; they still feed the weighted entropy that orders cell collapses. For biome
    /// affinity, use GenerateMultiBiome with a gradient. With a catalog and EnableConnectivity, a disconnected passable
    /// result is retried with derived seeds, up to MaxConnectivityAttempts, and the call fails if every attempt is
    /// disconnected. A solver failure on any attempt ends the call; the solver has already retried that seed.
    /// </summary>
    /// <exception cref="OperationCanceledException">Thrown when the token is cancelled during a solve.</exception>
    public WfcGenerationResult Generate(
        BiomeDefinition biome,
        Vector2I size,
        ulong seed,
        CardSignature? signature = null,
        CancellationToken cancellationToken = default)
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
            var result = GenerateOnce(biome, size, attemptSeed, initialTiles, cancellationToken);
            if (!result.Success && attempt > 0)
            {
                return WfcGenerationResult.Failed(
                    $"Connectivity attempt {attempt + 1} (seed {attemptSeed}) failed: {result.ErrorMessage}");
            }

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
        IReadOnlyCollection<string> initialTiles,
        CancellationToken cancellationToken)
    {
        // Initialize spatial coherence for this map size
        _spatialCoherence.Reset(size.X, size.Y);

        var solver = CreateSolver();

        WfcGrid CreateGrid() => new WfcGrid(size.X, size.Y, initialTiles);

        var attempt = SolveTimed(solver, CreateGrid, biome, seed, cancellationToken);
        return ToGenerationResult(attempt, size, _ => biome);
    }

    /// <summary>
    /// Generates one map across all registered biomes; with a gradient, BiomeAffinityConstraint weights tiles toward
    /// the local biome. Unlike Generate, there is no post-solve connectivity check or retry: with a catalog and
    /// EnableConnectivity only the reactive ConnectivityConstraint runs, so a disconnected map can be returned.
    /// </summary>
    /// <exception cref="OperationCanceledException">Thrown when the token is cancelled during the solve.</exception>
    public WfcGenerationResult GenerateMultiBiome(
        BiomeRegistry biomeRegistry,
        Func<Vector2I, BiomeDefinition> getBiomeAt,
        Vector2I size,
        ulong seed,
        BaselineGradient? gradient = null,
        Func<string, bool>? tileFilter = null,
        CancellationToken cancellationToken = default)
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

        var attempt = SolveTimed(solver, CreateGrid, defaultBiome, seed, cancellationToken);
        return ToGenerationResult(attempt, size, getBiomeAt);
    }

    /// <summary>
    /// Runs the retrying solve and adds the time of the whole call, every attempt included, to the result's Solve
    /// timing.
    /// </summary>
    private WfcSolveAttempt SolveTimed(
        WfcSolver solver,
        Func<IWfcTopology> createGrid,
        BiomeDefinition? biome,
        ulong seed,
        CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();
        var attempt = WfcRetrySolver.SolveWithRetry(solver, createGrid, biome, seed, MaxRetries, cancellationToken);
        var timings = attempt.Result.Timings with { Solve = Stopwatch.GetElapsedTime(start) };
        return attempt with { Result = attempt.Result.WithTimings(timings) };
    }

    /// <summary>
    /// Turns a solve attempt into a generation result: a failed solve fails with zero timings, a solved grid is
    /// converted to tile ids and a biome map, with the conversion time added to the solve's timings.
    /// </summary>
    private static WfcGenerationResult ToGenerationResult(
        WfcSolveAttempt attempt,
        Vector2I size,
        Func<Vector2I, BiomeDefinition> getBiomeAt)
    {
        var solveResult = attempt.Result;
        if (!solveResult.Success)
        {
            return WfcGenerationResult.Failed(solveResult.ErrorMessage ?? "Unknown error");
        }

        var grid = (WfcGrid)attempt.Topology;
        var conversionStart = Stopwatch.GetTimestamp();
        var biomeMap = BuildBiomeMap(size, getBiomeAt);
        var tileIds = WfcGridTileIds.ToTileIds(grid);
        var timings = solveResult.Timings with { MapDataConversion = Stopwatch.GetElapsedTime(conversionStart) };
        return WfcGenerationResult.Succeeded(
            new Vector2I(grid.Width, grid.Height), tileIds, biomeMap, solveResult.Iterations, timings);
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
