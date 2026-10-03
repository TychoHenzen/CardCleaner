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

    public override readonly string ToString() =>
        $"Vertices: {VertexCount}, Quads: {QuadCount}, Boundary: {BoundaryVertexCount}\n" +
        $"Area: min={MinArea:F4}, max={MaxArea:F4}, avg={AvgArea:F4}, std={AreaStdDev:F4}\n" +
        $"Bounds: {Bounds.Min} to {Bounds.Max}";
}
