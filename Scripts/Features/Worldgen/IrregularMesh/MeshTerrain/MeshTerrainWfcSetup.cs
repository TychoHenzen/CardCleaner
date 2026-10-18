using System.Collections.Generic;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Wfc;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshTerrain;

/// <summary>
/// The WFC solver and the registry that its catalog wraps, paired once.
/// Mesh terrain reads tile facts from the registry and solves with the
/// solver, so both must describe the same tiles. A null registry means the
/// solver has no catalog. ForRegistry is the production path; direct
/// construction is for test doubles.
/// </summary>
internal sealed record MeshTerrainWfcSetup(
    IWfcTerrainSolver Solver,
    ITileRegistry? TileRegistry)
{
    public static MeshTerrainWfcSetup ForRegistry(
        IReadOnlyList<(string tileA, string tileB)> transitionPairs,
        ITileRegistry? registry)
    {
        var solver = new WfcTerrainSolver(
            transitionPairs,
            registry != null ? TileCatalogData.From(registry) : null);
        return new MeshTerrainWfcSetup(solver, registry);
    }
}
