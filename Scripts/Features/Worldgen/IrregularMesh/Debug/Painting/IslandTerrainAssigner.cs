using System;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Debug.Painting;

/// <summary>
/// Fills random circular islands of passable terrain on a mesh for the editor preview.
/// </summary>
internal static class IslandTerrainAssigner
{
    /// <summary>
    /// Resets all vertices to terrain 0, then fills the islands. Returns the number of filled vertices.
    /// </summary>
    internal static int Assign(IrregularMesh mesh, int seed, int islandCount)
    {
        foreach (var vertex in mesh.Vertices)
        {
            vertex.TerrainType = 0;
        }

        var random = new Random(seed + 1000);
        var bounds = mesh.Bounds;

        for (int i = 0; i < islandCount; i++)
        {
            var center = new Vector2(
                (float)(bounds.Min.X + random.NextDouble() * (bounds.Max.X - bounds.Min.X)),
                (float)(bounds.Min.Y + random.NextDouble() * (bounds.Max.Y - bounds.Min.Y))
            );
            var radius = (float)(1.0 + random.NextDouble() * 2.5);

            FillIsland(mesh, center, radius);
        }

        return CountFilled(mesh);
    }

    private static void FillIsland(IrregularMesh mesh, Vector2 center, float radius)
    {
        foreach (var vertex in mesh.Vertices)
        {
            if (vertex.Position.DistanceTo(center) < radius)
            {
                vertex.TerrainType = 1;
            }
        }
    }

    private static int CountFilled(IrregularMesh mesh)
    {
        int filledCount = 0;
        foreach (var vertex in mesh.Vertices)
        {
            if (vertex.TerrainType == 1) filledCount++;
        }

        return filledCount;
    }
}
