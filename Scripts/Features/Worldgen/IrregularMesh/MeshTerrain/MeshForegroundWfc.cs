using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshTerrain;

internal sealed class MeshForegroundWfc
{
    private readonly ITileRegistry? _tileRegistry;
    private readonly Func<int[][], IReadOnlyCollection<string>, ulong, Func<string, bool>?, WfcGraphSolution> _solve;

    public MeshForegroundWfc(
        ITileRegistry? tileRegistry,
        Func<int[][], IReadOnlyCollection<string>, ulong, Func<string, bool>?, WfcGraphSolution> solve)
    {
        _tileRegistry = tileRegistry;
        _solve = solve;
    }

    public bool Generate(IrregularMesh mesh, BiomeRegistry biomeRegistry, ulong seed)
    {
        var tileRegistry = _tileRegistry;
        if (tileRegistry == null)
        {
            ILog.Error("[MeshTerrainGen] Cannot run direct mesh WFC without tile registry");
            return false;
        }

        var initialTiles = MeshForegroundTileCandidates.Collect(tileRegistry, biomeRegistry);
        if (initialTiles.Count == 0)
        {
            ILog.Error("[MeshTerrainGen] No valid tiles for mesh WFC");
            return false;
        }

        ILog.Print(
            $"[MeshTerrainGen] Starting direct mesh WFC with {initialTiles.Count} tiles " +
            $"on {mesh.Vertices.Count} vertices");

        var solution = _solve(
            IrregularMeshNeighbors.BuildNeighbors(mesh),
            initialTiles,
            seed,
            tileId => tileRegistry.GetTile(tileId) is { } tile && MeshForegroundTileCandidates.IsCandidate(tile));

        if (!solution.Success)
        {
            ILog.Error($"[MeshTerrainGen] Direct mesh WFC failed: {solution.ErrorMessage}");
            return false;
        }

        ILog.Print($"[MeshTerrainGen] Direct mesh WFC succeeded in {solution.Iterations} iterations");
        AssignTiles(mesh, solution.CollapsedTiles, tileRegistry);
        return true;
    }

    private static void AssignTiles(IrregularMesh mesh, string?[] collapsedTiles, ITileRegistry tileRegistry)
    {
        var autoTileCount = 0;

        for (var index = 0; index < mesh.Vertices.Count; index++)
        {
            var tileId = collapsedTiles[index];
            var vertex = mesh.Vertices[index];

            if (tileId == null)
            {
                ClearVertex(vertex, 1);
                continue;
            }

            var tile = tileRegistry.GetTile(tileId);
            if (tile?.HasAutoTileVariants == true)
            {
                vertex.ForegroundTileId = tileId;
                vertex.TileId = tileId;
                autoTileCount++;
            }
            else
            {
                vertex.ForegroundTileId = null;
                vertex.TileId = null;
            }

            vertex.TerrainType = tile?.IsPassable == true ? 1 : 0;
        }

        ILog.Print($"[MeshTerrainGen] Assigned {autoTileCount}/{mesh.Vertices.Count} vertices with auto-tiles");
    }

    private static void ClearVertex(MeshVertex vertex, int terrainType)
    {
        vertex.ForegroundTileId = null;
        vertex.TileId = null;
        vertex.TerrainType = terrainType;
    }
}
