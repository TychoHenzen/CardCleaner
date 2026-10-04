namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshGeneration;

using System.Collections.Generic;

internal sealed class WorkingFace
{
    public WorkingFace(int[] vertexIds)
    {
        VertexIds = vertexIds;
    }

    public int[] VertexIds { get; }

    public bool IsTriangle => VertexIds.Length == 3;

    public bool IsQuad => VertexIds.Length == 4;

    public IEnumerable<MeshEdge> GetEdges()
    {
        int vertexCount = VertexIds.Length;
        for (int index = 0; index < vertexCount; index++)
        {
            yield return new MeshEdge(VertexIds[index], VertexIds[(index + 1) % vertexCount]);
        }
    }
}
