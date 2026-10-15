using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Statistics about the mesh for debugging and validation.
/// </summary>
internal struct MeshStatistics
{
    internal int VertexCount;
    internal int QuadCount;
    internal int BoundaryVertexCount;
    internal float MinArea;
    internal float MaxArea;
    internal double AvgArea;
    internal float AreaStdDev;
    internal (Vector2 Min, Vector2 Max) Bounds;

    /// <summary>
    /// Measures a mesh from its vertices, its quad areas (one per quad) and its bounds.
    /// </summary>
    internal static MeshStatistics Of(
        IReadOnlyList<MeshVertex> vertices, IReadOnlyList<float> areas, (Vector2 Min, Vector2 Max) bounds)
    {
        return new MeshStatistics
        {
            VertexCount = vertices.Count,
            QuadCount = areas.Count,
            BoundaryVertexCount = vertices.Count(v => v.IsBoundary),
            MinArea = areas.Count > 0 ? areas.Min() : 0,
            MaxArea = areas.Count > 0 ? areas.Max() : 0,
            AvgArea = areas.Count > 0 ? areas.Average() : 0,
            AreaStdDev = areas.Count > 0 ? ComputeStdDev(areas) : 0,
            Bounds = bounds
        };
    }

    private static float ComputeStdDev(IReadOnlyList<float> values)
    {
        if (values.Count == 0) return 0;
        float avg = (float)values.Average();
        float sumSquares = values.Sum(v => (v - avg) * (v - avg));
        return MathF.Sqrt(sumSquares / values.Count);
    }

    public override readonly string ToString() =>
        $"Vertices: {VertexCount}, Quads: {QuadCount}, Boundary: {BoundaryVertexCount}\n" +
        $"Area: min={MinArea:F4}, max={MaxArea:F4}, avg={AvgArea:F4}, std={AreaStdDev:F4}\n" +
        $"Bounds: {Bounds.Min} to {Bounds.Max}";
}
