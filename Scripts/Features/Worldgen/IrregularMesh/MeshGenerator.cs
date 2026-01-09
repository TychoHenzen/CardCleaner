namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Generates an irregular quad mesh from a hexagonal base grid.
///
/// Algorithm:
/// 1. Generate hexagonal grid as connected triangle mesh
/// 2. Randomly merge adjacent triangles into quads
/// 3. Subdivide: quads → 4 quads, triangles → 3 quads
/// 4. Run Lloyd relaxation to equalize quad sizes
/// </summary>
public static class MeshGenerator
{
    /// <summary>
    /// Configuration for mesh generation.
    /// </summary>
    public class GenerationConfig
    {
        /// <summary>Number of hexagonal rings from center (determines mesh size).</summary>
        public int Rings { get; set; } = 10;

        /// <summary>Radius of each hexagon in world units.</summary>
        public float HexRadius { get; set; } = 1.0f;

        /// <summary>Probability of merging two adjacent triangles into a quad.</summary>
        public float MergeProbability { get; set; } = 0.7f;

        /// <summary>Number of Lloyd relaxation iterations.</summary>
        public int RelaxationIterations { get; set; } = 15;

        /// <summary>Whether to pin boundary vertices during relaxation.</summary>
        public bool PinBoundary { get; set; } = true;

        /// <summary>Random seed for reproducible generation (null = random).</summary>
        public int? Seed { get; set; }
    }

    /// <summary>
    /// Generate an irregular quad mesh with default configuration.
    /// </summary>
    public static IrregularMesh Generate(int rings = 10, int? seed = null)
    {
        return Generate(new GenerationConfig { Rings = rings, Seed = seed });
    }

    /// <summary>
    /// Generate an irregular quad mesh with custom configuration.
    /// </summary>
    public static IrregularMesh Generate(GenerationConfig config)
    {
        var random = config.Seed.HasValue
            ? new Random(config.Seed.Value)
            : new Random();

        // Step 1: Generate hex grid as triangles
        var workingMesh = GenerateHexTriangleMesh(config.Rings, config.HexRadius);
        GD.Print($"Step 1: Generated {workingMesh.Vertices.Count} vertices, {workingMesh.Faces.Count} triangles");

        // Step 2: Merge triangles into quads
        MergeAdjacentTriangles(workingMesh, config.MergeProbability, random);
        int numTris = workingMesh.Faces.Count(f => f.IsTriangle);
        int numQuads = workingMesh.Faces.Count(f => f.IsQuad);
        GD.Print($"Step 2: After merging - {numQuads} quads, {numTris} remaining triangles");

        // Step 3: Subdivide all faces
        SubdivideAllFaces(workingMesh);
        numQuads = workingMesh.Faces.Count(f => f.IsQuad);
        GD.Print($"Step 3: After subdivision - {numQuads} quads, {workingMesh.Vertices.Count} vertices");

        // Step 4: Lloyd relaxation
        if (config.RelaxationIterations > 0)
        {
            LloydRelaxation(workingMesh, config.RelaxationIterations, config.PinBoundary);
        }

        // Convert to final IrregularMesh format
        var finalMesh = ConvertToIrregularMesh(workingMesh);
        GD.Print($"Final mesh: {finalMesh.GetStatistics()}");

        return finalMesh;
    }

    #region Step 1: Hex Grid Generation

    private static WorkingMesh GenerateHexTriangleMesh(int rings, float radius)
    {
        var mesh = new WorkingMesh();

        // Spacing for pointy-top hexagons
        float horiz = radius * Mathf.Sqrt(3);
        float vert = radius * 1.5f;

        Vector2 AxialToCartesian(int q, int r) => new(horiz * (q + r * 0.5f), vert * r);

        // Generate all hex center coordinates within ring bounds
        var coords = new List<(int q, int r)>();
        for (int q = -rings; q <= rings; q++)
        {
            for (int r = -rings; r <= rings; r++)
            {
                if (Mathf.Abs(q + r) <= rings)
                {
                    coords.Add((q, r));
                }
            }
        }
        var coordSet = new HashSet<(int, int)>(coords);

        // Create triangles connecting adjacent hex centers
        // Two triangle types per hex position (upper-right region)
        var createdTriangles = new HashSet<string>();

        foreach (var (q, r) in coords)
        {
            // Triangle type 1: (q,r), (q+1,r), (q,r+1)
            if (coordSet.Contains((q + 1, r)) && coordSet.Contains((q, r + 1)))
            {
                var triKey = GetTriangleKey((q, r), (q + 1, r), (q, r + 1));
                if (createdTriangles.Add(triKey))
                {
                    var v0 = mesh.GetOrCreateVertex(AxialToCartesian(q, r));
                    var v1 = mesh.GetOrCreateVertex(AxialToCartesian(q + 1, r));
                    var v2 = mesh.GetOrCreateVertex(AxialToCartesian(q, r + 1));
                    mesh.Faces.Add(new WorkingFace(new[] { v0, v1, v2 }));
                }
            }

            // Triangle type 2: (q,r), (q+1,r-1), (q+1,r)
            if (coordSet.Contains((q + 1, r - 1)) && coordSet.Contains((q + 1, r)))
            {
                var triKey = GetTriangleKey((q, r), (q + 1, r - 1), (q + 1, r));
                if (createdTriangles.Add(triKey))
                {
                    var v0 = mesh.GetOrCreateVertex(AxialToCartesian(q, r));
                    var v1 = mesh.GetOrCreateVertex(AxialToCartesian(q + 1, r - 1));
                    var v2 = mesh.GetOrCreateVertex(AxialToCartesian(q + 1, r));
                    mesh.Faces.Add(new WorkingFace(new[] { v0, v2, v1 })); // CCW order
                }
            }
        }

        return mesh;
    }

    private static string GetTriangleKey((int, int) a, (int, int) b, (int, int) c)
    {
        var sorted = new[] { a, b, c }.OrderBy(x => x.Item1).ThenBy(x => x.Item2).ToArray();
        return $"{sorted[0]},{sorted[1]},{sorted[2]}";
    }

    #endregion

    #region Step 2: Triangle Merging

    private static void MergeAdjacentTriangles(WorkingMesh mesh, float mergeProbability, Random random)
    {
        // Build edge-to-faces map
        var edgeToFaces = new Dictionary<(int, int), List<WorkingFace>>();

        foreach (var face in mesh.Faces)
        {
            foreach (var edge in face.GetEdges())
            {
                var key = NormalizeEdge(edge);
                if (!edgeToFaces.ContainsKey(key))
                    edgeToFaces[key] = new List<WorkingFace>();
                edgeToFaces[key].Add(face);
            }
        }

        var merged = new HashSet<WorkingFace>();
        var newFaces = new List<WorkingFace>();

        // Shuffle edges for variety
        var edges = edgeToFaces.Keys.ToList();
        Shuffle(edges, random);

        foreach (var edgeKey in edges)
        {
            var faces = edgeToFaces[edgeKey];
            if (faces.Count != 2)
                continue;

            var f1 = faces[0];
            var f2 = faces[1];

            if (merged.Contains(f1) || merged.Contains(f2))
                continue;

            if (!f1.IsTriangle || !f2.IsTriangle)
                continue;

            if (random.NextDouble() > mergeProbability)
                continue;

            // Find the actual shared edge
            var sharedEdge = FindSharedEdge(f1, f2);
            if (sharedEdge == null)
                continue;

            // Merge into quad
            var quad = MergeTrianglesToQuad(f1, f2, sharedEdge.Value);
            newFaces.Add(quad);
            merged.Add(f1);
            merged.Add(f2);
        }

        // Keep unmerged faces
        foreach (var face in mesh.Faces)
        {
            if (!merged.Contains(face))
                newFaces.Add(face);
        }

        mesh.Faces = newFaces;
    }

    private static (int, int) NormalizeEdge((int, int) edge) =>
        edge.Item1 < edge.Item2 ? edge : (edge.Item2, edge.Item1);

    private static (int, int)? FindSharedEdge(WorkingFace f1, WorkingFace f2)
    {
        var edges1 = new HashSet<(int, int)>(f1.GetEdges().Select(NormalizeEdge));

        foreach (var edge in f2.GetEdges())
        {
            var normalized = NormalizeEdge(edge);
            if (edges1.Contains(normalized))
                return edge;
        }

        return null;
    }

    private static WorkingFace MergeTrianglesToQuad(WorkingFace tri1, WorkingFace tri2, (int, int) sharedEdge)
    {
        int v1 = sharedEdge.Item1;
        int v2 = sharedEdge.Item2;

        // Find opposite vertices (not on shared edge)
        int opp1 = tri1.VertexIds.First(v => v != v1 && v != v2);
        int opp2 = tri2.VertexIds.First(v => v != v1 && v != v2);

        // Order as quad (CCW): opp1 -> v1 -> opp2 -> v2
        // Check which direction gives correct winding
        var edges1 = tri1.GetEdges();
        foreach (var (a, b) in edges1)
        {
            if ((a == v1 && b == v2) || (a == v2 && b == v1))
            {
                if (a == v1)
                    return new WorkingFace(new[] { opp1, v1, opp2, v2 });
                else
                    return new WorkingFace(new[] { opp1, v2, opp2, v1 });
            }
        }

        // Fallback
        return new WorkingFace(new[] { opp1, v1, opp2, v2 });
    }

    #endregion

    #region Step 3: Subdivision

    private static void SubdivideAllFaces(WorkingMesh mesh)
    {
        var newFaces = new List<WorkingFace>();

        foreach (var face in mesh.Faces)
        {
            if (face.IsQuad)
                newFaces.AddRange(SubdivideQuad(face, mesh));
            else if (face.IsTriangle)
                newFaces.AddRange(SubdivideTriangle(face, mesh));
            else
                newFaces.Add(face); // Keep as-is
        }

        mesh.Faces = newFaces;
    }

    private static IEnumerable<WorkingFace> SubdivideQuad(WorkingFace quad, WorkingMesh mesh)
    {
        var v = quad.VertexIds;

        // Get midpoints of each edge (shared with adjacent faces via cache)
        int m01 = mesh.GetMidpointVertex(v[0], v[1]);
        int m12 = mesh.GetMidpointVertex(v[1], v[2]);
        int m23 = mesh.GetMidpointVertex(v[2], v[3]);
        int m30 = mesh.GetMidpointVertex(v[3], v[0]);

        // Center point (unique to this quad)
        int center = mesh.GetCentroidVertex(v);

        // Create 4 sub-quads
        yield return new WorkingFace(new[] { v[0], m01, center, m30 });
        yield return new WorkingFace(new[] { m01, v[1], m12, center });
        yield return new WorkingFace(new[] { center, m12, v[2], m23 });
        yield return new WorkingFace(new[] { m30, center, m23, v[3] });
    }

    private static IEnumerable<WorkingFace> SubdivideTriangle(WorkingFace tri, WorkingMesh mesh)
    {
        var v = tri.VertexIds;

        // Edge midpoints (shared with adjacent faces)
        int m01 = mesh.GetMidpointVertex(v[0], v[1]);
        int m12 = mesh.GetMidpointVertex(v[1], v[2]);
        int m20 = mesh.GetMidpointVertex(v[2], v[0]);

        // Centroid (unique to this triangle)
        int center = mesh.GetCentroidVertex(v);

        // Create 3 quads (one per original vertex)
        yield return new WorkingFace(new[] { v[0], m01, center, m20 });
        yield return new WorkingFace(new[] { v[1], m12, center, m01 });
        yield return new WorkingFace(new[] { v[2], m20, center, m12 });
    }

    #endregion

    #region Step 4: Lloyd Relaxation

    private static void LloydRelaxation(WorkingMesh mesh, int iterations, bool pinBoundary)
    {
        // Identify boundary vertices
        var boundaryVertices = new HashSet<int>();
        if (pinBoundary)
        {
            var edgeFaceCount = new Dictionary<(int, int), int>();

            foreach (var face in mesh.Faces)
            {
                foreach (var edge in face.GetEdges())
                {
                    var key = NormalizeEdge(edge);
                    edgeFaceCount[key] = edgeFaceCount.GetValueOrDefault(key, 0) + 1;
                }
            }

            foreach (var (edge, count) in edgeFaceCount)
            {
                if (count == 1)
                {
                    boundaryVertices.Add(edge.Item1);
                    boundaryVertices.Add(edge.Item2);
                }
            }

            GD.Print($"Relaxation: pinning {boundaryVertices.Count} boundary vertices");
        }

        // Build vertex-to-faces adjacency
        var vertexFaces = new Dictionary<int, List<WorkingFace>>();
        foreach (var face in mesh.Faces)
        {
            foreach (int vid in face.VertexIds)
            {
                if (!vertexFaces.ContainsKey(vid))
                    vertexFaces[vid] = new List<WorkingFace>();
                vertexFaces[vid].Add(face);
            }
        }

        for (int iter = 0; iter < iterations; iter++)
        {
            // Compute face areas and target area
            var faceAreas = mesh.Faces.ToDictionary(f => f, f => ComputeFaceArea(f, mesh));
            float targetArea = faceAreas.Values.Where(a => a > 0).DefaultIfEmpty(1f).Average();

            var newPositions = new Dictionary<int, Vector2>();

            foreach (var (vid, position) in mesh.Vertices)
            {
                if (boundaryVertices.Contains(vid))
                    continue;

                var faces = vertexFaces.GetValueOrDefault(vid, new List<WorkingFace>());
                if (faces.Count == 0)
                    continue;

                // Lloyd: move toward centroid of adjacent face centroids
                var lloydTarget = new Vector2(
                    faces.Average(f => ComputeFaceCentroid(f, mesh).X),
                    faces.Average(f => ComputeFaceCentroid(f, mesh).Y)
                );

                // Hybrid: add area correction
                var areaCorrection = Vector2.Zero;
                foreach (var f in faces)
                {
                    float area = faceAreas[f];
                    if (area < 1e-6f)
                        continue;

                    float deviation = (area / targetArea) - 1f;
                    var faceCentroid = ComputeFaceCentroid(f, mesh);
                    var toVertex = position - faceCentroid;
                    float dist = toVertex.Length();

                    if (dist > 1e-6f)
                    {
                        var direction = toVertex / dist;
                        areaCorrection -= direction * deviation * 0.1f;
                    }
                }

                var target = lloydTarget + areaCorrection;
                newPositions[vid] = position + (target - position) * 0.5f;
            }

            // Apply new positions
            foreach (var (vid, newPos) in newPositions)
            {
                mesh.Vertices[vid] = newPos;
            }

            // Log progress at start and end
            if (iter == 0 || iter == iterations - 1)
            {
                var areas = mesh.Faces.Where(f => f.IsQuad).Select(f => ComputeFaceArea(f, mesh)).ToList();
                if (areas.Count > 0)
                {
                    GD.Print($"Relaxation iter {iter}: area min={areas.Min():F4}, max={areas.Max():F4}, ratio={areas.Max() / areas.Min():F2}x");
                }
            }
        }
    }

    private static float ComputeFaceArea(WorkingFace face, WorkingMesh mesh)
    {
        var positions = face.VertexIds.Select(id => mesh.Vertices[id]).ToList();
        int n = positions.Count;
        if (n < 3)
            return 0f;

        float area = 0f;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            area += positions[i].X * positions[j].Y;
            area -= positions[j].X * positions[i].Y;
        }

        return Mathf.Abs(area) / 2f;
    }

    private static Vector2 ComputeFaceCentroid(WorkingFace face, WorkingMesh mesh)
    {
        var positions = face.VertexIds.Select(id => mesh.Vertices[id]).ToList();
        return new Vector2(
            positions.Average(p => p.X),
            positions.Average(p => p.Y)
        );
    }

    #endregion

    #region Conversion to Final Mesh

    private static IrregularMesh ConvertToIrregularMesh(WorkingMesh workingMesh)
    {
        var mesh = new IrregularMesh();

        // Add all vertices
        var vertexIdMap = new Dictionary<int, int>();
        foreach (var (oldId, position) in workingMesh.Vertices.OrderBy(kv => kv.Key))
        {
            int newId = mesh.AddVertex(position);
            vertexIdMap[oldId] = newId;
        }

        // Add only quad faces
        foreach (var face in workingMesh.Faces)
        {
            if (!face.IsQuad)
            {
                GD.PrintErr($"Non-quad face found in final mesh: {face.VertexIds.Length} vertices");
                continue;
            }

            var newVertexIds = face.VertexIds.Select(id => vertexIdMap[id]).ToArray();
            mesh.AddQuad(newVertexIds);
        }

        // Build adjacency
        mesh.BuildAdjacency();

        return mesh;
    }

    #endregion

    #region Utilities

    private static void Shuffle<T>(List<T> list, Random random)
    {
        int n = list.Count;
        for (int i = n - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    #endregion

    #region Working Mesh Classes (internal use during generation)

    /// <summary>
    /// Temporary mesh representation used during generation.
    /// Supports triangles and quads, vertex caching for shared edges.
    /// </summary>
    private class WorkingMesh
    {
        public Dictionary<int, Vector2> Vertices { get; } = new();
        public List<WorkingFace> Faces { get; set; } = new();

        private int _nextVertexId;
        private readonly Dictionary<(float, float), int> _positionCache = new();
        private readonly Dictionary<(int, int), int> _midpointCache = new();

        private const int PositionPrecision = 6;

        public int GetOrCreateVertex(Vector2 position)
        {
            var key = (
                Mathf.Round(position.X * Mathf.Pow(10, PositionPrecision)) / Mathf.Pow(10, PositionPrecision),
                Mathf.Round(position.Y * Mathf.Pow(10, PositionPrecision)) / Mathf.Pow(10, PositionPrecision)
            );

            if (_positionCache.TryGetValue(key, out int existingId))
                return existingId;

            int id = _nextVertexId++;
            Vertices[id] = position;
            _positionCache[key] = id;
            return id;
        }

        public int GetMidpointVertex(int v1, int v2)
        {
            var key = v1 < v2 ? (v1, v2) : (v2, v1);

            if (_midpointCache.TryGetValue(key, out int existingId))
                return existingId;

            var midpoint = (Vertices[v1] + Vertices[v2]) / 2f;
            int id = GetOrCreateVertex(midpoint);
            _midpointCache[key] = id;
            return id;
        }

        public int GetCentroidVertex(int[] vertexIds)
        {
            var centroid = new Vector2(
                vertexIds.Average(id => Vertices[id].X),
                vertexIds.Average(id => Vertices[id].Y)
            );
            return GetOrCreateVertex(centroid);
        }
    }

    /// <summary>
    /// Temporary face representation supporting both triangles and quads.
    /// </summary>
    private class WorkingFace
    {
        public int[] VertexIds { get; }

        public bool IsTriangle => VertexIds.Length == 3;
        public bool IsQuad => VertexIds.Length == 4;

        public WorkingFace(int[] vertexIds)
        {
            VertexIds = vertexIds;
        }

        public IEnumerable<(int, int)> GetEdges()
        {
            int n = VertexIds.Length;
            for (int i = 0; i < n; i++)
            {
                yield return (VertexIds[i], VertexIds[(i + 1) % n]);
            }
        }
    }

    #endregion
}
