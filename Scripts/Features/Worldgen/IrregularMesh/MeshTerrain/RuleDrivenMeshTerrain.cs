using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshTerrain;

/// <summary>
/// Assigns vertex terrain types by running WFC over the solver's adjacency rules and caller-supplied
/// tile-to-terrain mapping, for callers that have no tile registry or biomes.
/// </summary>
internal sealed class RuleDrivenMeshTerrain
{
    private readonly IWfcTerrainSolver _solver;
    private readonly Dictionary<string, int> _tileToTerrainType;

    internal RuleDrivenMeshTerrain(IWfcTerrainSolver solver, Dictionary<string, int> tileToTerrainType)
    {
        _solver = solver;
        _tileToTerrainType = tileToTerrainType;
    }

    /// <summary>
    /// Returns false, leaving the mesh untouched, when no tile has rules or the solve fails.
    /// </summary>
    internal bool Generate(IrregularMesh mesh, ulong seed)
    {
        var ruleTileIds = _solver.RuleTileIds;
        var tiles = _tileToTerrainType.Keys.Where(tileId => ruleTileIds.Contains(tileId)).ToList();
        if (tiles.Count == 0)
        {
            GD.PrintErr("[MeshTerrainGen] No tile has both adjacency rules and a terrain type");
            return false;
        }

        var solution = _solver.SolveGraphWithRules(IrregularMeshNeighbors.BuildNeighbors(mesh), tiles, seed);
        if (!solution.Success)
        {
            GD.PrintErr($"[MeshTerrainGen] Rule-driven mesh WFC failed: {solution.ErrorMessage}");
            return false;
        }

        for (var index = 0; index < mesh.Vertices.Count; index++)
        {
            var vertex = mesh.Vertices[index];
            vertex.TileId = null;
            vertex.ForegroundTileId = null;
            vertex.TerrainType = _tileToTerrainType[solution.CollapsedTiles[index]!];
        }

        mesh.UpdateAllCachedProperties();
        return true;
    }
}
