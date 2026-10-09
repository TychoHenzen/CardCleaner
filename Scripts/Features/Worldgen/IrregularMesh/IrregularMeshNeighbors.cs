using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Neighbor lists for an irregular mesh, passed to IWfcTerrainSolver.
/// Cells are mesh vertices. Neighbors are all vertices sharing any quad with a vertex.
/// </summary>
internal static class IrregularMeshNeighbors
{
    /// <summary>
    /// Builds the neighbor array of every vertex, indexed by vertex ID.
    /// </summary>
    internal static int[][] BuildNeighbors(IrregularMesh mesh)
    {
        var neighbors = new int[mesh.Vertices.Count][];
        for (var i = 0; i < mesh.Vertices.Count; i++)
        {
            // ASSUMPTION: HashSet<int> enumerates in insertion order while nothing is removed. The solver
            // consumes neighbors in this order, so the golden hashes in MeshTerrainFingerprintTest depend on it.
            neighbors[i] = ComputeNeighborSet(mesh, i).ToArray();
        }

        return neighbors;
    }

    /// <summary>
    /// Computes all vertices that share any quad with the given vertex.
    /// This includes edge-adjacent vertices AND corner-adjacent vertices
    /// (any vertex in any quad that has this vertex as a corner).
    /// </summary>
    private static HashSet<int> ComputeNeighborSet(IrregularMesh mesh, int vertexId)
    {
        var neighbors = new HashSet<int>();
        var vertex = mesh.Vertices[vertexId];

        foreach (var quadId in vertex.AdjacentQuadIds)
        {
            var quad = mesh.Quads[quadId];

            foreach (var otherVertexId in quad.VertexIds)
            {
                if (otherVertexId != vertexId)
                {
                    neighbors.Add(otherVertexId);
                }
            }
        }

        return neighbors;
    }
}
