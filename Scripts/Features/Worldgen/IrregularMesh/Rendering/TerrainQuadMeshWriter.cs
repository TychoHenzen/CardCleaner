using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Rendering;

/// <summary>
/// Emits the two textured triangles of a quad into a SurfaceTool.
/// </summary>
internal sealed class TerrainQuadMeshWriter
{
    private readonly Vector2 _atlasSize;
    private readonly int _tileSize;

    internal TerrainQuadMeshWriter(Vector2 atlasSize, int tileSize)
    {
        _atlasSize = atlasSize;
        _tileSize = tileSize;
    }

    /// <summary>
    /// Adds the quad as triangles (SW, SE, NE) and (SW, NE, NW) sampling the given atlas tile.
    /// </summary>
    internal void AddQuad(SurfaceTool surfaceTool, Vector2[] corners, Vector2I atlasCoords)
    {
        var uvCorners = ComputeUvCorners(GetTileUvRect(atlasCoords));

        AddTriangle(surfaceTool, corners, uvCorners, 0, 1, 2);
        AddTriangle(surfaceTool, corners, uvCorners, 0, 2, 3);
    }

    private Rect2 GetTileUvRect(Vector2I atlasCoords)
    {
        float u = (atlasCoords.X * _tileSize) / _atlasSize.X;
        float v = (atlasCoords.Y * _tileSize) / _atlasSize.Y;
        float uSize = _tileSize / _atlasSize.X;
        float vSize = _tileSize / _atlasSize.Y;

        return new Rect2(u, v, uSize, vSize);
    }

    /// <summary>
    /// UV corners in order SW(0), SE(1), NE(2), NW(3). The rect position is top-left (SW in the Y-up world).
    /// </summary>
    private static Vector2[] ComputeUvCorners(Rect2 uvRect)
    {
        return
        [
            new Vector2(uvRect.Position.X, uvRect.Position.Y + uvRect.Size.Y),
            new Vector2(uvRect.Position.X + uvRect.Size.X, uvRect.Position.Y + uvRect.Size.Y),
            new Vector2(uvRect.Position.X + uvRect.Size.X, uvRect.Position.Y),
            new Vector2(uvRect.Position.X, uvRect.Position.Y)
        ];
    }

    private static void AddTriangle(
        SurfaceTool surfaceTool, Vector2[] corners, Vector2[] uvCorners, int i0, int i1, int i2)
    {
        AddVertex(surfaceTool, corners[i0], uvCorners[i0]);
        AddVertex(surfaceTool, corners[i1], uvCorners[i1]);
        AddVertex(surfaceTool, corners[i2], uvCorners[i2]);
    }

    private static void AddVertex(SurfaceTool surfaceTool, Vector2 position, Vector2 uv)
    {
        surfaceTool.SetUV(uv);
        surfaceTool.AddVertex(new Vector3(position.X, position.Y, 0));
    }
}
