namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshGeneration;

using System;
using System.Collections.Generic;
using System.Linq;

internal static class TriangleMerger
{
    public static void Merge(WorkingMesh mesh, float mergeProbability, Random random)
    {
        var edgeToFaces = BuildEdgeToFaces(mesh);
        var merged = new HashSet<WorkingFace>();
        var newFaces = new List<WorkingFace>();
        var edges = edgeToFaces.Keys.ToList();
        Shuffle(edges, random);

        foreach (var edge in edges)
        {
            TryMerge(edge, edgeToFaces, merged, newFaces, mergeProbability, random);
        }

        AddUnmergedFaces(mesh, merged, newFaces);
        mesh.Faces = newFaces;
    }

    private static Dictionary<MeshEdge, List<WorkingFace>> BuildEdgeToFaces(WorkingMesh mesh)
    {
        var edgeToFaces = new Dictionary<MeshEdge, List<WorkingFace>>();
        foreach (var face in mesh.Faces)
        {
            foreach (var edge in face.GetEdges())
            {
                var key = edge.Normalize();
                if (!edgeToFaces.ContainsKey(key))
                {
                    edgeToFaces[key] = new List<WorkingFace>();
                }

                edgeToFaces[key].Add(face);
            }
        }

        return edgeToFaces;
    }

    private static void TryMerge(
        MeshEdge edge,
        Dictionary<MeshEdge, List<WorkingFace>> edgeToFaces,
        HashSet<WorkingFace> merged,
        List<WorkingFace> newFaces,
        float mergeProbability,
        Random random)
    {
        var faces = edgeToFaces[edge];
        if (faces.Count != 2)
        {
            return;
        }

        var firstFace = faces[0];
        var secondFace = faces[1];
        if (merged.Contains(firstFace) || merged.Contains(secondFace)
            || !firstFace.IsTriangle || !secondFace.IsTriangle
            || random.NextDouble() > mergeProbability)
        {
            return;
        }

        var sharedEdge = FindSharedEdge(firstFace, secondFace);
        if (!sharedEdge.HasValue)
        {
            return;
        }

        newFaces.Add(MergeTrianglesToQuad(firstFace, secondFace, sharedEdge.Value));
        merged.Add(firstFace);
        merged.Add(secondFace);
    }

    private static void AddUnmergedFaces(WorkingMesh mesh, HashSet<WorkingFace> merged, List<WorkingFace> newFaces)
    {
        foreach (var face in mesh.Faces)
        {
            if (!merged.Contains(face))
            {
                newFaces.Add(face);
            }
        }
    }

    private static MeshEdge? FindSharedEdge(WorkingFace firstFace, WorkingFace secondFace)
    {
        var firstEdges = new HashSet<MeshEdge>(firstFace.GetEdges().Select(edge => edge.Normalize()));
        foreach (var edge in secondFace.GetEdges())
        {
            if (firstEdges.Contains(edge.Normalize()))
            {
                return edge;
            }
        }

        return null;
    }

    private static WorkingFace MergeTrianglesToQuad(WorkingFace firstFace, WorkingFace secondFace, MeshEdge sharedEdge)
    {
        int firstSharedVertex = sharedEdge.StartVertexId;
        int secondSharedVertex = sharedEdge.EndVertexId;
        int firstOppositeVertex = firstFace.VertexIds.First(
            vertexId => vertexId != firstSharedVertex && vertexId != secondSharedVertex);
        int secondOppositeVertex = secondFace.VertexIds.First(
            vertexId => vertexId != firstSharedVertex && vertexId != secondSharedVertex);

        foreach (var edge in firstFace.GetEdges())
        {
            if (edge.StartVertexId == firstSharedVertex && edge.EndVertexId == secondSharedVertex)
            {
                return new WorkingFace(
                    new[] { firstOppositeVertex, firstSharedVertex, secondOppositeVertex, secondSharedVertex });
            }

            if (edge.StartVertexId == secondSharedVertex && edge.EndVertexId == firstSharedVertex)
            {
                return new WorkingFace(
                    new[] { firstOppositeVertex, secondSharedVertex, secondOppositeVertex, firstSharedVertex });
            }
        }

        return new WorkingFace(
            new[] { firstOppositeVertex, firstSharedVertex, secondOppositeVertex, secondSharedVertex });
    }

    private static void Shuffle<T>(List<T> list, Random random)
    {
        for (int index = list.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            (list[index], list[swapIndex]) = (list[swapIndex], list[index]);
        }
    }
}
