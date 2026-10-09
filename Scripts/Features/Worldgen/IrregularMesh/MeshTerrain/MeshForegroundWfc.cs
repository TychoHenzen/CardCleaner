using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshTerrain;

internal sealed class MeshForegroundWfc
{
    private readonly ITileRegistry? _tileRegistry;
    private readonly IWfcTerrainSolver _solver;

    public MeshForegroundWfc(ITileRegistry? tileRegistry, IWfcTerrainSolver solver)
    {
        _tileRegistry = tileRegistry;
        _solver = solver;
    }

    public bool Generate(IrregularMesh mesh, BiomeRegistry biomeRegistry, ulong seed)
    {
        if (_tileRegistry == null)
        {
            GD.PrintErr("[MeshTerrainGen] Cannot run direct mesh WFC without tile registry");
            return false;
        }

        var initialTiles = MeshForegroundTileCandidates.Collect(_tileRegistry, biomeRegistry);
        if (initialTiles.Count == 0)
        {
            GD.PrintErr("[MeshTerrainGen] No valid tiles for mesh WFC");
            return false;
        }

        GD.Print(
            $"[MeshTerrainGen] Starting direct mesh WFC with {initialTiles.Count} tiles " +
            $"on {mesh.Vertices.Count} vertices");

        var solution = _solver.SolveGraphWithRegistry(
            IrregularMeshNeighbors.BuildNeighbors(mesh),
            initialTiles,
            seed,
            MeshForegroundTileCandidates.IsCandidate);

        if (!solution.Success)
        {
            GD.PrintErr($"[MeshTerrainGen] Direct mesh WFC failed: {solution.ErrorMessage}");
            return false;
        }

        GD.Print($"[MeshTerrainGen] Direct mesh WFC succeeded in {solution.Iterations} iterations");
        AssignTiles(mesh, solution.CollapsedTiles);
        return true;
    }

    private void AssignTiles(IrregularMesh mesh, string?[] collapsedTiles)
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

            var tile = _tileRegistry!.GetTile(tileId);
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

        GD.Print($"[MeshTerrainGen] Assigned {autoTileCount}/{mesh.Vertices.Count} vertices with auto-tiles");
    }

    private static void ClearVertex(MeshVertex vertex, int terrainType)
    {
        vertex.ForegroundTileId = null;
        vertex.TileId = null;
        vertex.TerrainType = terrainType;
    }
}
