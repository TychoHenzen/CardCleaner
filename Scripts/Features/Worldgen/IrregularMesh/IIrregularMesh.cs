namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

using Godot;
using System.Collections.Generic;

/// <summary>
/// Read-only view of an irregular quad mesh: its vertices (data grid), quads (visual grid) and spatial queries.
/// </summary>
public interface IIrregularMesh
{
    IReadOnlyList<MeshVertex> Vertices { get; }

    IReadOnlyList<IMeshQuad> Quads { get; }

    /// <summary>Axis-aligned bounding box of the mesh (min corner, max corner).</summary>
    (Vector2 Min, Vector2 Max) Bounds { get; }

    /// <summary>The quad containing a mesh position, or null if outside the mesh.</summary>
    IMeshQuad? GetQuadAtPosition(Vector2 worldPos);

    /// <summary>The vertex nearest to a mesh position, or null for an empty mesh.</summary>
    MeshVertex? GetNearestVertex(Vector2 worldPos);

    /// <summary>All quads whose bounding box intersects the rectangle.</summary>
    IEnumerable<IMeshQuad> GetQuadsIntersectingRect(Rect2 rect);

    /// <summary>The quads sharing an edge with the given quad.</summary>
    IEnumerable<IMeshQuad> GetAdjacentQuads(int quadId);

    /// <summary>The vertices adjacent to the given vertex.</summary>
    IEnumerable<MeshVertex> GetAdjacentVertices(int vertexId);
}
