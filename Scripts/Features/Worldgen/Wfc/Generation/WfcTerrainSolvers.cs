using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

internal static class WfcTerrainSolvers
{
    /// <summary>
    /// Creates a solver whose rules are the given transition pairs.
    /// </summary>
    public static IWfcTerrainSolver Create(
        IReadOnlyList<(string tileA, string tileB)> transitionPairs,
        IWfcTileCatalog? tileCatalog)
    {
        return new WfcTerrainSolver(transitionPairs, tileCatalog);
    }
}
