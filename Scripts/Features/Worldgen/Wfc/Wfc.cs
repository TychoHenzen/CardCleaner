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

    /// <summary>
    /// The production map generator factory. It delegates to <see cref="CreateMapGenerator"/>, so the adjacency rules
    /// are still built in one place, and it lives in the front door so the factory and the rules stay in one module.
    /// </summary>
    public sealed class MapGeneratorFactory : IWfcMapGeneratorFactory
    {
        /// <inheritdoc />
        public IWfcMapGenerator Create(
            IReadOnlyList<(string tileA, string tileB)> transitionPairs,
            IWfcTileCatalog? tileCatalog) =>
            CreateMapGenerator(transitionPairs, tileCatalog);
    }
}
