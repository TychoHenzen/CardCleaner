using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshTerrain;

internal sealed class MeshTerrainWfcRunner
{
    private readonly IWfcTerrainSolver _solver;
    private readonly ITileRegistry? _tileRegistry;
    private readonly MeshTerrainProjection _projection;
    private readonly MeshForegroundWfc _foregroundWfc;

    public MeshTerrainWfcRunner(
        IWfcTerrainSolver solver,
        ITileRegistry? tileRegistry,
        Dictionary<string, int>? tileToTerrainType)
    {
        _solver = solver;
        _tileRegistry = tileRegistry;
        _projection = new MeshTerrainProjection(tileRegistry, tileToTerrainType);
        _foregroundWfc = new MeshForegroundWfc(solver, tileRegistry);
    }

    public IrregularMesh Generate(
        IrregularMesh mesh,
        BiomeRegistry biomeRegistry,
        Func<Vector2I, BiomeDefinition> getBiomeAt,
        Vector2I effectiveSize,
        (Vector2 Min, Vector2 Max) bounds,
        ulong seed)
    {
        var backgroundResult = _solver.GenerateBackground(
            biomeRegistry,
            getBiomeAt,
            effectiveSize,
            seed,
            tileId => _tileRegistry?.GetTile(tileId)?.HasAutoTileVariants == false);

        if (!backgroundResult.Success || backgroundResult.TileIds == null)
        {
            GD.PrintErr($"[MeshTerrainGen] Background WFC failed: {backgroundResult.ErrorMessage}");
            _projection.ApplyFallbackTerrain(mesh);
            return mesh;
        }

        GD.Print($"[MeshTerrainGen] Background WFC succeeded in {backgroundResult.Iterations} iterations");
        MeshTerrainProjection.MapBackgroundToQuads(mesh, backgroundResult.Size, backgroundResult.TileIds, bounds);

        if (!_foregroundWfc.Generate(mesh, biomeRegistry, seed + 1))
        {
            GD.PrintErr("[MeshTerrainGen] Direct mesh foreground WFC failed, clearing foreground");
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
