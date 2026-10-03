#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Button representing a single tile in the grid - uses direct drawing for proper scaling.
/// Fetches texture on demand to handle C# assembly reloads gracefully.
/// </summary>
[Tool]
public partial class TileButton : Button
{
    private readonly int _baseSize;
    private readonly int _padding;
    private readonly TileEditorService? _service;
    private readonly EditableTile? _tile;
    private readonly Color _bgColor;
    private readonly Vector2I _tileSize;
    private bool _isSelected;

    public TileButton() { }

    public TileButton(EditableTile tile, int baseSize, int padding, TileEditorService service)
    {
        _baseSize = baseSize;
        _padding = padding;
        _service = service;
        _tile = tile;
        _tileSize = new Vector2I(tile.SizeX, tile.SizeY);

        var maxDimension = Mathf.Max(tile.SizeX, tile.SizeY);
        var widthSize = baseSize * tile.SizeX / maxDimension;
        var heightSize = baseSize * tile.SizeY / maxDimension;
        var totalWidth = widthSize + padding * 2;
        var totalHeight = heightSize + padding * 2;
        CustomMinimumSize = new Vector2(totalWidth, totalHeight + 16);

        var sizeLabel = tile.SizeX > 1 || tile.SizeY > 1
            ? $" [{tile.SizeX}x{tile.SizeY}]"
            : "";
        TooltipText =
            $"{tile.Name}{sizeLabel}\n{tile.Id}\n{tile.Passability}\n" +
            $"Source: {tile.SourceId}, Atlas: ({tile.AtlasX},{tile.AtlasY})";
        Flat = true;
        TextureFilter = TextureFilterEnum.Nearest;
        _bgColor = tile.Passability.ToLowerInvariant() switch
        {
            "solid" => new Color(0.6f, 0.3f, 0.3f, 0.5f),
            "partially_passable" => new Color(0.6f, 0.6f, 0.3f, 0.5f),
            _ => new Color(0.3f, 0.6f, 0.3f, 0.5f)
        };
    }

    public override void _Draw()
    {
        if (_tile == null || _service == null)
            return;

        var maxDimension = Mathf.Max(_tileSize.X, _tileSize.Y);
        var displayWidth = _baseSize * _tileSize.X / maxDimension;
        var displayHeight = _baseSize * _tileSize.Y / maxDimension;
        var totalWidth = displayWidth + _padding * 2;
        var totalHeight = displayHeight + _padding * 2;
        DrawRect(new Rect2(0, 0, totalWidth, totalHeight), _bgColor);
        DrawSelectionBorder(totalWidth, totalHeight);
        DrawMultiTileIndicator(totalWidth, totalHeight);

        var texture = _service.GetTileTexture(_tile);
        var region = _service.GetTileTextureRegion(_tile);
        if (texture != null)
        {
            var srcRect = new Rect2(region.Position, region.Size);
            var destRect = new Rect2(_padding, _padding, displayWidth, displayHeight);
            DrawTextureRectRegion(texture, destRect, srcRect);
        }
        else
        {
            DrawString(
                ThemeDB.FallbackFont,
                new Vector2(_padding, totalHeight / 2),
                $"({_tile.AtlasX},{_tile.AtlasY})",
                HorizontalAlignment.Center,
                displayWidth);
        }

        var displayName = _tile.Name.Length > 10 ? _tile.Name[..10] + ".." : _tile.Name;
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(0, totalHeight + 12),
            displayName,
            HorizontalAlignment.Center,
            totalWidth,
            10);
    }

    private void DrawSelectionBorder(float totalWidth, float totalHeight)
    {
        if (_isSelected)
        {
            DrawRect(
                new Rect2(0, 0, totalWidth, totalHeight),
                new Color(1, 0.8f, 0, 1),
                false,
                2);
        }
    }

    private void DrawMultiTileIndicator(float totalWidth, float totalHeight)
    {
        if (_tileSize.X <= 1 && _tileSize.Y <= 1)
            return;

        var sizeText = $"{_tileSize.X}x{_tileSize.Y}";
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(totalWidth - 24, 12),
            sizeText,
            HorizontalAlignment.Right,
            24,
            10,
            new Color(1, 1, 0, 0.9f));
    }

    public void UpdateTile(EditableTile tile)
    {
        TooltipText =
            $"{tile.Name}\n{tile.Id}\n{tile.Passability}\n" +
            $"Source: {tile.SourceId}, Atlas: ({tile.AtlasX},{tile.AtlasY})";
        QueueRedraw();
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        QueueRedraw();
    }
}
#endif
