using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

internal sealed class WfcTerrainSolver : IWfcTerrainSolver
{
    private readonly IReadOnlyList<(string tileA, string tileB)> _transitionPairs;
    private readonly WfcAdjacencyRules _rules;
    private readonly ITileRegistry? _registry;
    private readonly WfcMapGenerator _mapGenerator;

    public WfcTerrainSolver(
        IReadOnlyList<(string tileA, string tileB)> transitionPairs,
        IReadOnlyDictionary<string, HashSet<string>>? extraAdjacency,
        ITileRegistry? tileRegistry)
    {
        _transitionPairs = transitionPairs;
        _rules = new WfcAdjacencyRules(transitionPairs);
        if (extraAdjacency != null)
        {
            foreach (var (tile, neighbors) in extraAdjacency)
            {
                foreach (var neighbor in neighbors)
                    _rules.AddAdjacency(tile, neighbor);
            }
        }

        // Snapshot before the map generator below adds gap tiles to the rules, so the ids are the pre-gap ones.
        RuleTileIds = _rules.AllTileIds.ToList();
        _registry = tileRegistry;
        _mapGenerator = new WfcMapGenerator(_rules, tileRegistry);
    }

    public IReadOnlyList<string> RuleTileIds { get; }

    public ITileRegistry? TileRegistry => _registry;

    public WfcGenerationResult GenerateBackground(
        BiomeRegistry biomes,
        Func<Vector2I, BiomeDefinition> getBiomeAt,
        Vector2I size,
        ulong seed,
        Func<TileDefinition, bool> tileFilter) =>
        _mapGenerator.GenerateMultiBiome(biomes, getBiomeAt, size, seed, null, tileFilter);

    public WfcGraphSolution SolveGraphWithRegistry(
        int[][] neighbors,
        IReadOnlyCollection<string> initialTiles,
        ulong seed,
        Func<TileDefinition, bool>? gapTileFilter)
    {
        var registry = _registry ?? throw new InvalidOperationException("SolveGraphWithRegistry needs a tile registry");

        var topology = new WfcNeighborListTopology(neighbors, initialTiles);
        var rules = new WfcAdjacencyRules(_transitionPairs);
        GapTileAdjacencyConfigurator.Configure(rules, registry, gapTileFilter, logSummary: false);

        var propagator = new WfcPropagator(rules);
        var selector = new WfcTileSelector();
        var blobTracker = new BlobSizeTracker();
        blobTracker.Initialize(neighbors.Length);
        var spatialCoherence = new SpatialCoherenceConstraint(registry);

        selector.AddConstraint(new DiminishingReturnsSoftModifier(blobTracker));
        selector.AddConstraint(spatialCoherence);
        selector.AddConstraint(new AutoTileGapConstraint(registry));
        selector.AddConstraint(new NoSolidFillConstraint(registry));

        if (registry is TileRegistry concreteRegistry)
            selector.AddConstraint(new TileProbabilityConstraint(concreteRegistry));

        var solver = new WfcSolver(
            propagator,
            selector,
            blobTracker,
            spatialCoherence: spatialCoherence,
            tileRegistry: registry)
        {
            MaxIterations = neighbors.Length * 2
        };
        var result = solver.Solve(topology, null, new RandomNumberGenerator { Seed = seed });
        return ToGraphSolution(result, topology, neighbors.Length);
    }

    public WfcGraphSolution SolveGraphWithRules(int[][] neighbors, IReadOnlyCollection<string> initialTiles, ulong seed)
    {
        var topology = new WfcNeighborListTopology(neighbors, initialTiles);
        var solver = new WfcSolver(new WfcPropagator(_rules), new WfcTileSelector())
        {
            MaxIterations = neighbors.Length * 2
        };
        var result = solver.Solve(topology, null, new RandomNumberGenerator { Seed = seed });
        return ToGraphSolution(result, topology, neighbors.Length);
    }

    private static WfcGraphSolution ToGraphSolution(WfcSolveResult result, IWfcTopology topology, int cellCount)
    {
        var collapsedTiles = new string?[cellCount];
        for (var cellId = 0; cellId < cellCount; cellId++)
            collapsedTiles[cellId] = topology.GetCollapsedTileAt(cellId);

        return new WfcGraphSolution(result.Success, result.Iterations, result.ErrorMessage, collapsedTiles);
    }
}
