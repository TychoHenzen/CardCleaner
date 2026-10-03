#if TOOLS
using System.IO;
using CardCleaner.Scripts.Core.Services.TilesetLoading;
using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TmxPreviewControl
{
    public override void _GuiInput(InputEvent @event)
    {
        if (_isAutoTileMode && _autoTileDef != null)
        {
            HandleAutoTileModeInput(@event);
            return;
        }

        if (_mapData == null)
            return;

        if (@event is InputEventMouseMotion mouseMotion)
            UpdateTooltip(mouseMotion.Position);
    }

    private void HandleAutoTileModeInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
                ToggleAutoTileCell(mouseButton.Position);
            return;
        }

        if (@event is InputEventMouseMotion mouseMotion)
            UpdateAutoTileTooltip(mouseMotion.Position);
    }

    private void ToggleAutoTileCell(Vector2 position)
    {
        var scaledTileSize = GetScaledTileSize();
        var halfTile = scaledTileSize / 2;
        var adjustedPosition = position - halfTile;
        var col = (int)(adjustedPosition.X / scaledTileSize.X);
        var row = (int)(adjustedPosition.Y / scaledTileSize.Y);
        if (row < 0 || row >= DataGridRows || col < 0 || col >= DataGridCols)
            return;

        _dataGrid[row, col] = !_dataGrid[row, col];
        RecomputeVisualBitmasks();
        QueueRedraw();
        EmitAutoTileInfo();
    }

    private void UpdateAutoTileTooltip(Vector2 localPos)
    {
        if (_autoTileDef == null)
        {
            TooltipText = "";
            return;
        }

        var format = GetEffectiveFormat();
        var bitmask = GetHoveredAutoTileBitmask(localPos, format);
        TooltipText = bitmask >= 0
            ? BuildAutoTileTooltip(bitmask, format)
            : format == "blob47" ? "Empty cell" : "";
    }

    private int GetHoveredAutoTileBitmask(Vector2 localPos, string format)
    {
        return format == "blob47"
            ? GetBlobHoveredBitmask(localPos)
            : GetDualGridHoveredBitmask(localPos);
    }

    private int GetBlobHoveredBitmask(Vector2 localPos)
    {
        var scaledTileSize = GetScaledTileSize();
        var halfTile = scaledTileSize / 2;
        var dataCol = (int)((localPos.X - halfTile.X) / scaledTileSize.X);
        var dataRow = (int)((localPos.Y - halfTile.Y) / scaledTileSize.Y);
        if (dataRow < 0 || dataRow >= DataGridRows || dataCol < 0 || dataCol >= DataGridCols)
            return -1;

        return _visualBitmasks[dataRow, dataCol];
    }

    private int GetDualGridHoveredBitmask(Vector2 localPos)
    {
        var scaledTileSize = GetScaledTileSize();
        var vx = (int)(localPos.X / scaledTileSize.X);
        var vy = (int)(localPos.Y / scaledTileSize.Y);
        if (vy < 0 || vy >= VisualGridRows || vx < 0 || vx >= VisualGridCols)
            return -1;

        return _visualBitmasks[vy, vx];
    }

    private Vector2 GetScaledTileSize()
    {
        return new Vector2(_tileSize.X, _tileSize.Y) * _scale;
    }

    private string BuildAutoTileTooltip(int bitmask, string format)
    {
        var atlasCoords = _autoTileDef!.GetAutoTileCoords(bitmask);
        var variantDef = _autoTileDef.GetVariantDefinition(bitmask);
        var tooltip = $"Bitmask: {bitmask}\n";
        tooltip += $"Atlas: ({atlasCoords.X}, {atlasCoords.Y})\n";

        if (variantDef.HasValue)
            tooltip += BuildVariantTooltip(variantDef.Value);

        tooltip += $"Format: {format}\n";
        tooltip += HasVariantForBitmask(bitmask) ? "Status: Valid" : "Status: Using default";
        return tooltip;
    }

    private static string BuildVariantTooltip(VariantDefinition variant)
    {
        var tooltip = "";
        if (variant.Size.X > 1 || variant.Size.Y > 1)
            tooltip += $"Size: {variant.Size.X}x{variant.Size.Y}\n";
        if (variant.Offset.X != 0 || variant.Offset.Y != 0)
            tooltip += $"Offset: ({variant.Offset.X}, {variant.Offset.Y})\n";
        return tooltip;
    }

    private bool HasVariantForBitmask(int bitmask)
    {
        if (_autoTileDef?.AutoTileVariants == null)
            return false;

        var variantIndex = GetVariantIndex(bitmask);
        return variantIndex >= 0
            && variantIndex < _autoTileDef.AutoTileVariants.Length
            && _autoTileDef.AutoTileVariants[variantIndex].HasValue;
    }

    private int GetVariantIndex(int bitmask)
    {
        return GetEffectiveFormat() == "blob47"
            ? NeighborBitmask8.GetBlobIndex(bitmask)
            : bitmask;
    }

    private void UpdateTooltip(Vector2 localPos)
    {
        if (_mapData == null)
        {
            TooltipText = "";
            return;
        }

        var bounds = _mapData.GetBounds();
        var scaledTileSize = GetScaledTileSize();
        var tileX = bounds.Min.X + (int)(localPos.X / scaledTileSize.X);
        var tileY = bounds.Min.Y + (int)(localPos.Y / scaledTileSize.Y);
        var resolution = _mapData.GetTileAt(tileX, tileY);
        TooltipText = resolution == null
            ? $"Position: ({tileX}, {tileY})\nEmpty"
            : BuildMapTooltip(tileX, tileY, resolution);
    }

    private static string BuildMapTooltip(int tileX, int tileY, TmxTileResolution resolution)
    {
        var tooltip = $"Position: ({tileX}, {tileY})\n";
        tooltip += $"Global ID: {resolution.GlobalTileId}\n";
        tooltip += $"Local ID: {resolution.LocalTileId}\n";
        tooltip += $"Atlas: ({resolution.AtlasCoords.X}, {resolution.AtlasCoords.Y})\n";
        tooltip += $"Tileset: {Path.GetFileName(resolution.Tileset.TsxPath)}";

        if (resolution.TileDefinition != null)
        {
            tooltip += $"\n\nTile: {resolution.TileDefinition.Id}";
            if (!string.IsNullOrEmpty(resolution.TileDefinition.AutoTileFormatName))
                tooltip += $"\nFormat: {resolution.TileDefinition.AutoTileFormatName}";
        }

        return tooltip;
    }
}
#endif
