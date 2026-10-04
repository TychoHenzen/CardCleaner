#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TilePropertiesPanel
{
    private void RebuildVariantGrid(int variantCount, string formatName)
    {
        if (_variantGridContainer == null)
            return;

        ClearVariantGrid();
        _currentVariantCount = variantCount;
        _variantThumbnails = new TextureRect?[variantCount];
        _variantShapePreviews = new TileShapePreview?[variantCount];
        var format = _service!.GetFormatDefinition(formatName);
        var bitmaskValues = GetBitmaskValues(format, formatName, variantCount);
        _slotIndexToBitmask = bitmaskValues;
        _variantGrid = CreateVariantGrid();

        for (var index = 0; index < variantCount; index++)
        {
            var maskValue = bitmaskValues[index];
            _variantGrid.AddChild(CreateVariantSlot(index, maskValue, format));
        }

        RefreshVariantThumbnails();
    }

    private void ClearVariantGrid()
    {
        foreach (var child in _variantGridContainer!.GetChildren())
            child.QueueFree();
    }

    private static IReadOnlyList<int> GetBitmaskValues(
        AutoTileFormatDefinition? format,
        string formatName,
        int variantCount)
    {
        if (format != null)
            return format.AllowedBitmasks.OrderBy(bitmask => bitmask).ToArray();
        if (formatName == "blob47")
            return NeighborBitmask8.GetValid47Masks();
        return Enumerable.Range(0, variantCount).ToArray();
    }

    private GridContainer CreateVariantGrid()
    {
        var grid = new GridContainer
        {
            Columns = 4,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);
        _variantGridContainer!.AddChild(grid);
        return grid;
    }

    private VBoxContainer CreateVariantSlot(
        int index,
        int maskValue,
        AutoTileFormatDefinition? format)
    {
        var variantSize = GetVariantSize(format, maskValue);
        var slot = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(80, 130),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        slot.AddChild(CreateVariantLabel(index, maskValue, format));
        slot.AddChild(CreateVariantShapePreview(index, maskValue, format));
        slot.AddChild(CreateVariantThumbnail(index, variantSize));
        slot.CustomMinimumSize = new Vector2(
            Math.Max(80, 64 * variantSize.X + 16),
            66 + 64 * variantSize.Y);
        slot.AddChild(CreateVariantButtons(index));
        return slot;
    }

    private Label CreateVariantLabel(
        int index,
        int maskValue,
        AutoTileFormatDefinition? format)
    {
        var labelText = format?.BitmaskType switch
        {
            BitmaskType.Full8 => $"{maskValue}: {GetBlobMaskLabel(maskValue)}",
            BitmaskType.Edge4 => maskValue < EdgeBitmaskLabels.Length
                ? $"{maskValue}: {EdgeBitmaskLabels[maskValue]}"
                : $"{maskValue}",
            _ => maskValue < CornerBitmaskLabels.Length
                ? $"{maskValue}: {CornerBitmaskLabels[maskValue]}"
                : $"{maskValue}"
        };
        var label = new Label
        {
            Text = labelText,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.AddThemeFontSizeOverride("font_size", 8);
        return label;
    }

    private TileShapePreview CreateVariantShapePreview(
        int index,
        int maskValue,
        AutoTileFormatDefinition? format)
    {
        var preview = new TileShapePreview
        {
            CustomMinimumSize = new Vector2(24, 24),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        var previewFormat = format?.BitmaskType switch
        {
            BitmaskType.Full8 => TileShapePreview.Format.Blob47,
            BitmaskType.Edge4 => TileShapePreview.Format.Edge16,
            _ => TileShapePreview.Format.Corner16
        };
        preview.SetMask(maskValue, previewFormat);
        _variantShapePreviews![index] = preview;
        return preview;
    }

    private PanelContainer CreateVariantThumbnail(int index, Vector2I variantSize)
    {
        var thumbnailSize = new Vector2(64 * variantSize.X, 64 * variantSize.Y);
        var panel = new PanelContainer
        {
            CustomMinimumSize = thumbnailSize,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        var thumbnail = new TextureRect
        {
            CustomMinimumSize = thumbnailSize,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Nearest
        };
        panel.AddChild(thumbnail);
        _variantThumbnails![index] = thumbnail;
        return panel;
    }

    private HBoxContainer CreateVariantButtons(int index)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        var selectButton = new Button
        {
            Text = "Set",
            CustomMinimumSize = new Vector2(28, 0)
        };
        selectButton.AddThemeFontSizeOverride("font_size", 10);
        selectButton.Pressed += () => OpenVariantPickerDialog(index);
        row.AddChild(selectButton);

        var clearButton = new Button
        {
            Text = "X",
            CustomMinimumSize = new Vector2(20, 0),
            TooltipText = "Clear variant"
        };
        clearButton.AddThemeFontSizeOverride("font_size", 10);
        clearButton.Pressed += () => ClearVariant(index);
        row.AddChild(clearButton);
        return row;
    }

    private static Vector2I GetVariantSize(
        AutoTileFormatDefinition? format,
        int bitmask)
    {
        if (format == null)
            return Vector2I.One;

        var variant = format.GetVariant(bitmask);
        return variant.HasValue
            && (variant.Value.Size.X > 0 || variant.Value.Size.Y > 0)
            ? variant.Value.Size
            : Vector2I.One;
    }

    private void RefreshVariantThumbnails()
    {
        if (_currentTile == null)
            return;

        for (var index = 0; index < _currentVariantCount; index++)
            UpdateVariantThumbnail(index);
    }
}
#endif
