namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshGeneration;

using Godot;
using System.Collections.Generic;
using System.Linq;

internal static class MeshRelaxer
{
    public static void Relax(WorkingMesh mesh, int iterations, bool pinBoundary)
    {
        var boundaryVertices = pinBoundary
            ? FindBoundaryVertices(mesh)
            : new HashSet<int>();
        if (pinBoundary)
        {
            GD.Print($"Relaxation: pinning {boundaryVertices.Count} boundary vertices");
        }

        var vertexFaces = BuildVertexFaces(mesh);
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            var faceAreas = mesh.Faces.ToDictionary(face => face, face => ComputeFaceArea(face, mesh));
            float targetArea = faceAreas.Values.Where(area => area > 0).DefaultIfEmpty(1f).Average();
            var newPositions = BuildNewPositions(mesh, vertexFaces, boundaryVertices, faceAreas, targetArea);
            ApplyPositions(mesh, newPositions);
            LogProgress(mesh, iteration, iterations);
        }
    }

    private static HashSet<int> FindBoundaryVertices(WorkingMesh mesh)
    {
        var edgeFaceCount = new Dictionary<MeshEdge, int>();
        foreach (var face in mesh.Faces)
        {
            foreach (var edge in face.GetEdges())
            {
                var key = edge.Normalize();
                edgeFaceCount[key] = edgeFaceCount.GetValueOrDefault(key, 0) + 1;
            }
        }

        var boundaryVertices = new HashSet<int>();
        foreach (var entry in edgeFaceCount)
        {
            if (entry.Value == 1)
            {
                boundaryVertices.Add(entry.Key.StartVertexId);
                boundaryVertices.Add(entry.Key.EndVertexId);
            }
        }

        return boundaryVertices;
    }

    private static Dictionary<int, List<WorkingFace>> BuildVertexFaces(WorkingMesh mesh)
    {
        var vertexFaces = new Dictionary<int, List<WorkingFace>>();
        foreach (var face in mesh.Faces)
        {
            foreach (int vertexId in face.VertexIds)
            {
                if (!vertexFaces.TryGetValue(vertexId, out var faces))
                {
                    faces = new List<WorkingFace>();
                    vertexFaces[vertexId] = faces;
                }

                faces.Add(face);
            }
        }

        return vertexFaces;
    }

    private static Dictionary<int, Vector2> BuildNewPositions(
        WorkingMesh mesh,
        Dictionary<int, List<WorkingFace>> vertexFaces,
        HashSet<int> boundaryVertices,
        Dictionary<WorkingFace, float> faceAreas,
        float targetArea)
    {
        var newPositions = new Dictionary<int, Vector2>();
        foreach (var entry in mesh.Vertices)
        {
            if (boundaryVertices.Contains(entry.Key)
                || !vertexFaces.TryGetValue(entry.Key, out var faces)
                || faces.Count == 0)
            {
                continue;
            }

            newPositions[entry.Key] = RelaxVertex(entry.Value, faces, mesh, faceAreas, targetArea);
        }

        return newPositions;
    }

    private static Vector2 RelaxVertex(
        Vector2 position,
        List<WorkingFace> faces,
        WorkingMesh mesh,
        Dictionary<WorkingFace, float> faceAreas,
        float targetArea)
    {
        var lloydTarget = new Vector2(
            faces.Average(face => ComputeFaceCentroid(face, mesh).X),
            faces.Average(face => ComputeFaceCentroid(face, mesh).Y));
        var areaCorrection = ComputeAreaCorrection(position, faces, mesh, faceAreas, targetArea);
        var target = lloydTarget + areaCorrection;
        return position + (target - position) * 0.5f;
    }

    private static Vector2 ComputeAreaCorrection(
        Vector2 position,
        List<WorkingFace> faces,
        WorkingMesh mesh,
        Dictionary<WorkingFace, float> faceAreas,
        float targetArea)
    {
        var correction = Vector2.Zero;
        foreach (var face in faces)
        {
            float area = faceAreas[face];
            if (area < 1e-6f)
            {
                continue;
            }

            float deviation = (area / targetArea) - 1f;
            var toVertex = position - ComputeFaceCentroid(face, mesh);
            float distance = toVertex.Length();
            if (distance > 1e-6f)
            {
                correction -= toVertex / distance * deviation * 0.1f;
            }
        }

        return correction;
    }

    private static void ApplyPositions(WorkingMesh mesh, Dictionary<int, Vector2> newPositions)
    {
        foreach (var entry in newPositions)
        {
            mesh.Vertices[entry.Key] = entry.Value;
        }
    }

    private static void LogProgress(WorkingMesh mesh, int iteration, int iterations)
    {
        if (iteration != 0 && iteration != iterations - 1)
        {
            return;
        }

        var areas = mesh.Faces
            .Where(face => face.IsQuad)
            .Select(face => ComputeFaceArea(face, mesh))
            .ToList();
        if (areas.Count > 0)
        {
            GD.Print(
                $"Relaxation iter {iteration}: area min={areas.Min():F4}, " +
                $"max={areas.Max():F4}, ratio={areas.Max() / areas.Min():F2}x");
        }
    }

    private static float ComputeFaceArea(WorkingFace face, WorkingMesh mesh)
    {
        var positions = face.VertexIds.Select(id => mesh.Vertices[id]).ToList();
        int vertexCount = positions.Count;
        if (vertexCount < 3)
        {
            return 0f;
        }

        float area = 0f;
        for (int index = 0; index < vertexCount; index++)
        {
            int nextIndex = (index + 1) % vertexCount;
            area += positions[index].X * positions[nextIndex].Y;
            area -= positions[nextIndex].X * positions[index].Y;
        }

        return Mathf.Abs(area) / 2f;
    }

    private static Vector2 ComputeFaceCentroid(WorkingFace face, WorkingMesh mesh)
    {
        var positions = face.VertexIds.Select(id => mesh.Vertices[id]).ToList();
        return new Vector2(
            positions.Average(position => position.X),
            positions.Average(position => position.Y));
    }
}
