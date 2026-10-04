#if TOOLS
using System;
using System.IO;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TmxPreviewPanel
{
    private void OnScaleChanged(double value)
    {
        _scaleLabel!.Text = $"{value:F1}x";
        _previewControl?.SetScale((float)value);
    }

    private void OnShowDataGridToggled(bool pressed)
    {
        _previewControl?.SetShowDataGrid(pressed);
    }

    private void OnInfoChanged(string info)
    {
        if (_infoLabel != null)
            _infoLabel.Text = info;
    }

    private void PopulateBaseTileSelector()
    {
        if (_baseTileSelector == null)
            return;

        _baseTileSelector.Clear();
        _availableBaseTiles.Clear();
        _baseTileSelector.AddItem("-- None (checkerboard) --", 0);
        if (_currentMapData == null)
            return;

        var index = 1;
        foreach (var tilesetRef in _currentMapData.Tilesets)
        {
            var tilesetName = Path.GetFileNameWithoutExtension(tilesetRef.TsxPath);
            foreach (var tileDef in tilesetRef.TilesetData.Tiles)
            {
                if (tileDef.HasAutoTileVariants)
                    continue;

                _availableBaseTiles.Add(new TmxPreviewTileOption(tileDef, tilesetRef));
                var displayText = $"{tileDef.Name} ({tileDef.Id}) [{tilesetName}]";
                _baseTileSelector.AddItem(displayText, index);
                _baseTileSelector.SetItemMetadata(index, index - 1);
                index++;
            }
        }
    }

    private void OnBaseTileSelected(long index)
    {
        if (index == 0)
        {
            _selectedBaseTile = null;
            _selectedBaseTileTileset = null;
        }
        else
        {
            var listIndex = _baseTileSelector!.GetItemMetadata((int)index).AsInt32();
            if (listIndex >= 0 && listIndex < _availableBaseTiles.Count)
            {
                var option = _availableBaseTiles[listIndex];
                _selectedBaseTile = option.Tile;
                _selectedBaseTileTileset = option.Tileset;
            }
        }

        _previewControl?.SetBaseTile(_selectedBaseTile, _selectedBaseTileTileset);
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
        RefreshAutoTilePreview();
    }

    private void RefreshAutoTilePreview()
    {
        if (_selectedAutoTile == null || _selectedAutoTileTileset == null)
            return;

        _previewControl?.SetAutoTilePreview(
            _selectedAutoTile,
            _selectedAutoTileTileset,
            (float)_scaleSlider!.Value,
            _formatOverride);
    }

    private void PopulateAutoTileSelector()
    {
        if (_autoTileSelector == null)
            return;

        _autoTileSelector.Clear();
        _availableAutoTiles.Clear();
        _autoTileSelector.AddItem("-- TMX View (no auto-tile) --", 0);
        if (_currentMapData == null)
            return;

        var index = 1;
        foreach (var tilesetRef in _currentMapData.Tilesets)
        {
            var tilesetName = Path.GetFileNameWithoutExtension(tilesetRef.TsxPath);
            foreach (var tileDef in tilesetRef.TilesetData.Tiles)
            {
                if (!tileDef.HasAutoTileVariants)
                    continue;

                _availableAutoTiles.Add(new TmxPreviewTileOption(tileDef, tilesetRef));
                var displayText = $"{tileDef.Name} ({tileDef.Id}) [{tilesetName}]";
                _autoTileSelector.AddItem(displayText, index);
                _autoTileSelector.SetItemMetadata(index, index - 1);
                index++;
            }
        }
    }

    private void OnAutoTileSelected(long index)
    {
        if (index == 0)
        {
            _selectedAutoTile = null;
            _selectedAutoTileTileset = null;
            if (_currentMapData != null)
                _previewControl?.LoadTmxMap(_currentMapData, (float)_scaleSlider!.Value);
            return;
        }

        var listIndex = _autoTileSelector!.GetItemMetadata((int)index).AsInt32();
        if (listIndex < 0 || listIndex >= _availableAutoTiles.Count)
            return;

        var option = _availableAutoTiles[listIndex];
        _selectedAutoTile = option.Tile;
        _selectedAutoTileTileset = option.Tileset;
        _previewControl?.SetAutoTilePreview(
            option.Tile,
            option.Tileset,
            (float)_scaleSlider!.Value,
            _formatOverride);
    }

    public void Refresh()
    {
        if (!string.IsNullOrEmpty(_currentTmxPath))
            LoadTmxFile(_currentTmxPath);
        PopulateBaseTileSelector();
        PopulateAutoTileSelector();
    }
}
#endif
