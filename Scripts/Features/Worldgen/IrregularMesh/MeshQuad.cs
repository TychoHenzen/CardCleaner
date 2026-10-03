namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A quad face in the irregular mesh, defined by 4 corner vertices.
/// Quads form the "visual grid" - each quad renders an auto-tile
/// based on the Corner16 bitmask computed from its corner terrain.
/// </summary>
public class MeshQuad
{
    // Corner16 bitmask constants (matching NeighborBitmaskCorner.cs)
    public const int NE = 1;  // 0b0001
    public const int SE = 2;  // 0b0010
    public const int SW = 4;  // 0b0100
    public const int NW = 8;  // 0b1000

    public int Id { get; }

    /// <summary>
    /// IDs of the 4 corner vertices, stored in CCW order.
    /// Order is determined by angle from centroid during finalization.
    /// </summary>
    public int[] VertexIds { get; }

    /// <summary>
    /// Cached centroid position. Updated when vertices move.
    /// </summary>
    public Vector2 Centroid { get; private set; }

    /// <summary>
    /// Cached area. Updated when vertices move.
    /// </summary>
    public float Area { get; private set; }

    /// <summary>
    /// IDs of adjacent quads (sharing an edge).
    /// Populated during mesh building.
    /// </summary>
    public List<int> AdjacentQuadIds { get; } = new();

    /// <summary>
    /// Background tile ID for this face (non-auto-tile from WFC phase 1).
    /// Each quad (visual cell) has exactly one background tile.
    /// </summary>
    public string? BackgroundTileId { get; set; }

    private readonly IrregularMesh _mesh;

    public MeshQuad(int id, int[] vertexIds, IrregularMesh mesh)
    {
        if (vertexIds.Length != 4)
            throw new ArgumentException("Quad must have exactly 4 vertices", nameof(vertexIds));

        Id = id;
        VertexIds = vertexIds;
        _mesh = mesh;
    }

    /// <summary>
    /// Get the corner vertices in CCW order, oriented so index 3 is the most NW vertex.
    /// Returns: [SW, SE, NE, NW] based on actual spatial position relative to centroid.
    /// This ensures consistent compass-direction mapping regardless of quad rotation.
    /// </summary>
    public MeshVertex[] GetSortedCorners()
    {
        var vertices = VertexIds.Select(id => _mesh.Vertices[id]).ToArray();
        var centroid = Centroid;

        // Step 1: Sort by angle from centroid to get consistent CCW winding
        var ccwSorted = vertices
            .OrderBy(v => Mathf.Atan2(v.Position.Y - centroid.Y, v.Position.X - centroid.X))
            .ToArray();

        // Step 2: Find the "most NW" vertex by spatial position
        // In Y-down coordinates (Godot 2D): NW means smallest X (west) and smallest Y (north)
        // Score = -relativeX - relativeY (maximize to find NW)
        int nwIndex = 0;
        float bestNwScore = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            var relPos = ccwSorted[i].Position - centroid;
            float nwScore = -relPos.X - relPos.Y;
            if (nwScore > bestNwScore)
            {
                bestNwScore = nwScore;
                nwIndex = i;
            }
        }

        // Step 3: Rotate array so NW vertex ends up at index 3
        // This preserves CCW winding while ensuring spatial orientation
        int shift = (nwIndex + 1) % 4;
        var result = new MeshVertex[4];
        for (int i = 0; i < 4; i++)
        {
            result[i] = ccwSorted[(i + shift) % 4];
        }

        return result;
    }

    /// <summary>
    /// Get corner positions in order: SW, SE, NE, NW (for UV mapping).
    /// </summary>
    public Vector2[] GetCornerPositions()
    {
        return GetSortedCorners().Select(v => v.Position).ToArray();
    }

    /// <summary>
    /// Compute Corner16 bitmask based on corner vertex terrain.
    /// Uses spatially-oriented corner classification.
    /// </summary>
    /// <param name="filledTerrainType">Terrain type considered "filled" (default: 1)</param>
    public int ComputeCorner16Bitmask(int filledTerrainType = 1)
    {
        var sortedCorners = GetSortedCorners();

        // GetSortedCorners returns vertices oriented by spatial position:
        // Index 0 = SW (most south-west)
        // Index 1 = SE (most south-east)
        // Index 2 = NE (most north-east)
        // Index 3 = NW (most north-west)

        int mask = 0;

        if (sortedCorners.Length > 0 && sortedCorners[0].TerrainType == filledTerrainType)
            mask |= SW;
        if (sortedCorners.Length > 1 && sortedCorners[1].TerrainType == filledTerrainType)
            mask |= SE;
        if (sortedCorners.Length > 2 && sortedCorners[2].TerrainType == filledTerrainType)
            mask |= NE;
        if (sortedCorners.Length > 3 && sortedCorners[3].TerrainType == filledTerrainType)
            mask |= NW;

        return mask;
    }

    /// <summary>
    /// Check if this quad is passable (all corners allow walking).
    /// </summary>
    public bool IsPassable()
    {
        return VertexIds.All(id =>
        {
            var vertex = _mesh.Vertices[id];
            return !vertex.HasStructure && IsTerrainPassable(vertex.TerrainType);
        });
    }

    /// <summary>
    /// Check if this quad is transparent (doesn't block line of sight).
    /// </summary>
    public bool IsTransparent()
    {
        // A quad is transparent if any corner is transparent terrain
        // (conservative for LOS - if you can see through any corner, light gets through)
        return VertexIds.Any(id => IsTerrainTransparent(_mesh.Vertices[id].TerrainType));
    }

    /// <summary>
    /// Test if a point is inside this quad using cross-product winding.
    /// </summary>
    public bool ContainsPoint(Vector2 point)
    {
        var corners = GetCornerPositions();
        return IsPointInConvexPolygon(point, corners);
    }

    /// <summary>
    /// Recalculate centroid and area from current vertex positions.
    /// Call after vertices have been moved (e.g., during relaxation).
    /// </summary>
    public void UpdateCachedProperties()
    {
        var positions = VertexIds.Select(id => _mesh.Vertices[id].Position).ToArray();

        // Centroid is average of corners
        Centroid = new Vector2(
            positions.Average(p => p.X),
            positions.Average(p => p.Y)
        );

        // Area using shoelace formula
        Area = ComputePolygonArea(positions);
    }

    private static float ComputePolygonArea(Vector2[] vertices)
    {
        float area = 0;
        int n = vertices.Length;

        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            area += vertices[i].X * vertices[j].Y;
            area -= vertices[j].X * vertices[i].Y;
        }

        return Mathf.Abs(area) / 2f;
    }

    private static bool IsPointInConvexPolygon(Vector2 point, Vector2[] polygon)
    {
        // Check that point is on the same side of all edges (CCW winding)
        int n = polygon.Length;
        bool? lastSign = null;

        for (int i = 0; i < n; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % n];

            // Cross product of edge vector and point-to-vertex vector
            float cross = (b.X - a.X) * (point.Y - a.Y) - (b.Y - a.Y) * (point.X - a.X);

            bool currentSign = cross >= 0;
            if (lastSign.HasValue && currentSign != lastSign.Value)
                return false;

            lastSign = currentSign;
        }

        return true;
    }

    /// <summary>Checks terrain passability by type.</summary>
    private static bool IsTerrainPassable(int terrainType) => terrainType > 0;

    /// <summary>Checks terrain transparency for line-of-sight.</summary>
    private static bool IsTerrainTransparent(int terrainType) => true;

    /// <summary>
    /// Gets the variant index for rendering this quad.
    /// Uses the VariantIndex from filled corners if set (>= 0),
    /// otherwise returns -1 to indicate position-based hash should be used.
    /// </summary>
    /// <param name="filledTerrainType">Terrain type considered "filled" (default: 1)</param>
    /// <returns>Variant index from first filled corner, or -1 for position-based selection.</returns>
    public int GetVariantIndex(int filledTerrainType = 1)
    {
        // Find the first filled corner with a set variant index
        foreach (var vertexId in VertexIds)
        {
            var vertex = _mesh.Vertices[vertexId];
            if (vertex.TerrainType == filledTerrainType && vertex.VariantIndex >= 0)
            {
                return vertex.VariantIndex;
            }
        }

        // No filled corners with set variant, use position-based hash
        return -1;
    }

    /// <summary>
    /// Gets the dominant tile ID from the quad's corners.
    /// Returns the most common non-null tile ID, preferring passable tiles.
    /// </summary>
    /// <returns>The dominant tile ID, or null if no tiles are assigned.</returns>
    public string? GetDominantTileId()
    {
        var tileCounts = new Dictionary<string, int>();
        var passableTiles = new HashSet<string>();

        foreach (var vertexId in VertexIds)
        {
            var vertex = _mesh.Vertices[vertexId];
            if (vertex.TileId == null) continue;

            tileCounts.TryGetValue(vertex.TileId, out var count);
            tileCounts[vertex.TileId] = count + 1;

            if (vertex.TerrainType > 0)
                passableTiles.Add(vertex.TileId);
        }

        if (tileCounts.Count == 0)
            return null;

        // Prefer passable tiles as the dominant type
        return passableTiles
            .OrderByDescending(t => tileCounts.GetValueOrDefault(t))
            .FirstOrDefault()
            ?? tileCounts.MaxBy(kv => kv.Value).Key;
    }

    public override string ToString() => $"Quad[{Id}] vertices={string.Join(",", VertexIds)}";
}
