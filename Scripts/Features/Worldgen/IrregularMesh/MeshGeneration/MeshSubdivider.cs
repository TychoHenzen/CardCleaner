namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshGeneration;

using System.Collections.Generic;

internal static class MeshSubdivider
{
    public static void SubdivideAllFaces(WorkingMesh mesh)
    {
        var newFaces = new List<WorkingFace>();
        foreach (var face in mesh.Faces)
        {
            if (face.IsQuad)
            {
                newFaces.AddRange(SubdivideQuad(face, mesh));
            }
            else if (face.IsTriangle)
            {
                newFaces.AddRange(SubdivideTriangle(face, mesh));
            }
            else
            {
                newFaces.Add(face);
            }
        }

        mesh.Faces = newFaces;
    }

    private static IEnumerable<WorkingFace> SubdivideQuad(WorkingFace quad, WorkingMesh mesh)
    {
        var vertices = quad.VertexIds;
        int firstMidpoint = mesh.GetMidpointVertex(vertices[0], vertices[1]);
        int secondMidpoint = mesh.GetMidpointVertex(vertices[1], vertices[2]);
        int thirdMidpoint = mesh.GetMidpointVertex(vertices[2], vertices[3]);
        int fourthMidpoint = mesh.GetMidpointVertex(vertices[3], vertices[0]);
        int center = mesh.GetCentroidVertex(vertices);

        yield return new WorkingFace(new[] { vertices[0], firstMidpoint, center, fourthMidpoint });
        yield return new WorkingFace(new[] { firstMidpoint, vertices[1], secondMidpoint, center });
        yield return new WorkingFace(new[] { center, secondMidpoint, vertices[2], thirdMidpoint });
        yield return new WorkingFace(new[] { fourthMidpoint, center, thirdMidpoint, vertices[3] });
    }

    private static IEnumerable<WorkingFace> SubdivideTriangle(WorkingFace triangle, WorkingMesh mesh)
    {
        var vertices = triangle.VertexIds;
        int firstMidpoint = mesh.GetMidpointVertex(vertices[0], vertices[1]);
        int secondMidpoint = mesh.GetMidpointVertex(vertices[1], vertices[2]);
        int thirdMidpoint = mesh.GetMidpointVertex(vertices[2], vertices[0]);
        int center = mesh.GetCentroidVertex(vertices);

        yield return new WorkingFace(new[] { vertices[0], firstMidpoint, center, thirdMidpoint });
        yield return new WorkingFace(new[] { vertices[1], secondMidpoint, center, firstMidpoint });
        yield return new WorkingFace(new[] { vertices[2], thirdMidpoint, center, secondMidpoint });
    }
}
