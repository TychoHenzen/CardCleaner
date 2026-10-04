using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Rendering;

/// <summary>
/// Builds a flat-coloured debug mesh where each quad is tinted by how many corners are filled.
/// </summary>
internal static class SolidColorMeshBuilder
{
    internal static ArrayMesh? Build(IrregularMesh mesh)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        foreach (var quad in mesh.Quads)
        {
            var corners = quad.GetCornerPositions();
            if (corners.Length != 4)
                continue;

            var color = GetBitmaskColor(quad.ComputeCorner16Bitmask());

            // Triangle 1: SW, SE, NE. Triangle 2: SW, NE, NW.
            AddColoredVertex(surfaceTool, corners[0], color);
            AddColoredVertex(surfaceTool, corners[1], color);
            AddColoredVertex(surfaceTool, corners[2], color);
            AddColoredVertex(surfaceTool, corners[0], color);
            AddColoredVertex(surfaceTool, corners[2], color);
            AddColoredVertex(surfaceTool, corners[3], color);
        }

        surfaceTool.GenerateNormals();
        return surfaceTool.Commit();
    }

    private static void AddColoredVertex(SurfaceTool surfaceTool, Vector2 position, Color color)
    {
        surfaceTool.SetColor(color);
        surfaceTool.AddVertex(new Vector3(position.X, position.Y, 0));
    }

    private static Color GetBitmaskColor(int bitmask)
    {
        int filledCount = 0;
        for (int i = 0; i < 4; i++)
        {
            if ((bitmask & (1 << i)) != 0)
                filledCount++;
        }

        return filledCount switch
        {
            0 => new Color(0.91f, 0.86f, 0.77f), // Empty - sand/dirt
            1 => new Color(0.49f, 0.70f, 0.26f), // One corner - light grass
            2 => new Color(0.33f, 0.55f, 0.18f), // Two corners - medium grass
            3 => new Color(0.20f, 0.41f, 0.12f), // Three corners - darker grass
            4 => new Color(0.18f, 0.31f, 0.09f), // Full - dark grass
            _ => Colors.Magenta
        };
    }
}
