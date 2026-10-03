#if TOOLS
using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TilePropertiesPanel
{
    private static readonly string[] CornerBitmaskLabels =
    {
        "None", "NE", "SE", "NE+SE", "SW", "NE+SW", "SE+SW", "NE+SE+SW",
        "NW", "NE+NW", "SE+NW", "NE+SE+NW", "SW+NW", "NE+SW+NW", "SE+SW+NW", "All"
    };

    private static readonly string[] EdgeBitmaskLabels =
    {
        "None", "N", "E", "N+E", "S", "N+S", "E+S", "N+E+S",
        "W", "N+W", "E+W", "N+E+W", "S+W", "N+S+W", "E+S+W", "All"
    };

    private static string GetBlobMaskLabel(int mask)
    {
        if (mask == 0)
            return "None";
        if (mask == 255)
            return "All";

        var parts = new List<string>();
        AddMaskLabel(parts, mask, 1, "N");
        AddMaskLabel(parts, mask, 2, "NE");
        AddMaskLabel(parts, mask, 4, "E");
        AddMaskLabel(parts, mask, 8, "SE");
        AddMaskLabel(parts, mask, 16, "S");
        AddMaskLabel(parts, mask, 32, "SW");
        AddMaskLabel(parts, mask, 64, "W");
        AddMaskLabel(parts, mask, 128, "NW");
        return string.Join("+", parts);
    }

    private static void AddMaskLabel(List<string> labels, int mask, int bit, string label)
    {
        if ((mask & bit) != 0)
            labels.Add(label);
    }

    private void OnTileModeChanged(long index)
    {
        if (_isUpdating || _currentTile == null)
            return;

        _currentTile.TileMode = index switch
        {
            1 => "pertilevariations",
            2 => "permapvariations",
            3 => "autotile",
            4 => "permapvariationautotile",
            5 => "animated",
            _ => "plain"
        };
        UpdateSectionVisibility();
        _service!.UpdateTile(_currentTile);
    }

    private void UpdateSectionVisibility()
    {
        if (_currentTile == null)
            return;

        var mode = _currentTile.TileMode?.ToLowerInvariant() ?? "plain";
        var showAutoTile = mode is "autotile" or "permapvariationautotile";
        var showVariations = mode is "pertilevariations" or "permapvariations"
            or "permapvariationautotile";
        var showAnimation = mode == "animated";
        _autoTileFoldout!.Visible = showAutoTile;
        _variationsFoldout!.Visible = showVariations;
        _animationFoldout!.Visible = showAnimation;
        UpdateVariationMode(mode, showVariations);
    }

    private void UpdateVariationMode(string mode, bool showVariations)
    {
        if (!showVariations || _variationModeDropdown == null)
            return;

        _isUpdating = true;
        if (mode is "permapvariations" or "permapvariationautotile")
        {
            _variationModeDropdown.Selected = 1;
            _currentTile!.VariationMode = "pergeneration";
        }
        else if (mode == "pertilevariations")
        {
            _variationModeDropdown.Selected = 0;
            _currentTile!.VariationMode = "perinstance";
        }
        _isUpdating = false;
    }

    private void OpenVariantPickerDialog(int slotIndex)
    {
        if (_currentTile == null)
            return;

        _editingVariantIndex = slotIndex;
        var bitmask = GetBitmaskForSlot(slotIndex);
        EnsureVariantArraySize(bitmask);
        EnsureVariantPickerDialog();
        ConfigureVariantPicker(bitmask);
        _variantPickerDialog!.Title = GetVariantPickerTitle(bitmask);
        _variantPickerDialog.Popup();
    }

    private void EnsureVariantPickerDialog()
    {
        if (_variantPickerDialog != null)
            return;

        _variantPickerDialog = new AcceptDialog
        {
            Title = "Select Atlas Variant",
            InitialPosition = Window.WindowInitialPosition.CenterMainWindowScreen,
            Size = GetLargeDialogSize(),
            OkButtonText = "Assign"
        };
        var dialogVBox = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        var pickerScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollMode.Auto,
            VerticalScrollMode = ScrollMode.Auto
        };
        _variantPicker = new TilesetAtlasPicker();
        pickerScroll.AddChild(_variantPicker);
        dialogVBox.AddChild(pickerScroll);
        _variantPickerDialog.AddChild(dialogVBox);
        AddChild(_variantPickerDialog);
        _variantPickerDialog.Confirmed += OnVariantPickerConfirmed;
        _variantPickerDialog.Canceled += OnVariantPickerCanceled;
    }

    private void ConfigureVariantPicker(int bitmask)
    {
        var source = _service!.GetAtlasSource(_currentTile!.SourceId);
        if (source == null || _variantPicker == null)
            return;

        var tileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);
        _variantPicker.SetSource(
            source,
            tileSize,
            _currentTile.SourceId,
            _currentTile.SourceScale);
        _variantPicker.SelectedSize = GetVariantSize(bitmask);
        _variantPicker.SelectedCoords = GetSelectedVariantCoordinates(bitmask);
    }

    private Vector2I GetSelectedVariantCoordinates(int bitmask)
    {
        if (_currentTile!.AutoTileVariants![bitmask].HasValue)
            return _currentTile.AutoTileVariants[bitmask]!.Value;

        return new Vector2I(_currentTile.AtlasX, _currentTile.AtlasY);
    }

    private string GetVariantPickerTitle(int bitmask)
    {
        var maskLabel = _currentVariantCount == 47
            ? GetBlobMaskLabel(bitmask)
            : bitmask < CornerBitmaskLabels.Length
                ? CornerBitmaskLabels[bitmask]
                : bitmask.ToString();
        return $"Select Variant for Bitmask {bitmask}: {maskLabel}";
    }

    private void OnVariantPickerConfirmed()
    {
        if (_editingVariantIndex < 0 || _currentTile == null || _variantPicker == null)
            return;

        var bitmask = GetBitmaskForSlot(_editingVariantIndex);
        EnsureVariantArraySize(bitmask);
        _currentTile.AutoTileVariants![bitmask] = _variantPicker.SelectedCoords;
        UpdateVariantThumbnail(_editingVariantIndex);
        _service!.UpdateTile(_currentTile);
        _editingVariantIndex = -1;
    }

    private void OnVariantPickerCanceled()
    {
        _editingVariantIndex = -1;
    }

    private void ClearVariant(int slotIndex)
    {
        if (_currentTile?.AutoTileVariants == null)
            return;

        var bitmask = GetBitmaskForSlot(slotIndex);
        if (bitmask < _currentTile.AutoTileVariants.Length)
            _currentTile.AutoTileVariants[bitmask] = null;
        UpdateVariantThumbnail(slotIndex);
        _service!.UpdateTile(_currentTile);
    }

    private void ClearAllVariants()
    {
        if (_currentTile == null)
            return;

        _currentTile.AutoTileVariants = null;
        for (var i = 0; i < _currentVariantCount; i++)
            UpdateVariantThumbnail(i);
        _service!.UpdateTile(_currentTile);
    }
}
#endif
