using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshTerrain;

/// <summary>
/// Assigns vertex terrain types by running WFC over caller-supplied adjacency rules and
/// tile-to-terrain mapping, for callers that have no tile registry or biomes.
/// </summary>
internal sealed class RuleDrivenMeshTerrain
{
    private readonly WfcAdjacencyRules _rules;
    private readonly Dictionary<string, int> _tileToTerrainType;

    internal RuleDrivenMeshTerrain(WfcAdjacencyRules rules, Dictionary<string, int> tileToTerrainType)
    {
        _rules = rules;
        _tileToTerrainType = tileToTerrainType;
    }

    /// <summary>
    /// Returns false, leaving the mesh untouched, when no tile has rules or the solve fails.
    /// </summary>
    internal bool Generate(IrregularMesh mesh, ulong seed)
    {
        var tiles = _tileToTerrainType.Keys.Where(_rules.AllTileIds.Contains).ToList();
        if (tiles.Count == 0)
        {
            GD.PrintErr("[MeshTerrainGen] No tile has both adjacency rules and a terrain type");
            return false;
        }

        var topology = new WfcNeighborListTopology(IrregularMeshWfcTopology.BuildNeighbors(mesh), tiles);
        var solver = new WfcSolver(new WfcPropagator(_rules), new WfcTileSelector())
        {
            MaxIterations = mesh.Vertices.Count * 2
        };
        var result = solver.Solve(topology, null, new RandomNumberGenerator { Seed = seed });

        if (!result.Success)
        {
            GD.PrintErr($"[MeshTerrainGen] Rule-driven mesh WFC failed: {result.ErrorMessage}");
            return false;
        }

        for (var index = 0; index < mesh.Vertices.Count; index++)
        {
            var vertex = mesh.Vertices[index];
            vertex.TileId = null;
            vertex.ForegroundTileId = null;
            vertex.TerrainType = _tileToTerrainType[topology.GetCollapsedTileAt(index)!];
        }

        mesh.UpdateAllCachedProperties();
        return true;
    }
}
