using System.Collections.Generic;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Wfc;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshTerrain;

/// <summary>
/// The WFC solver and the tile catalog that it wraps, paired once.
/// Mesh terrain reads tile facts from the catalog and solves with the
/// solver, so both must describe the same tiles. A null catalog means the
/// solver has no catalog. ForCatalog is the production path; direct
/// construction is for test doubles.
/// </summary>
internal sealed record MeshTerrainWfcSetup(
    IWfcTerrainSolver Solver,
    TileCatalogData? Tiles)
{
    public static MeshTerrainWfcSetup ForCatalog(
        IReadOnlyList<(string tileA, string tileB)> transitionPairs,
        TileCatalogData? tiles)
    {
        var solver = new WfcTerrainSolver(transitionPairs, tiles);
        return new MeshTerrainWfcSetup(solver, tiles);
    }
}
