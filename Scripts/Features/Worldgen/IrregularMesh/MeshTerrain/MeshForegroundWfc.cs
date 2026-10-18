using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshTerrain;

internal sealed class MeshForegroundWfc
{
    private readonly TileCatalogData? _tiles;
    private readonly Func<int[][], IReadOnlyCollection<string>, ulong, Func<string, bool>?, WfcGraphSolution> _solve;

    public MeshForegroundWfc(
        TileCatalogData? tiles,
        Func<int[][], IReadOnlyCollection<string>, ulong, Func<string, bool>?, WfcGraphSolution> solve)
    {
        _tiles = tiles;
        _solve = solve;
    }

    public bool Generate(IrregularMesh mesh, BiomeSet biomes, ulong seed)
    {
        var tiles = _tiles;
        if (tiles == null)
        {
            ILog.Error("[MeshTerrainGen] Cannot run direct mesh WFC without tile catalog");
            return false;
        }

        var initialTiles = MeshForegroundTileCandidates.Collect(tiles, biomes.Ids);
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
            tileId => tiles.GetTile(tileId) is { } tile && MeshForegroundTileCandidates.IsCandidate(tile));

        if (!solution.Success)
        {
            ILog.Error($"[MeshTerrainGen] Direct mesh WFC failed: {solution.ErrorMessage}");
            return false;
        }

        ILog.Print($"[MeshTerrainGen] Direct mesh WFC succeeded in {solution.Iterations} iterations");
        AssignTiles(mesh, solution.CollapsedTiles, tiles);
        return true;
    }

    private static void AssignTiles(IrregularMesh mesh, string?[] collapsedTiles, TileCatalogData tiles)
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

            var tile = tiles.GetTile(tileId);
            var visibleTileId = tile?.HasAutoTileVariants == true ? tileId : null;
            vertex.ForegroundTileId = visibleTileId;
            vertex.TileId = visibleTileId;
            if (visibleTileId != null)
            {
                autoTileCount++;
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
