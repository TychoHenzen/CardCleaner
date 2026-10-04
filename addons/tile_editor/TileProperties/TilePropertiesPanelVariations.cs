#if TOOLS
using System;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TilePropertiesPanel
{
    private void OnVariationModeChanged(long index)
    {
        if (_isUpdating || _currentTile == null)
            return;

        _currentTile.VariationMode = index == 1 ? "pergeneration" : "perinstance";
        _service!.UpdateTile(_currentTile);
    }

    private void RebuildVariationsList()
    {
        if (_variationsListContainer == null || _currentTile == null)
            return;

        foreach (var child in _variationsListContainer.GetChildren())
            child.QueueFree();
        AddBaseVariationRow();
        AddVariationRows();
    }

    private void AddBaseVariationRow()
    {
        var row = new HBoxContainer();
        var tile = _currentTile!;
        row.AddChild(CreateVariationThumbnail(new Vector2I(tile.AtlasX, tile.AtlasY)));
        row.AddChild(new Label
        {
            Text = $"Base: ({tile.AtlasX}, {tile.AtlasY})",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });
        _variationsListContainer!.AddChild(row);
    }

    private void AddVariationRows()
    {
        if (_currentTile!.Variations == null)
            return;

        for (var index = 0; index < _currentTile.Variations.Length; index++)
        {
            var row = CreateVariationRow(index, _currentTile.Variations[index]);
            _variationsListContainer!.AddChild(row);
        }
    }

    private HBoxContainer CreateVariationRow(int index, Vector2I coords)
    {
        var row = new HBoxContainer();
        row.AddChild(CreateVariationThumbnail(coords));
        row.AddChild(new Label
        {
            Text = $"Var {index + 1}: ({coords.X}, {coords.Y})",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });
        var removeButton = new Button
        {
            Text = "X",
            CustomMinimumSize = new Vector2(24, 0),
            TooltipText = "Remove variation"
        };
        var capturedIndex = index;
        removeButton.Pressed += () => RemoveVariation(capturedIndex);
        row.AddChild(removeButton);
        return row;
    }

    private PanelContainer CreateVariationThumbnail(Vector2I coords)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(32, 32)
        };
        var textureRect = new TextureRect
        {
            CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Nearest
        };
        SetThumbnailTexture(textureRect, coords);
        panel.AddChild(textureRect);
        return panel;
    }

    private void SetThumbnailTexture(TextureRect textureRect, Vector2I coords)
    {
        if (_currentTile == null)
            return;

        var texture = _service!.GetTileTexture(_currentTile);
        if (texture == null)
            return;

        var baseTileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);
        var actualTileSize = new Vector2I(
            (int)(baseTileSize.X / _currentTile.SourceScale),
            (int)(baseTileSize.Y / _currentTile.SourceScale));
        textureRect.Texture = new AtlasTexture
        {
            Atlas = texture,
            Region = new Rect2I(coords * actualTileSize, actualTileSize)
        };
    }

    private void OpenAddVariationDialog()
    {
        if (_currentTile == null)
            return;

        EnsureVariationPickerDialog();
        var source = _service!.GetAtlasSource(_currentTile.SourceId);
        if (source != null && _variationPicker != null)
        {
            var tileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);
            _variationPicker.SetSource(
                source,
                tileSize,
                _currentTile.SourceId,
                _currentTile.SourceScale);
            _variationPicker.SelectedCoords =
                new Vector2I(_currentTile.AtlasX, _currentTile.AtlasY);
        }

        _variationPickerDialog!.Popup();
    }

    private void EnsureVariationPickerDialog()
    {
        if (_variationPickerDialog != null)
            return;

        _variationPickerDialog = new AcceptDialog
        {
            Title = "Add Tile Variation",
            InitialPosition = Window.WindowInitialPosition.CenterMainWindowScreen,
            Size = GetLargeDialogSize(),
            OkButtonText = "Add"
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
        _variationPicker = new TilesetAtlasPicker();
        pickerScroll.AddChild(_variationPicker);
        dialogVBox.AddChild(pickerScroll);
        _variationPickerDialog.AddChild(dialogVBox);
        AddChild(_variationPickerDialog);
        _variationPickerDialog.Confirmed += OnAddVariationConfirmed;
    }

    private void OnAddVariationConfirmed()
    {
        if (_currentTile == null || _variationPicker == null)
            return;

        var newCoords = _variationPicker.SelectedCoords;
        if (newCoords == new Vector2I(_currentTile.AtlasX, _currentTile.AtlasY))
            return;
        if (_currentTile.Variations != null)
        {
            foreach (var variation in _currentTile.Variations)
            {
                if (variation.X == newCoords.X && variation.Y == newCoords.Y)
                    return;
            }
        }

        var existing = _currentTile.Variations ?? Array.Empty<Vector2I>();
        var newVariations = new Vector2I[existing.Length + 1];
        Array.Copy(existing, newVariations, existing.Length);
        newVariations[existing.Length] = newCoords;
        _currentTile.Variations = newVariations;
        RebuildVariationsList();
        _service!.UpdateTile(_currentTile);
    }

    private void RemoveVariation(int index)
    {
        if (_currentTile?.Variations == null || index >= _currentTile.Variations.Length)
            return;

        var oldVariations = _currentTile.Variations;
        if (oldVariations.Length == 1)
        {
            _currentTile.Variations = null;
        }
        else
        {
            var newVariations = new Vector2I[oldVariations.Length - 1];
            var newIndex = 0;
            for (var i = 0; i < oldVariations.Length; i++)
            {
                if (i != index)
                    newVariations[newIndex++] = oldVariations[i];
            }
            _currentTile.Variations = newVariations;
        }

        RebuildVariationsList();
        _service!.UpdateTile(_currentTile);
    }
}
#endif
