using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Debug.Painting;
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

        var painter = new MeshDebugPainter(this, _mesh, _worldScale);

        // Draw in order: highlights first, then wireframe, then markers
        if (_highlightedCells != null)
        {
            painter.DrawHighlightedCells(_highlightedCells, HighlightColor);
        }

        if (_selectedCell.HasValue)
        {
            painter.DrawSelectedCell(_selectedCell.Value, SelectedColor);
        }

        if (_showAdjacency)
        {
            painter.DrawAdjacency(AdjacencyColor);
        }

        if (_showWireframe)
        {
            painter.DrawWireframe(WireframeColor);
        }

        if (_highlightedPath != null)
        {
            painter.DrawPath(_highlightedPath, PathColor);
        }

        if (_showVertexMarkers || _showTerrainTypes)
        {
            painter.DrawVertexMarkers(_showTerrainTypes, VertexColor);
        }

        if (_showQuadIds)
        {
            painter.DrawQuadIds();
        }
    }
}
