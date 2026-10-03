namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshGeneration;

internal readonly record struct MeshEdge(int StartVertexId, int EndVertexId)
{
    public MeshEdge Normalize()
    {
        return StartVertexId < EndVertexId
            ? this
            : new MeshEdge(EndVertexId, StartVertexId);
    }
}
