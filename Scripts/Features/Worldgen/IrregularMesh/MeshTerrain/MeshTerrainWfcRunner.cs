using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshTerrain;

internal sealed class MeshTerrainWfcRunner
{
    private readonly WfcMapGenerator _wfcGenerator;
    private readonly MeshTerrainProjection _projection;
    private readonly MeshForegroundWfc _foregroundWfc;

    public MeshTerrainWfcRunner(
        WfcMapGenerator wfcGenerator,
        ITileRegistry? tileRegistry,
        CompiledTransitionResolver? transitionResolver,
        Dictionary<string, int>? tileToTerrainType)
    {
        _wfcGenerator = wfcGenerator;
        _projection = new MeshTerrainProjection(tileRegistry, tileToTerrainType);
        _foregroundWfc = new MeshForegroundWfc(tileRegistry, transitionResolver);
    }

    public IrregularMesh Generate(
        IrregularMesh mesh,
        BiomeRegistry biomeRegistry,
        Func<Vector2I, BiomeDefinition> getBiomeAt,
        Vector2I effectiveSize,
        (Vector2 Min, Vector2 Max) bounds,
        ulong seed,
        bool useDirectMeshWfc)
    {
        var backgroundResult = _wfcGenerator.GenerateMultiBiome(
            biomeRegistry,
            getBiomeAt,
            effectiveSize,
            seed,
            null,
            tile => !tile.HasAutoTileVariants);

        if (!backgroundResult.Success || backgroundResult.MapData == null)
        {
            GD.PrintErr($"[MeshTerrainGen] Background WFC failed: {backgroundResult.ErrorMessage}");
            _projection.ApplyFallbackTerrain(mesh);
            return mesh;
        }

        GD.Print($"[MeshTerrainGen] Background WFC succeeded in {backgroundResult.Iterations} iterations");
        _projection.MapBackgroundToQuads(mesh, backgroundResult.MapData, bounds);
        GenerateForeground(
            mesh,
            biomeRegistry,
            getBiomeAt,
            effectiveSize,
            bounds,
            seed + 1,
            useDirectMeshWfc);
        mesh.UpdateAllCachedProperties();
        return mesh;
    }

    public void ApplyFallbackTerrain(IrregularMesh mesh)
    {
        _projection.ApplyFallbackTerrain(mesh);
    }

    private void GenerateForeground(
        IrregularMesh mesh,
        BiomeRegistry biomeRegistry,
        Func<Vector2I, BiomeDefinition> getBiomeAt,
        Vector2I effectiveSize,
        (Vector2 Min, Vector2 Max) bounds,
        ulong seed,
        bool useDirectMeshWfc)
    {
        if (useDirectMeshWfc)
        {
            if (!_foregroundWfc.Generate(mesh, biomeRegistry, seed))
            {
                GD.PrintErr("[MeshTerrainGen] Direct mesh foreground WFC failed, clearing foreground");
                _projection.ClearForeground(mesh);
            }

            return;
        }

        GenerateLegacyForeground(
            mesh,
            biomeRegistry,
            getBiomeAt,
            effectiveSize,
            bounds,
            seed);
    }

    private void GenerateLegacyForeground(
        IrregularMesh mesh,
        BiomeRegistry biomeRegistry,
        Func<Vector2I, BiomeDefinition> getBiomeAt,
        Vector2I effectiveSize,
        (Vector2 Min, Vector2 Max) bounds,
        ulong seed)
    {
        var foregroundResult = _wfcGenerator.GenerateMultiBiome(
            biomeRegistry,
            getBiomeAt,
            effectiveSize,
            seed,
            null);

        if (!foregroundResult.Success || foregroundResult.MapData == null)
        {
            GD.PrintErr($"[MeshTerrainGen] Foreground WFC failed: {foregroundResult.ErrorMessage}");
            _projection.ClearForeground(mesh);
            return;
        }

        GD.Print($"[MeshTerrainGen] Legacy foreground WFC succeeded in {foregroundResult.Iterations} iterations");
        _projection.MapForegroundToVertices(mesh, foregroundResult.MapData, bounds);
    }
}
