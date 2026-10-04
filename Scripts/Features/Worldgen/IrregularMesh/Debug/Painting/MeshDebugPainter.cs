using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Debug.Painting;

/// <summary>
/// Draws debug overlays for an irregular mesh onto a canvas item, scaling mesh coordinates by the world scale.
/// </summary>
internal sealed class MeshDebugPainter
{
    private readonly CanvasItem _canvas;
    private readonly IrregularMesh _mesh;
    private readonly float _worldScale;

    internal MeshDebugPainter(CanvasItem canvas, IrregularMesh mesh, float worldScale)
    {
        _canvas = canvas;
        _mesh = mesh;
        _worldScale = worldScale;
    }

    internal void DrawWireframe(Color color)
    {
        foreach (var quad in _mesh.Quads)
        {
            var corners = quad.GetCornerPositions();
            if (corners.Length != 4) continue;

            for (int i = 0; i < 4; i++)
            {
                var from = corners[i] * _worldScale;
                var to = corners[(i + 1) % 4] * _worldScale;
                _canvas.DrawLine(from, to, color, 1f);
            }
        }
    }

    /// <summary>
    /// Draws a circle per vertex, coloured by terrain type when requested.
    /// </summary>
    internal void DrawVertexMarkers(bool colorByTerrain, Color markerColor)
    {
        foreach (var vertex in _mesh.Vertices)
        {
            var pos = vertex.Position * _worldScale;
            var color = colorByTerrain ? GetTerrainColor(vertex.TerrainType) : markerColor;

            _canvas.DrawCircle(pos, 3f, color);
        }
    }

    internal void DrawQuadIds()
    {
        var font = ThemeDB.FallbackFont;
        if (font == null) return;

        foreach (var quad in _mesh.Quads)
        {
            var center = quad.Centroid * _worldScale;
            _canvas.DrawString(font, center, quad.Id.ToString(), HorizontalAlignment.Center,
                -1, 10, new Color(1, 1, 1, 0.7f));
        }
    }

    internal void DrawPath(IReadOnlyList<int> path, Color color)
    {
        if (path.Count < 2) return;

        for (int i = 0; i < path.Count - 1; i++)
        {
            var fromCell = path[i];
            var toCell = path[i + 1];

            if (fromCell >= _mesh.Quads.Count || toCell >= _mesh.Quads.Count) continue;

            var from = _mesh.Quads[fromCell].Centroid * _worldScale;
            var to = _mesh.Quads[toCell].Centroid * _worldScale;

            _canvas.DrawLine(from, to, color, 3f);
            _canvas.DrawCircle(from, 5f, color);
        }

        // Draw final point
        var lastCell = path[^1];
        if (lastCell < _mesh.Quads.Count)
        {
            var pos = _mesh.Quads[lastCell].Centroid * _worldScale;
            _canvas.DrawCircle(pos, 7f, color);
        }
    }

    internal void DrawHighlightedCells(IEnumerable<int> cells, Color color)
    {
        foreach (var cellId in cells)
        {
            if (cellId >= _mesh.Quads.Count) continue;

            var scaledCorners = GetScaledCorners(cellId);
            if (scaledCorners == null) continue;

            _canvas.DrawColoredPolygon(scaledCorners, color);
        }
    }

    internal void DrawSelectedCell(int cellId, Color fillColor)
    {
        if (cellId >= _mesh.Quads.Count) return;

        var scaledCorners = GetScaledCorners(cellId);
        if (scaledCorners == null) return;

        _canvas.DrawColoredPolygon(scaledCorners, fillColor);

        // Draw outline
        for (int i = 0; i < 4; i++)
        {
            _canvas.DrawLine(scaledCorners[i], scaledCorners[(i + 1) % 4], new Color(0, 0.8f, 1, 1f), 2f);
        }
    }

    internal void DrawAdjacency(Color color)
    {
        var drawnEdges = new HashSet<(int, int)>();

        foreach (var quad in _mesh.Quads)
        {
            var fromCenter = quad.Centroid * _worldScale;

            foreach (var adjId in quad.AdjacentQuadIds)
            {
                // Avoid drawing the same edge twice
                var edge = (Mathf.Min(quad.Id, adjId), Mathf.Max(quad.Id, adjId));
                if (!drawnEdges.Add(edge)) continue;

                if (adjId >= _mesh.Quads.Count) continue;
                var toCenter = _mesh.Quads[adjId].Centroid * _worldScale;

                _canvas.DrawLine(fromCenter, toCenter, color, 1f);
            }
        }
    }

    /// <summary>
    /// Returns the four scaled corner positions of the quad, or null if the quad is not a full quad.
    /// </summary>
    private Vector2[]? GetScaledCorners(int cellId)
    {
        var corners = _mesh.Quads[cellId].GetCornerPositions();
        if (corners.Length != 4) return null;

        var scaledCorners = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            scaledCorners[i] = corners[i] * _worldScale;
        }

        return scaledCorners;
    }

    private static Color GetTerrainColor(int terrainType)
    {
        return terrainType switch
        {
            0 => new Color(0.2f, 0.4f, 0.8f, 0.8f), // Water - blue
            1 => new Color(0.3f, 0.7f, 0.3f, 0.8f), // Grass - green
            2 => new Color(0.8f, 0.7f, 0.4f, 0.8f), // Sand - tan
            3 => new Color(0.5f, 0.5f, 0.5f, 0.8f), // Rock - gray
            4 => new Color(0.9f, 0.9f, 0.9f, 0.8f), // Snow - white
            5 => new Color(0.6f, 0.3f, 0.1f, 0.8f), // Dirt - brown
            _ => new Color(1f, 0f, 1f, 0.8f)        // Unknown - magenta
        };
    }
}
