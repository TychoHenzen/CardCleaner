using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Topology;

/// <summary>
/// Derives vertex adjacency, quad adjacency and boundary flags from the quad edges of a mesh.
/// </summary>
internal static class MeshAdjacencyBuilder
{
    private const int CornersPerQuad = 4;

    /// <summary>
    /// Two vertices are adjacent if they share a quad edge.
    /// </summary>
    internal static void BuildVertexAdjacency(IrregularMesh mesh)
    {
        foreach (var vertex in mesh.Vertices)
        {
            vertex.AdjacentVertexIds.Clear();
        }

        var processedEdges = new HashSet<(int, int)>();

        foreach (var quad in mesh.Quads)
        {
            foreach (var edge in EnumerateEdges(quad))
            {
                if (!processedEdges.Add(edge))
                    continue;

                mesh.Vertices[edge.Item1].AdjacentVertexIds.Add(edge.Item2);
                mesh.Vertices[edge.Item2].AdjacentVertexIds.Add(edge.Item1);
            }
        }
    }

    /// <summary>
    /// Two quads are adjacent if they are the only two quads sharing an edge.
    /// </summary>
    internal static void BuildQuadAdjacency(IrregularMesh mesh)
    {
        foreach (var quad in mesh.Quads)
        {
            quad.AdjacentQuadIds.Clear();
        }

        var edgeToQuads = new Dictionary<(int, int), List<int>>();

        foreach (var quad in mesh.Quads)
        {
            foreach (var edge in EnumerateEdges(quad))
            {
                if (!edgeToQuads.ContainsKey(edge))
                    edgeToQuads[edge] = new List<int>();

                edgeToQuads[edge].Add(quad.Id);
            }
        }

        foreach (var quadList in edgeToQuads.Values)
        {
            if (quadList.Count == 2)
                LinkQuads(mesh, quadList[0], quadList[1]);
        }
    }

    /// <summary>
    /// A vertex is on the boundary if it belongs to an edge that is only in one quad.
    /// </summary>
    internal static void IdentifyBoundaryVertices(IrregularMesh mesh)
    {
        var edgeFaceCount = new Dictionary<(int, int), int>();

        foreach (var quad in mesh.Quads)
        {
            foreach (var edge in EnumerateEdges(quad))
            {
                edgeFaceCount[edge] = edgeFaceCount.GetValueOrDefault(edge, 0) + 1;
            }
        }

        foreach (var vertex in mesh.Vertices)
        {
            vertex.IsBoundary = false;
        }

        foreach (var (edge, count) in edgeFaceCount)
        {
            if (count == 1)
            {
                mesh.Vertices[edge.Item1].IsBoundary = true;
                mesh.Vertices[edge.Item2].IsBoundary = true;
            }
        }
    }

    private static void LinkQuads(IrregularMesh mesh, int first, int second)
    {
        if (!mesh.Quads[first].AdjacentQuadIds.Contains(second))
            mesh.Quads[first].AdjacentQuadIds.Add(second);

        if (!mesh.Quads[second].AdjacentQuadIds.Contains(first))
            mesh.Quads[second].AdjacentQuadIds.Add(first);
    }

    /// <summary>
    /// Yields each quad edge with its vertex ids normalized so the smaller id comes first.
    /// </summary>
    private static IEnumerable<(int, int)> EnumerateEdges(MeshQuad quad)
    {
        var vids = quad.VertexIds;
        for (int i = 0; i < CornersPerQuad; i++)
        {
            int v1 = vids[i];
            int v2 = vids[(i + 1) % CornersPerQuad];
            yield return v1 < v2 ? (v1, v2) : (v2, v1);
        }
    }
}
