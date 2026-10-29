using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

internal sealed class WfcTerrainSolver : IWfcTerrainSolver
{
    private readonly IReadOnlyList<(string tileA, string tileB)> _transitionPairs;
    private readonly WfcAdjacencyRules _rules;
    private readonly IWfcTileCatalog? _tileCatalog;
    private readonly WfcMapGenerator _mapGenerator;

    public WfcTerrainSolver(
        IReadOnlyList<(string tileA, string tileB)> transitionPairs,
        IWfcTileCatalog? tileCatalog)
    {
        _transitionPairs = transitionPairs;
        _rules = new WfcAdjacencyRules(transitionPairs);

        // Snapshot before the map generator below adds gap tiles to the rules, so the ids are the pre-gap ones.
        RuleTileIds = _rules.AllTileIds.ToList();
        _tileCatalog = tileCatalog;
        _mapGenerator = new WfcMapGenerator(_rules, tileCatalog);
    }

    public IReadOnlyList<string> RuleTileIds { get; }

    public WfcGenerationResult GenerateBackground(
        BiomeSet biomes,
        Func<Vector2I, BiomeDefinition> getBiomeAt,
        Vector2I size,
        ulong seed,
        Func<string, bool> tileFilter) =>
        _mapGenerator.GenerateMultiBiome(biomes, getBiomeAt, size, seed, null, tileFilter);

    public WfcGraphSolution SolveGraphWithCatalog(
        int[][] neighbors,
        IReadOnlyCollection<string> initialTiles,
        ulong seed,
        Func<string, bool>? gapTileFilter)
    {
        var catalog = _tileCatalog
            ?? throw new InvalidOperationException("SolveGraphWithCatalog needs a tile catalog");

        return WfcRetrySolver.SolveGraph(neighbors, _transitionPairs, catalog, initialTiles, seed, gapTileFilter);
    }

    /// <summary>
    /// The container's IWfcTerrainSolver.IFactory. Each Create call builds a solver over fresh rules.
    /// </summary>
    [Service(typeof(IWfcTerrainSolver.IFactory))]
    internal sealed class Factory : IWfcTerrainSolver.IFactory
    {
        public IWfcTerrainSolver Create(
            IReadOnlyList<(string tileA, string tileB)> transitionPairs,
            IWfcTileCatalog? tileCatalog) =>
            new WfcTerrainSolver(transitionPairs, tileCatalog);
    }
}
