namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshGeneration;

using Godot;
using System.Collections.Generic;
using System.Linq;

internal static class HexGridGenerator
{
    public static WorkingMesh Generate(int rings, float radius)
    {
        var mesh = new WorkingMesh();
        float horizontalSpacing = radius * Mathf.Sqrt(3);
        float verticalSpacing = radius * 1.5f;

        var coordinates = BuildCoordinates(rings);
        var coordinateSet = new HashSet<HexCoordinate>(coordinates);
        var createdTriangles = new HashSet<string>();

        foreach (var coordinate in coordinates)
        {
            AddFirstTriangle(coordinate, coordinateSet, createdTriangles, mesh, horizontalSpacing, verticalSpacing);
            AddSecondTriangle(coordinate, coordinateSet, createdTriangles, mesh, horizontalSpacing, verticalSpacing);
        }

        return mesh;
    }

    private static List<HexCoordinate> BuildCoordinates(int rings)
    {
        var coordinates = new List<HexCoordinate>();
        for (int q = -rings; q <= rings; q++)
        {
            for (int r = -rings; r <= rings; r++)
            {
                if (Mathf.Abs(q + r) <= rings)
                {
                    coordinates.Add(new HexCoordinate(q, r));
                }
            }
        }

        return coordinates;
    }

    private static void AddFirstTriangle(
        HexCoordinate coordinate,
        HashSet<HexCoordinate> coordinateSet,
        HashSet<string> createdTriangles,
        WorkingMesh mesh,
        float horizontalSpacing,
        float verticalSpacing)
    {
        var firstNeighbor = new HexCoordinate(coordinate.Q + 1, coordinate.R);
        var secondNeighbor = new HexCoordinate(coordinate.Q, coordinate.R + 1);
        if (!coordinateSet.Contains(firstNeighbor) || !coordinateSet.Contains(secondNeighbor))
        {
            return;
        }

        var key = GetTriangleKey(coordinate, firstNeighbor, secondNeighbor);
        if (!createdTriangles.Add(key))
        {
            return;
        }

        int firstVertex = mesh.GetOrCreateVertex(ToCartesian(coordinate, horizontalSpacing, verticalSpacing));
        int secondVertex = mesh.GetOrCreateVertex(ToCartesian(firstNeighbor, horizontalSpacing, verticalSpacing));
        int thirdVertex = mesh.GetOrCreateVertex(ToCartesian(secondNeighbor, horizontalSpacing, verticalSpacing));
        mesh.Faces.Add(new WorkingFace(new[] { firstVertex, secondVertex, thirdVertex }));
    }

    private static void AddSecondTriangle(
        HexCoordinate coordinate,
        HashSet<HexCoordinate> coordinateSet,
        HashSet<string> createdTriangles,
        WorkingMesh mesh,
        float horizontalSpacing,
        float verticalSpacing)
    {
        var firstNeighbor = new HexCoordinate(coordinate.Q + 1, coordinate.R - 1);
        var secondNeighbor = new HexCoordinate(coordinate.Q + 1, coordinate.R);
        if (!coordinateSet.Contains(firstNeighbor) || !coordinateSet.Contains(secondNeighbor))
        {
            return;
        }

        var key = GetTriangleKey(coordinate, firstNeighbor, secondNeighbor);
        if (!createdTriangles.Add(key))
        {
            return;
        }

        int firstVertex = mesh.GetOrCreateVertex(ToCartesian(coordinate, horizontalSpacing, verticalSpacing));
        int secondVertex = mesh.GetOrCreateVertex(ToCartesian(firstNeighbor, horizontalSpacing, verticalSpacing));
        int thirdVertex = mesh.GetOrCreateVertex(ToCartesian(secondNeighbor, horizontalSpacing, verticalSpacing));
        mesh.Faces.Add(new WorkingFace(new[] { firstVertex, thirdVertex, secondVertex }));
    }

    private static Vector2 ToCartesian(HexCoordinate coordinate, float horizontalSpacing, float verticalSpacing)
    {
        return new Vector2(
            horizontalSpacing * (coordinate.Q + coordinate.R * 0.5f),
            verticalSpacing * coordinate.R);
    }

    private static string GetTriangleKey(HexCoordinate first, HexCoordinate second, HexCoordinate third)
    {
        var sorted = new[] { first, second, third }
            .OrderBy(coordinate => coordinate.Q)
            .ThenBy(coordinate => coordinate.R)
            .ToArray();
        return $"{sorted[0].Q},{sorted[0].R},{sorted[1].Q},{sorted[1].R},{sorted[2].Q},{sorted[2].R}";
    }
}
