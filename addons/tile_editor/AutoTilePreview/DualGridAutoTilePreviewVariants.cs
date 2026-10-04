#if TOOLS
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class DualGridAutoTilePreview
{
    private VariantPreviewInfo GetVariantInfo(int bitmask)
    {
        var defaultCoords = new Vector2I(
            _overlayTile?.AtlasX ?? 0,
            _overlayTile?.AtlasY ?? 0);
        var defaultSize = Vector2I.One;
        var defaultOffset = Vector2I.Zero;
        if (_overlayTile == null)
            return new VariantPreviewInfo(defaultCoords, defaultSize, defaultOffset, false);

        var format = GetEffectiveFormat();
        var variantIndex = GetVariantIndex(format, bitmask);
        if (variantIndex < 0)
            return new VariantPreviewInfo(defaultCoords, defaultSize, defaultOffset, false);

        var formatDefinition = _service!.GetFormatDefinition(format);
        if (!IsVariantAllowed(formatDefinition, variantIndex))
            return new VariantPreviewInfo(defaultCoords, defaultSize, defaultOffset, false);

        var variantSize = GetFormatVariantSize(formatDefinition, variantIndex);
        var customInfo = GetCustomVariantInfo(variantIndex, variantSize);
        if (customInfo.HasValue)
            return customInfo.Value;

        var autoTileInfo = GetAutoTileVariantInfo(variantIndex, variantSize);
        if (autoTileInfo.HasValue)
            return autoTileInfo.Value;

        return new VariantPreviewInfo(defaultCoords, defaultSize, defaultOffset, false);
    }

    private static int GetVariantIndex(string format, int bitmask)
    {
        return format == "blob47"
            ? NeighborBitmask8.GetBlobIndex(bitmask)
            : bitmask;
    }

    private static bool IsVariantAllowed(
        CardCleaner.Features.Worldgen.AutoTiling.AutoTileFormatDefinition? format,
        int variantIndex)
    {
        return format == null || format.AllowedBitmasks.Contains(variantIndex);
    }

    private VariantPreviewInfo? GetCustomVariantInfo(int variantIndex, Vector2I variantSize)
    {
        if (_overlayTile!.CustomVariantDefinitions == null
            || !_overlayTile.CustomVariantDefinitions.TryGetValue(
                variantIndex,
                out var customDefinition))
        {
            return null;
        }

        return new VariantPreviewInfo(
            customDefinition.AtlasCoords,
            variantSize,
            customDefinition.Offset,
            true);
    }

    private VariantPreviewInfo? GetAutoTileVariantInfo(int variantIndex, Vector2I variantSize)
    {
        if (_overlayTile!.AutoTileVariants == null
            || variantIndex >= _overlayTile.AutoTileVariants.Length
            || !_overlayTile.AutoTileVariants[variantIndex].HasValue)
        {
            return null;
        }

        return new VariantPreviewInfo(
            _overlayTile.AutoTileVariants[variantIndex]!.Value,
            variantSize,
            Vector2I.Zero,
            true);
    }

    private static Vector2I GetFormatVariantSize(
        CardCleaner.Features.Worldgen.AutoTiling.AutoTileFormatDefinition? format,
        int variantIndex)
    {
        if (format == null)
            return Vector2I.One;

        var variant = format.GetVariant(variantIndex);
        return variant.HasValue
            && (variant.Value.Size.X > 0 || variant.Value.Size.Y > 0)
            ? variant.Value.Size
            : Vector2I.One;
    }

    private bool IsBitmaskAllowed(int bitmask)
    {
        var format = GetEffectiveFormat();
        var formatDefinition = _service!.GetFormatDefinition(format);
        if (formatDefinition == null)
        {
            if (format == "blob47")
            {
                var blobIndex = NeighborBitmask8.GetBlobIndex(bitmask);
                return blobIndex >= 0 && blobIndex < 47;
            }

            return bitmask >= 0 && bitmask < 16;
        }

        var variantIndex = format == "blob47"
            ? NeighborBitmask8.GetBlobIndex(bitmask)
            : bitmask;
        return formatDefinition.AllowedBitmasks.Contains(variantIndex);
    }

    private void UpdateTooltip(Vector2 localPos)
    {
        if (_overlayTile == null)
        {
            TooltipText = "";
            return;
        }

        var bitmask = GetHoveredBitmask(localPos);
        TooltipText = bitmask < 0 ? "" : BuildTooltip(bitmask);
    }

    private int GetHoveredBitmask(Vector2 localPos)
    {
        return GetEffectiveFormat() == "blob47"
            ? GetBlobHoveredBitmask(localPos)
            : GetDualGridHoveredBitmask(localPos);
    }

    private int GetBlobHoveredBitmask(Vector2 localPos)
    {
        var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;
        var halfTile = scaledTileSize / 2;
        var dataCol = (int)((localPos.X - halfTile.X) / scaledTileSize.X);
        var dataRow = (int)((localPos.Y - halfTile.Y) / scaledTileSize.Y);
        return IsDataCellInBounds(dataRow, dataCol)
            ? _visualBitmasks[dataRow, dataCol]
            : -1;
    }

    private int GetDualGridHoveredBitmask(Vector2 localPos)
    {
        var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;
        var col = (int)(localPos.X / scaledTileSize.X);
        var row = (int)(localPos.Y / scaledTileSize.Y);
        return row >= 0 && row < VisualGridRows && col >= 0 && col < VisualGridCols
            ? _visualBitmasks[row, col]
            : -1;
    }

    private static bool IsDataCellInBounds(int row, int col)
    {
        return row >= 0 && row < DataGridRows && col >= 0 && col < DataGridCols;
    }

    private string BuildTooltip(int bitmask)
    {
        var info = GetVariantInfo(bitmask);
        var tooltip = $"Bitmask: {bitmask}\n";
        tooltip += $"Atlas: ({info.AtlasCoords.X}, {info.AtlasCoords.Y})\n";
        if (info.Size.X > 1 || info.Size.Y > 1)
            tooltip += $"Size: {info.Size.X}x{info.Size.Y}\n";
        if (info.Offset.X != 0 || info.Offset.Y != 0)
            tooltip += $"Offset: ({info.Offset.X}, {info.Offset.Y})\n";
        tooltip += info.IsValid ? "Status: Valid" : "Status: Using default";
        return tooltip;
    }
}
#endif
