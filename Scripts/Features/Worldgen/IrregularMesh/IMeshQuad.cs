namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

using Godot;
using System.Collections.Generic;

/// <summary>
/// Read-only view of one quad of the mesh: its four corner vertices, cached geometry and terrain queries.
/// </summary>
public interface IMeshQuad
{
    // Corner16 bitmask constants (matching NeighborBitmaskCorner.cs)
    const int NE = 1;  // 0b0001
    const int SE = 2;  // 0b0010
    const int SW = 4;  // 0b0100
    const int NW = 8;  // 0b1000

    int Id { get; }

    /// <summary>IDs of the 4 corner vertices, in CCW order.</summary>
    IReadOnlyList<int> VertexIds { get; }

    /// <summary>Cached centroid position.</summary>
    Vector2 Centroid { get; }

    /// <summary>Cached area.</summary>
    float Area { get; }

    /// <summary>IDs of the quads sharing an edge with this one.</summary>
    IReadOnlyList<int> AdjacentQuadIds { get; }

    /// <summary>Background tile ID for this face (non-auto-tile from WFC phase 1).</summary>
    string? BackgroundTileId { get; }

    /// <summary>The corner vertices oriented as [SW, SE, NE, NW].</summary>
    MeshVertex[] GetSortedCorners();

    /// <summary>Corner positions in order: SW, SE, NE, NW.</summary>
    Vector2[] GetCornerPositions();

    /// <summary>Corner16 bitmask of the corners whose terrain type is the filled type.</summary>
    int ComputeCorner16Bitmask(int filledTerrainType = 1);

    /// <summary>Whether the point lies inside this quad.</summary>
    bool ContainsPoint(Vector2 point);

    /// <summary>Variant index of the first filled corner that has one, or -1 for position-based selection.</summary>
    int GetVariantIndex(int filledTerrainType = 1);

    /// <summary>The most common corner tile ID, preferring passable tiles, or null when none is assigned.</summary>
    string? GetDominantTileId();
}
