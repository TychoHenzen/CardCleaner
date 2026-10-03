#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Visual tile picker that displays a tileset atlas and allows clicking to select tiles.
/// Wrap in a ScrollContainer for scrollable behavior.
/// </summary>
[Tool]
public partial class TilesetAtlasPicker : Control
{
    private const float DisplayScale = 4f;

    private TileSetAtlasSource? _source;
    private Texture2D? _texture;
    private Vector2I _tileSize = new(16, 16);
    private Vector2I _selectedCoords = new(-1, -1);
    private Vector2I _hoveredCoords = new(-1, -1);
    private Vector2I _selectedSize = new(1, 1); // Size of the selected tile (for multi-tile support)
    private float _sourceScale = 1.0f; // Source scale factor (0.5=32px, 1.0=16px, 2.0=8px)
    private int _sourceId;

    // Actual tile size in source texture pixels (adjusted by source scale)
    // For scale 2.0 (8px sources): 16/2.0 = 8px tiles
    // For scale 0.5 (32px sources): 16/0.5 = 32px tiles
    private Vector2I ActualSourceTileSize => new Vector2I(
        (int)(_tileSize.X / _sourceScale),
        (int)(_tileSize.Y / _sourceScale)
    );

    private AtlasPickerSelection Selection => new(_hoveredCoords, _selectedCoords, _selectedSize);

    [Signal]
    public delegate void TileSelectedEventHandler(Vector2I atlasCoords, int sourceId);

    public Vector2I SelectedCoords
    {
        get => _selectedCoords;
        set
        {
            if (_selectedCoords != value)
            {
                _selectedCoords = value;
                QueueRedraw();
            }
        }
    }

    /// <summary>
    /// Size of the tile being selected (for multi-tile support).
    /// Default is (1, 1) for single-cell tiles.
    /// </summary>
    public Vector2I SelectedSize
    {
        get => _selectedSize;
        set
        {
            var clamped = new Vector2I(Mathf.Max(1, value.X), Mathf.Max(1, value.Y));
            if (_selectedSize != clamped)
            {
                _selectedSize = clamped;
                QueueRedraw();
            }
        }
    }

    public int SourceId
    {
        get => _sourceId;
        set
        {
            if (_sourceId != value)
            {
                _sourceId = value;
            }
        }
    }

    public void SetSource(TileSetAtlasSource? source, Vector2I tileSize, int sourceId, float sourceScale = 1.0f)
    {
        _source = source;
        _sourceId = sourceId;
        _tileSize = tileSize;
        _sourceScale = sourceScale > 0 ? sourceScale : 1.0f;
        _texture = source?.Texture;

        if (_texture != null)
        {
            CustomMinimumSize = _texture.GetSize() * DisplayScale;
        }
        else
        {
            CustomMinimumSize = Vector2.Zero;
        }

        QueueRedraw();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        TextureFilter = TextureFilterEnum.Nearest;
        MouseExited += OnMouseExited;
    }

    public override void _Draw()
    {
        if (_texture == null || _tileSize.X <= 0 || _tileSize.Y <= 0)
            return;

        var layout = new AtlasGridLayout(_texture.GetSize(), ActualSourceTileSize, DisplayScale);

        // Draw the atlas texture scaled
        DrawTextureRect(_texture, new Rect2(Vector2.Zero, layout.ScaledSize), false);

        AtlasPickerPainter.DrawHover(this, layout, Selection);
        AtlasPickerPainter.DrawSelection(this, layout, Selection);
        AtlasPickerPainter.DrawGridLines(this, layout);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_texture == null || _tileSize.X <= 0 || _tileSize.Y <= 0)
            return;

        var layout = new AtlasGridLayout(_texture.GetSize(), ActualSourceTileSize, DisplayScale);

        if (@event is InputEventMouseMotion motion)
            OnMouseMotion(layout.CellAt(motion.Position));
        else if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouseButton)
            OnLeftPressed(layout.CellAt(mouseButton.Position));
    }

    private void OnMouseMotion(Vector2I coords)
    {
        if (coords == _hoveredCoords)
            return;

        _hoveredCoords = coords;
        QueueRedraw();
    }

    private void OnLeftPressed(Vector2I coords)
    {
        if (coords.X < 0 || coords.Y < 0)
            return;

        _selectedCoords = coords;
        QueueRedraw();
        EmitSignal(SignalName.TileSelected, coords, _sourceId);
    }

    private void OnMouseExited()
    {
        _hoveredCoords = new Vector2I(-1, -1);
        QueueRedraw();
    }
}
#endif
