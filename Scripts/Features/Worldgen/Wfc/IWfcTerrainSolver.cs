using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
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
        BiomeRegistry biomes,
        Func<Vector2I, BiomeDefinition> getBiomeAt,
        Vector2I size,
        ulong seed,
        Func<TileDefinition, bool> tileFilter);

    /// <summary>
    /// Solves a cell graph with the registry's tiles. Gap-tile adjacencies cover the registry tiles passing the filter.
    /// </summary>
    WfcGraphSolution SolveGraphWithRegistry(
        int[][] neighbors,
        IReadOnlyCollection<string> initialTiles,
        ulong seed,
        Func<TileDefinition, bool>? gapTileFilter);

    /// <summary>
    /// Solves a cell graph with the solver's own adjacency rules.
    /// </summary>
    WfcGraphSolution SolveGraphWithRules(int[][] neighbors, IReadOnlyCollection<string> initialTiles, ulong seed);

    /// <summary>
    /// Creates a solver whose rules are the compiled transitions plus the given extra adjacencies.
    /// </summary>
    static IWfcTerrainSolver Create(
        IReadOnlyDictionary<string, HashSet<string>>? extraAdjacency,
        ITileRegistry? tileRegistry) => new WfcTerrainSolver(extraAdjacency, tileRegistry);
}
