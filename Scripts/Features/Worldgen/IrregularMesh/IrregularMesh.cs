namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Topology;

/// <summary>
/// An irregular quad mesh for terrain rendering.
/// Contains vertices (data grid) and quads (visual grid) with precomputed adjacency.
/// </summary>
public class IrregularMesh : IIrregularMesh
{
    public List<MeshVertex> Vertices { get; } = new();
    public List<MeshQuad> Quads { get; } = new();

    IReadOnlyList<MeshVertex> IIrregularMesh.Vertices => Vertices;
    IReadOnlyList<IMeshQuad> IIrregularMesh.Quads => Quads;

    /// <summary>
    /// Axis-aligned bounding box of the mesh (min corner, max corner).
    /// </summary>
    public (Vector2 Min, Vector2 Max) Bounds { get; private set; }

    // Spatial hash grid for O(1) quad lookup
    private readonly MeshSpatialIndex _spatialIndex = new();

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
        MeshAdjacencyBuilder.BuildVertexAdjacency(this);
        MeshAdjacencyBuilder.BuildQuadAdjacency(this);
        MeshAdjacencyBuilder.IdentifyBoundaryVertices(this);
        UpdateAllCachedProperties();
        ComputeBounds();
        _spatialIndex.Rebuild(Quads);
    }

    /// <summary>
    /// Get the quad containing a world position, or null if outside mesh.
    /// Uses spatial hash for O(1) average lookup.
    /// </summary>
    public MeshQuad? GetQuadAtPosition(Vector2 worldPos)
    {
        // Quick bounds check
        if (worldPos.X < Bounds.Min.X || worldPos.X > Bounds.Max.X ||
            worldPos.Y < Bounds.Min.Y || worldPos.Y > Bounds.Max.Y)
        {
            return null;
        }

        // Check quads in this grid cell only
        var quadIds = _spatialIndex.GetCandidates(worldPos);
        if (quadIds != null)
        {
            foreach (int quadId in quadIds)
            {
                if (Quads[quadId].ContainsPoint(worldPos))
                    return Quads[quadId];
            }
        }

        return null;
    }

    IMeshQuad? IIrregularMesh.GetQuadAtPosition(Vector2 worldPos) => GetQuadAtPosition(worldPos);

    IEnumerable<IMeshQuad> IIrregularMesh.GetQuadsIntersectingRect(Rect2 rect) => GetQuadsIntersectingRect(rect);

    IEnumerable<IMeshQuad> IIrregularMesh.GetAdjacentQuads(int quadId) => GetAdjacentQuads(quadId);

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
    /// Get all quads that intersect a rectangle.
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
        _spatialIndex.Rebuild(Quads);
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
    internal MeshStatistics GetStatistics() =>
        MeshStatistics.Of(Vertices, Quads.Select(q => q.Area).ToList(), Bounds);
}
