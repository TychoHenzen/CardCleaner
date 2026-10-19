using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// The single door through which code outside Wfc creates a map generator or a terrain solver. Each call builds
/// fresh adjacency rules, because the generator constructor adds gap tiles to them.
/// </summary>
public static class Wfc
{
    /// <summary>
    /// Creates a map generator over rules built from the transition pairs.
    /// </summary>
    public static IWfcMapGenerator CreateMapGenerator(
        IReadOnlyList<(string tileA, string tileB)> transitionPairs,
        IWfcTileCatalog? tileCatalog) =>
        new WfcMapGenerator(new WfcAdjacencyRules(transitionPairs), tileCatalog);

    /// <summary>
    /// Creates a terrain solver over rules built from the transition pairs.
    /// </summary>
    public static IWfcTerrainSolver CreateTerrainSolver(
        IReadOnlyList<(string tileA, string tileB)> transitionPairs,
        IWfcTileCatalog? tileCatalog) =>
        new WfcTerrainSolver(transitionPairs, tileCatalog);
}
