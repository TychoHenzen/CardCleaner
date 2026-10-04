#if TOOLS
using System;
using CardCleaner.Scripts.Core.Services.TilesetLoading;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TmxPreviewControl
{
    internal void SetBaseTile(TileDefinition? baseTile, TmxTilesetReference? tilesetRef)
    {
        _baseTileDef = baseTile;
        _baseTilesetRef = tilesetRef;
        QueueRedraw();
    }

    internal void LoadTmxMap(TmxMapData mapData, float scale)
    {
        _mapData = mapData;
        _scale = scale;
        _tileSize = mapData.Tilesets.Count > 0
            ? mapData.Tilesets[0].TilesetData.TilesetConfig.BaseTileSize
            : new Vector2I(16, 16);
        _isAutoTileMode = false;
        _autoTileDef = null;
        _currentTilesetRef = null;
        GD.Print(
            $"[TmxPreviewControl] LoadTmxMap: {mapData.Tilesets.Count} "
            + $"tilesets, tileSize={_tileSize}");
        foreach (var tileset in mapData.Tilesets)
        {
            GD.Print(
                $"[TmxPreviewControl]   Tileset: {tileset.TsxPath}, "
                + $"TilesetPath='{tileset.TilesetData.TilesetPath}', "
                + $"Tiles={tileset.TilesetData.Tiles.Count}");
        }

        UpdateSize();
        QueueRedraw();
        EmitInfo();
    }

    public void SetScale(float scale)
    {
        _scale = scale;
        UpdateSize();
        QueueRedraw();
    }

    public void ClearPreview()
    {
        _mapData = null;
        _autoTileDef = null;
        _isAutoTileMode = false;
        UpdateSize();
        QueueRedraw();
        EmitSignal(SignalName.InfoChanged, "No TMX file loaded.");
    }

    internal void SetAutoTilePreview(
        TileDefinition tileDef,
        TmxTilesetReference tilesetRef,
        float scale,
        string? formatOverride = null)
    {
        _autoTileDef = tileDef;
        _currentTilesetRef = tilesetRef;
        _isAutoTileMode = true;
        _scale = scale;
        _formatOverride = formatOverride;
        _tileSize = tilesetRef.TilesetData.TilesetConfig.BaseTileSize;
        InitializeDataGrid();
        RecomputeVisualBitmasks();
        UpdateSize();
        QueueRedraw();
        EmitAutoTileInfo();
    }

    private string GetEffectiveFormat()
    {
        if (!string.IsNullOrEmpty(_formatOverride))
            return _formatOverride;
        return _autoTileDef?.AutoTileFormatName?.ToLowerInvariant() ?? "corner16";
    }

    public void SetShowDataGrid(bool show)
    {
        _showDataGrid = show;
        QueueRedraw();
    }

    private void InitializeDataGrid()
    {
        Array.Clear(_dataGrid);
        for (var row = 3; row <= 6; row++)
        for (var col = 5; col <= 9; col++)
            _dataGrid[row, col] = true;
        for (var row = 1; row <= 2; row++)
        for (var col = 2; col <= 3; col++)
            _dataGrid[row, col] = true;
    }

    private void RecomputeVisualBitmasks()
    {
        if (_autoTileDef == null)
            return;

        if (GetEffectiveFormat() == "blob47")
        {
            RecomputeBlobBitmasks();
            return;
        }

        _visualBitmasks = DualGridAutoTile.ComputeAllBitmasks(_dataGrid);
    }

    private void RecomputeBlobBitmasks()
    {
        _visualBitmasks = new int[DataGridRows, DataGridCols];
        for (var row = 0; row < DataGridRows; row++)
        for (var col = 0; col < DataGridCols; col++)
        {
            if (!_dataGrid[row, col])
            {
                _visualBitmasks[row, col] = -1;
                continue;
            }

            var position = new Vector2I(col, row);
            _visualBitmasks[row, col] = NeighborBitmask8.Compute(position, neighborPos =>
            {
                if (neighborPos.X < 0 || neighborPos.X >= DataGridCols
                    || neighborPos.Y < 0 || neighborPos.Y >= DataGridRows)
                {
                    return false;
                }

                return _dataGrid[neighborPos.Y, neighborPos.X];
            });
        }
    }

    private void EmitAutoTileInfo()
    {
        if (_autoTileDef == null)
            return;

        var filledCount = CountFilledCells();
        var format = GetEffectiveFormat();
        var formatText = string.IsNullOrEmpty(_formatOverride)
            ? format
            : $"{format} (overridden)";
        var info = $"Auto-tile: {_autoTileDef.Name} ({formatText}). "
            + $"Data grid: {filledCount}/{DataGridRows * DataGridCols} cells. "
            + "Click to toggle.";
        EmitSignal(SignalName.InfoChanged, info);
    }

    private int CountFilledCells()
    {
        var filledCount = 0;
        for (var row = 0; row < DataGridRows; row++)
        for (var col = 0; col < DataGridCols; col++)
        {
            if (_dataGrid[row, col])
                filledCount++;
        }

        return filledCount;
    }

    private void UpdateSize()
    {
        var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;
        if (_isAutoTileMode && _autoTileDef != null)
        {
            CustomMinimumSize = new Vector2(
                (DataGridCols + 1) * scaledTileSize.X,
                (DataGridRows + 1) * scaledTileSize.Y);
            return;
        }

        if (_mapData == null)
        {
            CustomMinimumSize = Vector2.Zero;
            return;
        }

        var bounds = _mapData.GetBounds();
        var width = bounds.Max.X - bounds.Min.X + 1;
        var height = bounds.Max.Y - bounds.Min.Y + 1;
        CustomMinimumSize = new Vector2(
            width * scaledTileSize.X,
            height * scaledTileSize.Y);
    }

    private void EmitInfo()
    {
        if (_mapData == null)
        {
            EmitSignal(SignalName.InfoChanged, "No TMX file loaded.");
            return;
        }

        var bounds = _mapData.GetBounds();
        var tileCount = 0;
        foreach (var _ in _mapData.GetAllTiles())
            tileCount++;
        var info = $"TMX loaded: {_mapData.Tilesets.Count} tileset(s), {tileCount} tiles. "
            + $"Bounds: ({bounds.Min.X},{bounds.Min.Y}) to "
            + $"({bounds.Max.X},{bounds.Max.Y}). Hover for tile info.";
        EmitSignal(SignalName.InfoChanged, info);
    }
}
#endif
