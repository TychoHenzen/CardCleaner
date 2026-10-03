namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshGeneration;

using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using Godot;
using System.Collections.Generic;
using System.Linq;

internal static class MeshConverter
{
    public static IrregularMesh Convert(WorkingMesh workingMesh)
    {
        var mesh = new IrregularMesh();
        var vertexIdMap = AddVertices(workingMesh, mesh);
        AddQuadFaces(workingMesh, mesh, vertexIdMap);
        mesh.BuildAdjacency();
        return mesh;
    }

    private static Dictionary<int, int> AddVertices(WorkingMesh workingMesh, IrregularMesh mesh)
    {
        var vertexIdMap = new Dictionary<int, int>();
        foreach (var entry in workingMesh.Vertices.OrderBy(entry => entry.Key))
        {
            vertexIdMap[entry.Key] = mesh.AddVertex(entry.Value);
        }

        return vertexIdMap;
    }

    private static void AddQuadFaces(
        WorkingMesh workingMesh,
        IrregularMesh mesh,
        Dictionary<int, int> vertexIdMap)
    {
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
    }
}
