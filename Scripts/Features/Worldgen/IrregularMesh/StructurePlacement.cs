using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Manages structure placement on irregular mesh vertices.
/// Structures are placed at vertices (data grid concept) and affect adjacent quads.
/// </summary>
public class StructurePlacement
{
    private readonly IrregularMesh _mesh;
    private readonly Dictionary<int, StructureType> _structures = new();
    private readonly Dictionary<int, Node2D> _structureVisuals = new();

    /// <summary>
    /// Raised when a structure is placed at a vertex.
    /// </summary>
    public event Action<int, StructureType>? StructurePlaced;

    /// <summary>
    /// Raised when a structure is removed from a vertex.
    /// </summary>
    public event Action<int>? StructureRemoved;

    public StructurePlacement(IrregularMesh mesh)
    {
        _mesh = mesh;
    }

    /// <summary>
    /// Check if a structure can be placed at the given vertex.
    /// </summary>
    /// <param name="vertexId">The vertex ID to check.</param>
    /// <param name="type">The structure type to place.</param>
    /// <returns>True if placement is valid.</returns>
    public bool CanPlaceStructure(int vertexId, StructureType type)
    {
        if (vertexId < 0 || vertexId >= _mesh.Vertices.Count)
            return false;

        var vertex = _mesh.Vertices[vertexId];

        // Already has a structure
        if (vertex.HasStructure)
            return false;

        // Boundary vertices cannot have structures
        if (vertex.IsBoundary)
            return false;

        // Check terrain compatibility
        if (!IsTerrainCompatible(vertex.TerrainType, type))
            return false;

        return true;
    }

    /// <summary>
    /// Place a structure at the given vertex.
    /// </summary>
    /// <param name="vertexId">The vertex ID to place the structure at.</param>
    /// <param name="type">The structure type to place.</param>
    /// <returns>True if placement succeeded.</returns>
    public bool PlaceStructure(int vertexId, StructureType type)
    {
        if (!CanPlaceStructure(vertexId, type))
            return false;

        var vertex = _mesh.Vertices[vertexId];
        vertex.HasStructure = true;
        _structures[vertexId] = type;

        // Notify adjacent quads that passability may have changed
        foreach (var quadId in vertex.AdjacentQuadIds)
        {
            // The quad's IsPassable() method will automatically check HasStructure
            // No explicit update needed since it queries vertex state
        }

        StructurePlaced?.Invoke(vertexId, type);
        return true;
    }

    /// <summary>
    /// Remove a structure from the given vertex.
    /// </summary>
    /// <param name="vertexId">The vertex ID to remove the structure from.</param>
    /// <returns>True if removal succeeded.</returns>
    public bool RemoveStructure(int vertexId)
    {
        if (vertexId < 0 || vertexId >= _mesh.Vertices.Count)
            return false;

        var vertex = _mesh.Vertices[vertexId];
        if (!vertex.HasStructure)
            return false;

        vertex.HasStructure = false;
        _structures.Remove(vertexId);

        // Clean up visual if it exists
        if (_structureVisuals.TryGetValue(vertexId, out var visual))
        {
            visual.QueueFree();
            _structureVisuals.Remove(vertexId);
        }

        StructureRemoved?.Invoke(vertexId);
        return true;
    }

    /// <summary>
    /// Get the structure type at a vertex.
    /// </summary>
    /// <param name="vertexId">The vertex ID to query.</param>
    /// <returns>The structure type, or null if no structure exists.</returns>
    public StructureType? GetStructureAt(int vertexId)
    {
        return _structures.TryGetValue(vertexId, out var type) ? type : null;
    }

    /// <summary>
    /// Get all vertices with structures.
    /// </summary>
    public IEnumerable<int> GetAllStructureVertices() => _structures.Keys;

    /// <summary>
    /// Get the count of placed structures.
    /// </summary>
    public int StructureCount => _structures.Count;

    /// <summary>
    /// Spawn a visual representation of a structure at a vertex.
    /// </summary>
    /// <param name="vertexId">The vertex where the structure is placed.</param>
    /// <param name="visual">The visual node to spawn.</param>
    /// <param name="parent">The parent node to add the visual to.</param>
    public void SpawnStructureVisual(int vertexId, Node2D visual, Node parent)
    {
        if (!_structures.ContainsKey(vertexId))
            return;

        // Remove existing visual if any
        if (_structureVisuals.TryGetValue(vertexId, out var existingVisual))
            existingVisual.QueueFree();

        var vertex = _mesh.Vertices[vertexId];
        visual.Position = vertex.Position;
        parent.AddChild(visual);
        _structureVisuals[vertexId] = visual;
    }

    /// <summary>
    /// Get the visual for a structure vertex.
    /// </summary>
    public Node2D? GetStructureVisual(int vertexId)
    {
        return _structureVisuals.TryGetValue(vertexId, out var visual) ? visual : null;
    }

    /// <summary>
    /// Clear all structures from the mesh.
    /// </summary>
    public void ClearAllStructures()
    {
        foreach (var vertexId in _structures.Keys.ToList())
        {
            RemoveStructure(vertexId);
        }
    }

    /// <summary>
    /// Get vertices that would block movement if a structure was placed at the given vertex.
    /// Useful for previewing structure placement effects.
    /// </summary>
    /// <param name="vertexId">The vertex to check.</param>
    /// <returns>IDs of quads that would become impassable.</returns>
    public IEnumerable<int> GetAffectedQuads(int vertexId)
    {
        if (vertexId < 0 || vertexId >= _mesh.Vertices.Count)
            return Enumerable.Empty<int>();

        var vertex = _mesh.Vertices[vertexId];
        return vertex.AdjacentQuadIds
            .Where(qid => _mesh.Quads[qid].IsPassable())
            .ToList();
    }

    /// <summary>
    /// Find the nearest valid placement vertex to a world position.
    /// </summary>
    /// <param name="worldPos">World position to search from.</param>
    /// <param name="type">Structure type to place.</param>
    /// <param name="maxDistance">Maximum distance to search.</param>
    /// <returns>The nearest valid vertex ID, or null if none found.</returns>
    public int? FindNearestValidPlacement(Vector2 worldPos, StructureType type, float maxDistance = 32f)
    {
        int? nearestId = null;
        float nearestDist = float.MaxValue;

        foreach (var vertex in _mesh.Vertices)
        {
            var dist = vertex.Position.DistanceTo(worldPos);
            if (dist > maxDistance)
                continue;

            if (dist < nearestDist && CanPlaceStructure(vertex.Id, type))
            {
                nearestId = vertex.Id;
                nearestDist = dist;
            }
        }

        return nearestId;
    }

    private static bool IsTerrainCompatible(int terrainType, StructureType structureType)
    {
        // Define terrain compatibility rules
        // TerrainType 0 is typically water/empty - no structures allowed
        // TerrainType 1+ are land types - most structures allowed
        return structureType switch
        {
            // Walls can only be placed on solid ground
            StructureType.Wall => terrainType > 0,

            // Doors require walls nearby, but for simplicity allow on any solid ground
            StructureType.Door => terrainType > 0,

            // Bridges can span water (terrainType 0)
            StructureType.Bridge => true,

            // Most other structures require solid ground
            _ => terrainType > 0
        };
    }
}
