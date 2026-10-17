namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshGeneration;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

internal sealed record WorkingFace
{
    public WorkingFace(int[] vertexIds)
    {
        VertexIds = Array.AsReadOnly((int[])vertexIds.Clone());
    }

    public IReadOnlyList<int> VertexIds { get; }

    public bool IsTriangle => VertexIds.Count == 3;

    public bool IsQuad => VertexIds.Count == 4;

    public IEnumerable<MeshEdge> GetEdges()
    {
        int vertexCount = VertexIds.Count;
        for (int index = 0; index < vertexCount; index++)
        {
            yield return new MeshEdge(VertexIds[index], VertexIds[(index + 1) % vertexCount]);
        }
    }

    // Faces are keys by identity (ASSUMPTION: no caller wants value equality of two faces).
    public bool Equals(WorkingFace? other) => ReferenceEquals(this, other);

    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}
