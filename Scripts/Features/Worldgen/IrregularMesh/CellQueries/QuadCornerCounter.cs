namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.CellQueries;

/// <summary>
/// Counts how many corners of a quad satisfy a terrain condition.
/// </summary>
internal static class QuadCornerCounter
{
    /// <summary>
    /// Corners with terrain type above zero (type 0 is impassable, any higher type is passable).
    /// </summary>
    internal static int CountFilledCorners(IrregularMesh mesh, MeshQuad quad)
    {
        int count = 0;
        foreach (var vid in quad.VertexIds)
        {
            if (mesh.Vertices[vid].TerrainType > 0)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Filled corners that do not carry a blocking structure.
    /// </summary>
    internal static int CountPassableCorners(IrregularMesh mesh, MeshQuad quad)
    {
        int count = 0;
        foreach (var vid in quad.VertexIds)
        {
            var vertex = mesh.Vertices[vid];
            if (!vertex.HasStructure && vertex.TerrainType > 0)
                count++;
        }

        return count;
    }
}
