using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// The front door from irregular-mesh terrain generation into WFC. Callers pass neighbour lists and tile sets
/// and get collapsed tiles back; they do not reach the solver, its constraints or its rules.
/// </summary>
public interface IWfcTerrainSolver
{
    /// <summary>
    /// Tile ids the rules knew when the solver was built, in enumeration order, before any gap tiles were added.
    /// </summary>
    IReadOnlyList<string> RuleTileIds { get; }

    /// <summary>
    /// Generates the background grid over the biomes. Only tiles passing the filter take part.
    /// </summary>
    WfcGenerationResult GenerateBackground(
        BiomeSet biomes,
        Func<Vector2I, BiomeDefinition> getBiomeAt,
        Vector2I size,
        ulong seed,
        Func<string, bool> tileFilter);

    /// <summary>
    /// Solves a cell graph with the catalog's tiles. Gap-tile adjacencies cover the catalog tiles passing the filter.
    /// </summary>
    WfcGraphSolution SolveGraphWithCatalog(
        int[][] neighbors,
        IReadOnlyCollection<string> initialTiles,
        ulong seed,
        Func<string, bool>? gapTileFilter);

    /// <summary>
    /// Builds a solver over fresh rules. The container finds the production implementation, the internal
    /// WfcMapGenerator.Factory, so callers outside Wfc hold this interface and never the solver class.
    /// </summary>
    public interface IFactory
    {
        IWfcTerrainSolver Create(
            IReadOnlyList<(string tileA, string tileB)> transitionPairs,
            IWfcTileCatalog? tileCatalog);
    }
}
