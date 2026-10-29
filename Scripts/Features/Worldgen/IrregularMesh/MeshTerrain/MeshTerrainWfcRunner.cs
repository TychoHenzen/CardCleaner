using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshTerrain;

internal sealed class MeshTerrainWfcRunner
{
    private readonly IWfcTerrainSolver _solver;
    private readonly ITileCatalog? _tiles;
    private readonly MeshTerrainProjection _projection;
    private readonly MeshForegroundWfc _foregroundWfc;

    public MeshTerrainWfcRunner(MeshTerrainWfcSetup setup, Dictionary<string, int>? tileToTerrainType)
    {
        _solver = setup.Solver;
        _tiles = setup.Tiles;
        _projection = new MeshTerrainProjection(setup.Tiles, tileToTerrainType);
        _foregroundWfc = new MeshForegroundWfc(setup.Tiles, SolveForeground);
    }

    private WfcGraphSolution SolveForeground(
        int[][] neighbors,
        IReadOnlyCollection<string> initialTiles,
        ulong seed,
        Func<string, bool>? gapTileFilter) =>
        _solver.SolveGraphWithCatalog(neighbors, initialTiles, seed, gapTileFilter);

    public IrregularMesh Generate(
        IrregularMesh mesh,
        BiomeSet biomes,
        Func<Vector2I, BiomeDefinition> getBiomeAt,
        Vector2I effectiveSize,
        (Vector2 Min, Vector2 Max) bounds,
        ulong seed)
    {
        var backgroundResult = _solver.GenerateBackground(
            biomes,
            getBiomeAt,
            effectiveSize,
            seed,
            tileId => _tiles?.GetTile(tileId)?.HasAutoTileVariants == false);

        if (!backgroundResult.Success || backgroundResult.TileIds == null)
        {
            ILog.Error($"[MeshTerrainGen] Background WFC failed: {backgroundResult.ErrorMessage}");
            _projection.ApplyFallbackTerrain(mesh);
            return mesh;
        }

        ILog.Print($"[MeshTerrainGen] Background WFC succeeded in {backgroundResult.Iterations} iterations");
        MeshTerrainProjection.MapBackgroundToQuads(mesh, backgroundResult.Size, backgroundResult.TileIds, bounds);

        if (!_foregroundWfc.Generate(mesh, biomes, seed + 1))
        {
            ILog.Error("[MeshTerrainGen] Direct mesh foreground WFC failed, clearing foreground");
            MeshTerrainProjection.ClearForeground(mesh);
        }

        mesh.UpdateAllCachedProperties();
        return mesh;
    }

    public void ApplyFallbackTerrain(IrregularMesh mesh)
    {
        _projection.ApplyFallbackTerrain(mesh);
    }
}
