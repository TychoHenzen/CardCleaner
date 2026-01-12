using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Debug;

/// <summary>
/// Debug visualization for irregular mesh terrain.
/// Renders wireframes, vertex markers, paths, and visibility information.
/// </summary>
public partial class IrregularMeshDebugRenderer : Node2D
{
    private IrregularMesh? _mesh;
    private IrregularMeshMapData? _mapData;
    private float _worldScale = 1f;

    private bool _showWireframe = true;
    private bool _showVertexMarkers;
    private bool _showQuadIds;
    private bool _showTerrainTypes;
    private bool _showAdjacency;

    private List<int>? _highlightedPath;
    private HashSet<int>? _highlightedCells;
    private int? _selectedCell;

    #region Export Properties

    /// <summary>
    /// Whether to show mesh wireframe.
    /// </summary>
    [Export]
    public bool ShowWireframe
    {
        get => _showWireframe;
        set
        {
            _showWireframe = value;
            QueueRedraw();
        }
    }

    /// <summary>
    /// Whether to show vertex position markers.
    /// </summary>
    [Export]
    public bool ShowVertexMarkers
    {
        get => _showVertexMarkers;
        set
        {
            _showVertexMarkers = value;
            QueueRedraw();
        }
    }

    /// <summary>
    /// Whether to show quad ID labels.
    /// </summary>
    [Export]
    public bool ShowQuadIds
    {
        get => _showQuadIds;
        set
        {
            _showQuadIds = value;
            QueueRedraw();
        }
    }

    /// <summary>
    /// Whether to show terrain type colors on vertices.
    /// </summary>
    [Export]
    public bool ShowTerrainTypes
    {
        get => _showTerrainTypes;
        set
        {
            _showTerrainTypes = value;
            QueueRedraw();
        }
    }

    /// <summary>
    /// Whether to show adjacency connections.
    /// </summary>
    [Export]
    public bool ShowAdjacency
    {
        get => _showAdjacency;
        set
        {
            _showAdjacency = value;
            QueueRedraw();
        }
    }

    /// <summary>
    /// Color for wireframe lines.
    /// </summary>
    [Export]
    public Color WireframeColor { get; set; } = new Color(1, 1, 1, 0.3f);

    /// <summary>
    /// Color for vertex markers.
    /// </summary>
    [Export]
    public Color VertexColor { get; set; } = new Color(1, 0.5f, 0, 0.8f);

    /// <summary>
    /// Color for highlighted path.
    /// </summary>
    [Export]
    public Color PathColor { get; set; } = new Color(0, 1, 0, 0.8f);

    /// <summary>
    /// Color for highlighted cells.
    /// </summary>
    [Export]
    public Color HighlightColor { get; set; } = new Color(1, 1, 0, 0.4f);

    /// <summary>
    /// Color for selected cell.
    /// </summary>
    [Export]
    public Color SelectedColor { get; set; } = new Color(0, 0.8f, 1, 0.6f);

    /// <summary>
    /// Color for adjacency lines.
    /// </summary>
    [Export]
    public Color AdjacencyColor { get; set; } = new Color(0.5f, 0.5f, 1, 0.3f);

    #endregion

    /// <summary>
    /// Initialize the debug renderer.
    /// </summary>
    public void Initialize(IrregularMesh mesh, IrregularMeshMapData? mapData = null, float worldScale = 1f)
    {
        _mesh = mesh;
        _mapData = mapData;
        _worldScale = worldScale;
        QueueRedraw();
    }

    /// <summary>
    /// Highlight a path (list of cell IDs).
    /// </summary>
    public void HighlightPath(List<int>? path)
    {
        _highlightedPath = path;
        QueueRedraw();
    }

    /// <summary>
    /// Highlight a set of cells.
    /// </summary>
    public void HighlightCells(HashSet<int>? cells)
    {
        _highlightedCells = cells;
        QueueRedraw();
    }

    /// <summary>
    /// Select a specific cell for detailed display.
    /// </summary>
    public void SelectCell(int? cellId)
    {
        _selectedCell = cellId;
        QueueRedraw();
    }

    /// <summary>
    /// Clear all highlights.
    /// </summary>
    public void ClearHighlights()
    {
        _highlightedPath = null;
        _highlightedCells = null;
        _selectedCell = null;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_mesh == null) return;

        // Draw in order: highlights first, then wireframe, then markers
        if (_highlightedCells != null)
        {
            DrawHighlightedCells();
        }

        if (_selectedCell.HasValue)
        {
            DrawSelectedCell();
        }

        if (_showAdjacency)
        {
            DrawAdjacency();
        }

        if (_showWireframe)
        {
            DrawWireframe();
        }

        if (_highlightedPath != null)
        {
            DrawPath();
        }

        if (_showVertexMarkers || _showTerrainTypes)
        {
            DrawVertexMarkers();
        }

        if (_showQuadIds)
        {
            DrawQuadIds();
        }
    }

    private void DrawWireframe()
    {
        foreach (var quad in _mesh!.Quads)
        {
            var corners = quad.GetCornerPositions();
            if (corners.Length != 4) continue;

            for (int i = 0; i < 4; i++)
            {
                var from = corners[i] * _worldScale;
                var to = corners[(i + 1) % 4] * _worldScale;
                DrawLine(from, to, WireframeColor, 1f);
            }
        }
    }

    private void DrawVertexMarkers()
    {
        foreach (var vertex in _mesh!.Vertices)
        {
            var pos = vertex.Position * _worldScale;

            Color color;
            if (_showTerrainTypes)
            {
                color = GetTerrainColor(vertex.TerrainType);
            }
            else
            {
                color = VertexColor;
            }

            DrawCircle(pos, 3f, color);
        }
    }

    private void DrawQuadIds()
    {
        var font = ThemeDB.FallbackFont;
        if (font == null) return;

        foreach (var quad in _mesh!.Quads)
        {
            var center = quad.Centroid * _worldScale;
            DrawString(font, center, quad.Id.ToString(), HorizontalAlignment.Center,
                -1, 10, new Color(1, 1, 1, 0.7f));
        }
    }

    private void DrawPath()
    {
        if (_highlightedPath == null || _highlightedPath.Count < 2) return;

        for (int i = 0; i < _highlightedPath.Count - 1; i++)
        {
            var fromCell = _highlightedPath[i];
            var toCell = _highlightedPath[i + 1];

            if (fromCell >= _mesh!.Quads.Count || toCell >= _mesh.Quads.Count) continue;

            var from = _mesh.Quads[fromCell].Centroid * _worldScale;
            var to = _mesh.Quads[toCell].Centroid * _worldScale;

            DrawLine(from, to, PathColor, 3f);
            DrawCircle(from, 5f, PathColor);
        }

        // Draw final point
        if (_highlightedPath.Count > 0)
        {
            var lastCell = _highlightedPath[^1];
            if (lastCell < _mesh!.Quads.Count)
            {
                var pos = _mesh.Quads[lastCell].Centroid * _worldScale;
                DrawCircle(pos, 7f, PathColor);
            }
        }
    }

    private void DrawHighlightedCells()
    {
        if (_highlightedCells == null) return;

        foreach (var cellId in _highlightedCells)
        {
            if (cellId >= _mesh!.Quads.Count) continue;

            var quad = _mesh.Quads[cellId];
            var corners = quad.GetCornerPositions();
            if (corners.Length != 4) continue;

            // Draw filled quad
            var scaledCorners = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                scaledCorners[i] = corners[i] * _worldScale;
            }

            DrawColoredPolygon(scaledCorners, HighlightColor);
        }
    }

    private void DrawSelectedCell()
    {
        if (!_selectedCell.HasValue || _selectedCell.Value >= _mesh!.Quads.Count) return;

        var quad = _mesh.Quads[_selectedCell.Value];
        var corners = quad.GetCornerPositions();
        if (corners.Length != 4) return;

        var scaledCorners = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            scaledCorners[i] = corners[i] * _worldScale;
        }

        DrawColoredPolygon(scaledCorners, SelectedColor);

        // Draw outline
        for (int i = 0; i < 4; i++)
        {
            DrawLine(scaledCorners[i], scaledCorners[(i + 1) % 4], new Color(0, 0.8f, 1, 1f), 2f);
        }
    }

    private void DrawAdjacency()
    {
        var drawnEdges = new HashSet<(int, int)>();

        foreach (var quad in _mesh!.Quads)
        {
            var fromCenter = quad.Centroid * _worldScale;

            foreach (var adjId in quad.AdjacentQuadIds)
            {
                // Avoid drawing the same edge twice
                var edge = (Mathf.Min(quad.Id, adjId), Mathf.Max(quad.Id, adjId));
                if (drawnEdges.Contains(edge)) continue;
                drawnEdges.Add(edge);

                if (adjId >= _mesh.Quads.Count) continue;
                var toCenter = _mesh.Quads[adjId].Centroid * _worldScale;

                DrawLine(fromCenter, toCenter, AdjacencyColor, 1f);
            }
        }
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
