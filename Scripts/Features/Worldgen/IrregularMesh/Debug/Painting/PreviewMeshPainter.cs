using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Debug.Painting;

/// <summary>
/// Draws the editor preview overlays (wireframe and vertex markers) with a mesh-to-screen transform.
/// </summary>
internal sealed class PreviewMeshPainter
{
    private readonly CanvasItem _canvas;
    private readonly IrregularMesh _mesh;
    private readonly Transform2D _transform;

    internal PreviewMeshPainter(CanvasItem canvas, IrregularMesh mesh, Transform2D transform)
    {
        _canvas = canvas;
        _mesh = mesh;
        _transform = transform;
    }

    internal void DrawWireframe()
    {
        foreach (var quad in _mesh.Quads)
        {
            var corners = quad.GetCornerPositions();
            if (corners.Length != 4) continue;

            for (int i = 0; i < 4; i++)
            {
                var p1 = _transform * corners[i];
                var p2 = _transform * corners[(i + 1) % 4];
                _canvas.DrawLine(p1, p2, new Color(0.2f, 0.2f, 0.2f, 0.5f), 1f);
            }
        }
    }

    internal void DrawVertices()
    {
        foreach (var vertex in _mesh.Vertices)
        {
            var pos = _transform * vertex.Position;
            var color = vertex.TerrainType == 1
                ? new Color(0.0f, 0.5f, 0.0f)
                : new Color(0.6f, 0.5f, 0.4f);
            var size = vertex.IsBoundary ? 4f : 3f;
            _canvas.DrawCircle(pos, size, color);

            if (vertex.IsBoundary)
            {
                _canvas.DrawCircle(pos, size + 1, Colors.White with { A = 0.5f });
            }
        }
    }
}
