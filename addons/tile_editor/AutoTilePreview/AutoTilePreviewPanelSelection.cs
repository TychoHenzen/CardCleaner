#if TOOLS
using System;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class AutoTilePreviewPanel
{
    private void OnInfoChanged(string info)
    {
        if (_infoLabel != null)
            _infoLabel.Text = info;
    }

    private void OnShowDataGridToggled(bool pressed)
    {
        _mapPreview?.SetShowDataGrid(pressed);
    }

    private void PopulateDropdowns()
    {
        PopulateTileSelector();
        PopulateBaseTileSelector();
        PopulateFormatDropdown();
    }

    private void PopulateFormatDropdown()
    {
        if (_formatOverrideDropdown == null || _service == null)
            return;

        _formatOverrideDropdown.Clear();
        _formatOverrideDropdown.AddItem("(Use tile's format)", 0);
        _formatOverrideDropdown.SetItemMetadata(0, "");

        var index = 1;
        foreach (var formatName in _service.GetAvailableFormatNames())
        {
            var displayName = _service.GetFormatDisplayName(formatName);
            _formatOverrideDropdown.AddItem(displayName, index);
            _formatOverrideDropdown.SetItemMetadata(index, formatName);
            index++;
        }
    }

    private void OnFormatOverrideSelected(long index)
    {
        if (_formatOverrideDropdown == null)
            return;

        var metadata = _formatOverrideDropdown.GetItemMetadata((int)index).AsString();
        _formatOverride = string.IsNullOrEmpty(metadata) ? null : metadata;
        RefreshPreview();
    }

    private void PopulateTileSelector()
    {
        _tileSelector!.Clear();
        _tileSelector.AddItem("-- Select tile --", 0);
        var index = 1;
        foreach (var tile in _service!.AllTiles)
        {
            if (!tile.HasAutoTileVariants)
                continue;

            _tileSelector.AddItem($"{tile.Name} ({tile.Id})", index);
            _tileSelector.SetItemMetadata(index, tile.Id);
            index++;
        }
    }

    private void PopulateBaseTileSelector()
    {
        _baseTileSelector!.Clear();
        _baseTileSelector.AddItem("-- Select base --", 0);
        var index = 1;
        foreach (var tile in _service!.AllTiles)
        {
            if (!tile.Layer.Equals("terrain", StringComparison.OrdinalIgnoreCase))
                continue;

            _baseTileSelector.AddItem($"{tile.Name} ({tile.Id})", index);
            _baseTileSelector.SetItemMetadata(index, tile.Id);
            index++;
        }
    }

    private void OnTileSelected(long index)
    {
        if (index == 0)
        {
            _selectedTileId = null;
            ClearPreview();
            return;
        }

        _selectedTileId = _tileSelector!.GetItemMetadata((int)index).AsString();
        RefreshPreview();
    }

    private void OnBaseTileSelected(long index)
    {
        _selectedBaseTileId = index == 0
            ? null
            : _baseTileSelector!.GetItemMetadata((int)index).AsString();
        RefreshPreview();
    }

    private void OnScaleChanged(double value)
    {
        _scaleLabel!.Text = $"{value:F1}x";
        _mapPreview?.SetScale((float)value);
    }

    private void ClearPreview()
    {
        _infoLabel!.Text = "Select a tile with auto-tile variants to preview.";
        _mapPreview?.ClearPreview();
    }

    private void RefreshPreview()
    {
        if (string.IsNullOrEmpty(_selectedTileId))
        {
            ClearPreview();
            return;
        }

        var tile = _service!.GetTile(_selectedTileId);
        if (tile == null || !tile.HasAutoTileVariants)
        {
            _infoLabel!.Text = "Selected tile has no auto-tile variants.";
            _mapPreview?.ClearPreview();
            return;
        }

        var formatName = _formatOverride
            ?? tile.AutoTileFormat?.ToLowerInvariant()
            ?? "corner16";
        var format = _service.GetFormatDefinition(formatName);
        var displayName = format == null
            ? formatName
            : _service.GetFormatDisplayName(formatName);
        UpdatePreviewInfo(displayName);
        _mapPreview?.SetTiles(
            tile,
            GetSelectedBaseTile(),
            (float)_scaleSlider!.Value,
            _formatOverride);
    }

    private void UpdatePreviewInfo(string formatDisplayName)
    {
        var suffix = _formatOverride == null ? "" : " - overridden";
        _infoLabel!.Text =
            $"Click cells to toggle ({formatDisplayName}{suffix}). "
            + "Visual grid shows correct transitions.";
    }

    private EditableTile? GetSelectedBaseTile()
    {
        return string.IsNullOrEmpty(_selectedBaseTileId)
            ? null
            : _service!.GetTile(_selectedBaseTileId);
    }

    public void Refresh()
    {
        PopulateDropdowns();
        if (!string.IsNullOrEmpty(_selectedTileId))
            RefreshPreview();
    }
}
#endif
