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
    private const float Scale = 4f;

    private TileSetAtlasSource? _source;
    private Texture2D? _texture;
    private Vector2I _tileSize = new(16, 16);
    private Vector2I _selectedCoords = new(-1, -1);
    private Vector2I _hoveredCoords = new(-1, -1);
    private Vector2I _selectedSize = new(1, 1); // Size of the selected tile (for multi-tile support)
    private int _sourceId;

    private static readonly Color GridColor = new(0.3f, 0.3f, 0.3f, 0.8f);
    private static readonly Color SelectionColor = new(1f, 0.8f, 0f, 0.8f);
    private static readonly Color HoverColor = new(1f, 1f, 1f, 0.4f);

    private Vector2 ScaledTileSize => new Vector2(_tileSize.X, _tileSize.Y) * Scale;

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

    public void SetSource(TileSetAtlasSource? source, Vector2I tileSize, int sourceId)
    {
        _source = source;
        _sourceId = sourceId;
        _tileSize = tileSize;
        _texture = source?.Texture;

        if (_texture != null)
        {
            CustomMinimumSize = _texture.GetSize() * Scale;
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

        var textureSize = _texture.GetSize();
        var scaledSize = textureSize * Scale;
        var scaledTile = ScaledTileSize;

        // Draw the atlas texture scaled
        DrawTextureRect(_texture, new Rect2(Vector2.Zero, scaledSize), false);

        // Calculate grid dimensions
        int cols = (int)(textureSize.X / _tileSize.X);
        int rows = (int)(textureSize.Y / _tileSize.Y);

        // Draw hover highlight (uses SelectedSize for multi-tile preview)
        if (_hoveredCoords.X >= 0 && _hoveredCoords.Y >= 0 &&
            _hoveredCoords.X < cols && _hoveredCoords.Y < rows)
        {
            var hoverRect = new Rect2(
                _hoveredCoords.X * scaledTile.X,
                _hoveredCoords.Y * scaledTile.Y,
                scaledTile.X * _selectedSize.X,
                scaledTile.Y * _selectedSize.Y
            );
            DrawRect(hoverRect, HoverColor);

            // Draw grid lines within multi-tile hover area
            if (_selectedSize.X > 1 || _selectedSize.Y > 1)
            {
                for (int dx = 1; dx < _selectedSize.X; dx++)
                {
                    var xPos = (_hoveredCoords.X + dx) * scaledTile.X;
                    DrawLine(
                        new Vector2(xPos, _hoveredCoords.Y * scaledTile.Y),
                        new Vector2(xPos, (_hoveredCoords.Y + _selectedSize.Y) * scaledTile.Y),
                        HoverColor * 1.5f
                    );
                }
                for (int dy = 1; dy < _selectedSize.Y; dy++)
                {
                    var yPos = (_hoveredCoords.Y + dy) * scaledTile.Y;
                    DrawLine(
                        new Vector2(_hoveredCoords.X * scaledTile.X, yPos),
                        new Vector2((_hoveredCoords.X + _selectedSize.X) * scaledTile.X, yPos),
                        HoverColor * 1.5f
                    );
                }
            }
        }

        // Draw selection highlight (uses SelectedSize for multi-tile)
        if (_selectedCoords.X >= 0 && _selectedCoords.Y >= 0 &&
            _selectedCoords.X < cols && _selectedCoords.Y < rows)
        {
            var selectRect = new Rect2(
                _selectedCoords.X * scaledTile.X,
                _selectedCoords.Y * scaledTile.Y,
                scaledTile.X * _selectedSize.X,
                scaledTile.Y * _selectedSize.Y
            );
            // Draw selection as filled rectangle with transparency
            DrawRect(selectRect, SelectionColor with { A = 0.3f });
            // Draw selection outline
            DrawRect(selectRect, SelectionColor, false, 3.0f);
        }

        // Draw vertical grid lines
        for (int x = 0; x <= cols; x++)
        {
            var xPos = x * scaledTile.X;
            DrawLine(new Vector2(xPos, 0), new Vector2(xPos, scaledSize.Y), GridColor);
        }

        // Draw horizontal grid lines
        for (int y = 0; y <= rows; y++)
        {
            var yPos = y * scaledTile.Y;
            DrawLine(new Vector2(0, yPos), new Vector2(scaledSize.X, yPos), GridColor);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_texture == null || _tileSize.X <= 0 || _tileSize.Y <= 0)
            return;

        var textureSize = _texture.GetSize();
        int cols = (int)(textureSize.X / _tileSize.X);
        int rows = (int)(textureSize.Y / _tileSize.Y);

        if (@event is InputEventMouseMotion motion)
        {
            var coords = CalculateAtlasCoords(motion.Position, cols, rows);
            if (coords != _hoveredCoords)
            {
                _hoveredCoords = coords;
                QueueRedraw();
            }
        }
        else if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
            {
                var coords = CalculateAtlasCoords(mouseButton.Position, cols, rows);
                if (coords.X >= 0 && coords.Y >= 0)
                {
                    _selectedCoords = coords;
                    QueueRedraw();
                    EmitSignal(SignalName.TileSelected, coords, _sourceId);
                }
            }
        }
    }

    private Vector2I CalculateAtlasCoords(Vector2 position, int maxCols, int maxRows)
    {
        var scaledTile = ScaledTileSize;
        int x = (int)(position.X / scaledTile.X);
        int y = (int)(position.Y / scaledTile.Y);

        if (x < 0 || x >= maxCols || y < 0 || y >= maxRows)
            return new Vector2I(-1, -1);

        return new Vector2I(x, y);
    }

    private void OnMouseExited()
    {
        _hoveredCoords = new Vector2I(-1, -1);
        QueueRedraw();
    }
}
#endif
