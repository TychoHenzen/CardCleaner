namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// An irregular quad mesh for terrain rendering.
/// Contains vertices (data grid) and quads (visual grid) with precomputed adjacency.
/// </summary>
public class IrregularMesh
{
    public List<MeshVertex> Vertices { get; } = new();
    public List<MeshQuad> Quads { get; } = new();

    /// <summary>
    /// Axis-aligned bounding box of the mesh (min corner, max corner).
    /// </summary>
    public (Vector2 Min, Vector2 Max) Bounds { get; private set; }

    /// <summary>
    /// Add a vertex to the mesh and return its ID.
    /// </summary>
    public int AddVertex(Vector2 position)
    {
        int id = Vertices.Count;
        Vertices.Add(new MeshVertex(id, position));
        return id;
    }

    /// <summary>
    /// Add a quad to the mesh and return its ID.
    /// Vertex IDs must already exist in the mesh.
    /// </summary>
    public int AddQuad(int[] vertexIds)
    {
        if (vertexIds.Any(id => id < 0 || id >= Vertices.Count))
            throw new ArgumentException("Invalid vertex ID in quad");

        int id = Quads.Count;
        var quad = new MeshQuad(id, vertexIds, this);
        Quads.Add(quad);

        // Register this quad with its corner vertices
        foreach (int vertexId in vertexIds)
        {
            Vertices[vertexId].AdjacentQuadIds.Add(id);
        }

        return id;
    }

    /// <summary>
    /// Build adjacency relationships between vertices and quads.
    /// Call after all vertices and quads have been added.
    /// </summary>
    public void BuildAdjacency()
    {
        BuildVertexAdjacency();
        BuildQuadAdjacency();
        IdentifyBoundaryVertices();
        UpdateAllCachedProperties();
        ComputeBounds();
    }

    /// <summary>
    /// Get the quad containing a world position, or null if outside mesh.
    /// Uses bounding box pre-filter then point-in-polygon test.
    /// </summary>
    public MeshQuad? GetQuadAtPosition(Vector2 worldPos)
    {
        // Quick bounds check
        if (worldPos.X < Bounds.Min.X || worldPos.X > Bounds.Max.X ||
            worldPos.Y < Bounds.Min.Y || worldPos.Y > Bounds.Max.Y)
        {
            return null;
        }

        // Linear search with point-in-polygon test
        // TODO: Optimize with spatial hash or quad-tree for large meshes
        foreach (var quad in Quads)
        {
            if (quad.ContainsPoint(worldPos))
                return quad;
        }

        return null;
    }

    /// <summary>
    /// Get the nearest vertex to a world position.
    /// </summary>
    public MeshVertex? GetNearestVertex(Vector2 worldPos)
    {
        if (Vertices.Count == 0)
            return null;

        return Vertices.MinBy(v => v.Position.DistanceSquaredTo(worldPos));
    }

    /// <summary>
    /// Get all quads that intersect a rectangle (for structure placement checks).
    /// </summary>
    public IEnumerable<MeshQuad> GetQuadsIntersectingRect(Rect2 rect)
    {
        foreach (var quad in Quads)
        {
            // Quick AABB check first
            var corners = quad.GetCornerPositions();
            var quadMin = new Vector2(corners.Min(c => c.X), corners.Min(c => c.Y));
            var quadMax = new Vector2(corners.Max(c => c.X), corners.Max(c => c.Y));

            var quadRect = new Rect2(quadMin, quadMax - quadMin);

            if (quadRect.Intersects(rect))
            {
                yield return quad;
            }
        }
    }

    /// <summary>
    /// Get adjacent quads for a given quad ID.
    /// </summary>
    public IEnumerable<MeshQuad> GetAdjacentQuads(int quadId)
    {
        if (quadId < 0 || quadId >= Quads.Count)
            yield break;

        foreach (int adjacentId in Quads[quadId].AdjacentQuadIds)
        {
            yield return Quads[adjacentId];
        }
    }

    /// <summary>
    /// Get adjacent vertices for a given vertex ID.
    /// </summary>
    public IEnumerable<MeshVertex> GetAdjacentVertices(int vertexId)
    {
        if (vertexId < 0 || vertexId >= Vertices.Count)
            yield break;

        foreach (int adjacentId in Vertices[vertexId].AdjacentVertexIds)
        {
            yield return Vertices[adjacentId];
        }
    }

    /// <summary>
    /// Recalculate all cached properties (centroids, areas, bounds).
    /// Call after vertex positions have changed (e.g., after relaxation).
    /// </summary>
    public void UpdateAllCachedProperties()
    {
        foreach (var quad in Quads)
        {
            quad.UpdateCachedProperties();
        }
        ComputeBounds();
    }

    private void BuildVertexAdjacency()
    {
        // Clear existing adjacency
        foreach (var vertex in Vertices)
        {
            vertex.AdjacentVertexIds.Clear();
        }

        // Two vertices are adjacent if they share a quad edge
        // For each quad, add edges between consecutive vertices
        var processedEdges = new HashSet<(int, int)>();

        foreach (var quad in Quads)
        {
            var vids = quad.VertexIds;
            for (int i = 0; i < 4; i++)
            {
                int v1 = vids[i];
                int v2 = vids[(i + 1) % 4];

                // Normalize edge direction for deduplication
                var edge = v1 < v2 ? (v1, v2) : (v2, v1);

                if (processedEdges.Add(edge))
                {
                    Vertices[v1].AdjacentVertexIds.Add(v2);
                    Vertices[v2].AdjacentVertexIds.Add(v1);
                }
            }
        }
    }

    private void BuildQuadAdjacency()
    {
        // Clear existing adjacency
        foreach (var quad in Quads)
        {
            quad.AdjacentQuadIds.Clear();
        }

        // Build edge-to-quads map
        var edgeToQuads = new Dictionary<(int, int), List<int>>();

        foreach (var quad in Quads)
        {
            var vids = quad.VertexIds;
            for (int i = 0; i < 4; i++)
            {
                int v1 = vids[i];
                int v2 = vids[(i + 1) % 4];

                // Normalize edge direction
                var edge = v1 < v2 ? (v1, v2) : (v2, v1);

                if (!edgeToQuads.ContainsKey(edge))
                    edgeToQuads[edge] = new List<int>();

                edgeToQuads[edge].Add(quad.Id);
            }
        }

        // Quads sharing an edge are adjacent
        foreach (var quadList in edgeToQuads.Values)
        {
            if (quadList.Count == 2)
            {
                int q1 = quadList[0];
                int q2 = quadList[1];

                if (!Quads[q1].AdjacentQuadIds.Contains(q2))
                    Quads[q1].AdjacentQuadIds.Add(q2);

                if (!Quads[q2].AdjacentQuadIds.Contains(q1))
                    Quads[q2].AdjacentQuadIds.Add(q1);
            }
        }
    }

    private void IdentifyBoundaryVertices()
    {
        // A vertex is on the boundary if it belongs to an edge that's only in one quad
        var edgeFaceCount = new Dictionary<(int, int), int>();

        foreach (var quad in Quads)
        {
            var vids = quad.VertexIds;
            for (int i = 0; i < 4; i++)
            {
                int v1 = vids[i];
                int v2 = vids[(i + 1) % 4];
                var edge = v1 < v2 ? (v1, v2) : (v2, v1);

                edgeFaceCount[edge] = edgeFaceCount.GetValueOrDefault(edge, 0) + 1;
            }
        }

        // Mark boundary vertices
        foreach (var vertex in Vertices)
        {
            vertex.IsBoundary = false;
        }

        foreach (var (edge, count) in edgeFaceCount)
        {
            if (count == 1)
            {
                Vertices[edge.Item1].IsBoundary = true;
                Vertices[edge.Item2].IsBoundary = true;
            }
        }
    }

    private void ComputeBounds()
    {
        if (Vertices.Count == 0)
        {
            Bounds = (Vector2.Zero, Vector2.Zero);
            return;
        }

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var vertex in Vertices)
        {
            minX = Mathf.Min(minX, vertex.Position.X);
            minY = Mathf.Min(minY, vertex.Position.Y);
            maxX = Mathf.Max(maxX, vertex.Position.X);
            maxY = Mathf.Max(maxY, vertex.Position.Y);
        }

        Bounds = (new Vector2(minX, minY), new Vector2(maxX, maxY));
    }

    /// <summary>
    /// Get statistics about the mesh.
    /// </summary>
    public MeshStatistics GetStatistics()
    {
        var areas = Quads.Select(q => q.Area).ToList();
        int boundaryCount = Vertices.Count(v => v.IsBoundary);

        return new MeshStatistics
        {
            VertexCount = Vertices.Count,
            QuadCount = Quads.Count,
            BoundaryVertexCount = boundaryCount,
            MinArea = areas.Count > 0 ? areas.Min() : 0,
            MaxArea = areas.Count > 0 ? areas.Max() : 0,
            AvgArea = areas.Count > 0 ? areas.Average() : 0,
            AreaStdDev = areas.Count > 0 ? ComputeStdDev(areas) : 0,
            Bounds = Bounds
        };
    }

    private static float ComputeStdDev(List<float> values)
    {
        if (values.Count == 0) return 0;
        float avg = (float)values.Average();
        float sumSquares = values.Sum(v => (v - avg) * (v - avg));
        return Mathf.Sqrt(sumSquares / values.Count);
    }
}

/// <summary>
/// Statistics about the mesh for debugging and validation.
/// </summary>
public struct MeshStatistics
{
    public int VertexCount;
    public int QuadCount;
    public int BoundaryVertexCount;
    public float MinArea;
    public float MaxArea;
    public double AvgArea;
    public float AreaStdDev;
    public (Vector2 Min, Vector2 Max) Bounds;

    public override readonly string ToString() =>
        $"Vertices: {VertexCount}, Quads: {QuadCount}, Boundary: {BoundaryVertexCount}\n" +
        $"Area: min={MinArea:F4}, max={MaxArea:F4}, avg={AvgArea:F4}, std={AreaStdDev:F4}\n" +
        $"Bounds: {Bounds.Min} to {Bounds.Max}";
}
