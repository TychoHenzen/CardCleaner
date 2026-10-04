#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class DualGridAutoTilePreview
{
    private void InitializeSamplePattern()
    {
        for (var row = 3; row <= 6; row++)
        for (var col = 5; col <= 9; col++)
            _dataGrid[row, col] = true;

        for (var row = 1; row <= 2; row++)
        for (var col = 2; col <= 3; col++)
            _dataGrid[row, col] = true;

        RecomputeVisualBitmasks();
    }

    public void SetScale(float scale)
    {
        _scale = scale;
        UpdateSize();
        QueueRedraw();
    }

    public void SetShowDataGrid(bool show)
    {
        _showDataGrid = show;
        QueueRedraw();
    }

    public void SetTiles(
        EditableTile? overlayTile,
        EditableTile? baseTile,
        float scale,
        string? formatOverride = null)
    {
        _overlayTile = overlayTile;
        _baseTile = baseTile;
        _scale = scale;
        _formatOverride = formatOverride;
        _tileSize = _service!.TileSet?.TileSize ?? new Vector2I(16, 16);
        UpdateSize();
        RecomputeVisualBitmasks();
        QueueRedraw();
        EmitInfo();
    }

    private string GetEffectiveFormat()
    {
        if (!string.IsNullOrEmpty(_formatOverride))
            return _formatOverride;
        return _overlayTile?.AutoTileFormat?.ToLowerInvariant() ?? "corner16";
    }

    public void ClearPreview()
    {
        _overlayTile = null;
        _baseTile = null;
        UpdateSize();
        QueueRedraw();
    }

    private void UpdateSize()
    {
        if (_overlayTile == null)
        {
            CustomMinimumSize = Vector2.Zero;
            return;
        }

        var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;
        CustomMinimumSize = new Vector2(
            (DataGridCols + 1) * scaledTileSize.X,
            (DataGridRows + 1) * scaledTileSize.Y);
    }
}
#endif
