namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

using Godot;
using System.Collections.Generic;

/// <summary>
/// A vertex in the irregular mesh, holding position and terrain data.
/// Vertices form the "data grid" - terrain types are assigned here,
/// and quads sample their corner vertices to compute bitmasks.
/// </summary>
public class MeshVertex
{
    public int Id { get; }
    public Vector2 Position { get; set; }

    /// <summary>
    /// Terrain type at this vertex (tile ID or terrain enum value).
    /// Used by adjacent quads to compute Corner16 bitmasks.
    /// </summary>
    public int TerrainType { get; set; }

    /// <summary>
    /// Whether this vertex has a blocking structure.
    /// Affects passability of adjacent quads.
    /// </summary>
    public bool HasStructure { get; set; }

    /// <summary>
    /// IDs of quads that have this vertex as a corner.
    /// Populated during mesh building.
    /// </summary>
    public List<int> AdjacentQuadIds { get; } = new();

    /// <summary>
    /// IDs of vertices connected to this one by mesh edges.
    /// Populated during mesh building.
    /// </summary>
    public List<int> AdjacentVertexIds { get; } = new();

    /// <summary>
    /// Whether this vertex is on the outer boundary of the mesh.
    /// Boundary vertices are pinned during relaxation.
    /// </summary>
    public bool IsBoundary { get; set; }

    public MeshVertex(int id, Vector2 position)
    {
        Id = id;
        Position = position;
    }

    public override string ToString() => $"Vertex[{Id}] at {Position}";
}
