#if TOOLS
using System;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TilePropertiesPanel
{
    private void PopulateSourceDropdown()
    {
        _sourceDropdown!.Clear();
        foreach (var sourceInfo in _service!.GetAvailableAtlasSources())
            _sourceDropdown.AddItem(sourceInfo.DisplayName, sourceInfo.SourceId);
    }

    private void PopulateFormatDropdown()
    {
        if (_autoTileFormatDropdown == null)
            return;

        _autoTileFormatDropdown.Clear();
        var index = 0;
        foreach (var formatName in _service!.GetAvailableFormatNames())
        {
            var displayName = _service.GetFormatDisplayName(formatName);
            var format = _service.GetFormatDefinition(formatName);
            var variantCount = format?.AllowedBitmasks.Count ?? 16;
            _autoTileFormatDropdown.AddItem(
                $"{displayName} ({variantCount} variants)",
                index);
            _autoTileFormatDropdown.SetItemMetadata(index, formatName);
            index++;
        }
    }

    private void OnAutoTileFormatsLoaded()
    {
        RefreshFormatDropdown();
    }

    public void RefreshFormatDropdown()
    {
        if (_autoTileFormatDropdown == null)
            return;

        var currentFormat = _currentTile?.AutoTileFormat ?? "corner16";
        PopulateFormatDropdown();
        _autoTileFormatDropdown.Selected = GetFormatDropdownIndex(currentFormat);
    }

    private void UpdateFormatDescription(string formatName)
    {
        if (_formatDescriptionLabel == null)
            return;

        var format = _service!.GetFormatDefinition(formatName);
        if (format == null)
        {
            _formatDescriptionLabel.Text = "";
            return;
        }

        var typeDescription = format.BitmaskType switch
        {
            CardCleaner.Features.Worldgen.AutoTiling.BitmaskType.Corner4
                => "4-bit corner bitmask (NE, SE, SW, NW)",
            CardCleaner.Features.Worldgen.AutoTiling.BitmaskType.Edge4
                => "4-bit edge bitmask (N, E, S, W)",
            CardCleaner.Features.Worldgen.AutoTiling.BitmaskType.Full8
                => "8-bit full bitmask (8 neighbors)",
            _ => "Unknown bitmask type"
        };
        _formatDescriptionLabel.Text = format.IsBuiltIn
            ? $"Built-in format: {typeDescription}"
            : $"Custom format: {typeDescription}";
    }

    private int GetFormatDropdownIndex(string formatName)
    {
        if (_autoTileFormatDropdown == null)
            return 0;

        for (var i = 0; i < _autoTileFormatDropdown.ItemCount; i++)
        {
            var metadata = _autoTileFormatDropdown.GetItemMetadata(i).AsString();
            if (string.Equals(metadata, formatName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return 0;
    }

    private void OnSourceDropdownChanged(long index)
    {
        if (_isUpdating || _sourceDropdown == null || _currentTile == null)
            return;

        var sourceId = _sourceDropdown.GetItemId((int)index);
        _sourceIdField!.Value = sourceId;
        _currentTile.SourceId = sourceId;
        UpdateSourceButtonText();
        UpdateAtlasButtonAppearance();
        OnFieldChanged("");
    }

    private void UpdateSourceButtonText()
    {
        if (_sourcePickerButton == null || _currentTile == null)
            return;

        var sourceInfo = _service!.GetAvailableAtlasSources()
            .Find(source => source.SourceId == _currentTile.SourceId);
        _sourcePickerButton.Text = sourceInfo?.DisplayName
            ?? $"Source {_currentTile.SourceId}";
    }

    private void UpdateAtlasButtonAppearance()
    {
        if (_currentTile == null || _atlasButtonLabel == null || _atlasButtonThumbnail == null)
            return;

        _atlasButtonLabel.Text = $"({_currentTile.AtlasX}, {_currentTile.AtlasY})";
        var texture = _service!.GetTileTexture(_currentTile);
        if (texture == null)
        {
            _atlasButtonThumbnail.Texture = null;
            return;
        }

        var baseTileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);
        var actualTileSize = new Vector2I(
            (int)(baseTileSize.X / _currentTile.SourceScale),
            (int)(baseTileSize.Y / _currentTile.SourceScale));
        var region = new Rect2I(
            new Vector2I(_currentTile.AtlasX, _currentTile.AtlasY) * actualTileSize,
            actualTileSize);
        _atlasButtonThumbnail.Texture = new AtlasTexture
        {
            Atlas = texture,
            Region = region
        };
    }

    private void OpenAtlasPickerDialog()
    {
        if (_currentTile == null)
            return;

        if (_atlasPickerDialog == null)
            CreateAtlasPickerDialog();

        var source = _service!.GetAtlasSource(_currentTile.SourceId);
        if (source != null && _atlasDialogPicker != null)
        {
            var tileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);
            _atlasDialogPicker.SetSource(
                source,
                tileSize,
                _currentTile.SourceId,
                _currentTile.SourceScale);
            _atlasDialogPicker.SelectedCoords =
                new Vector2I(_currentTile.AtlasX, _currentTile.AtlasY);
            _atlasDialogPicker.SelectedSize =
                new Vector2I(_currentTile.SizeX, _currentTile.SizeY);
        }

        _atlasPickerDialog!.Popup();
    }

    private void CreateAtlasPickerDialog()
    {
        _atlasPickerDialog = new AcceptDialog
        {
            Title = "Select Atlas Coordinates",
            InitialPosition = Window.WindowInitialPosition.CenterMainWindowScreen,
            Size = GetLargeDialogSize(),
            OkButtonText = "Select"
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
        _atlasDialogPicker = new TilesetAtlasPicker();
        pickerScroll.AddChild(_atlasDialogPicker);
        dialogVBox.AddChild(pickerScroll);
        _atlasPickerDialog.AddChild(dialogVBox);
        AddChild(_atlasPickerDialog);
        _atlasPickerDialog.Confirmed += OnAtlasPickerConfirmed;
    }

    private void OnAtlasPickerConfirmed()
    {
        if (_currentTile == null || _atlasDialogPicker == null)
            return;

        _currentTile.AtlasX = _atlasDialogPicker.SelectedCoords.X;
        _currentTile.AtlasY = _atlasDialogPicker.SelectedCoords.Y;
        UpdateAtlasButtonAppearance();
        _service!.UpdateTile(_currentTile);
    }

    private static HBoxContainer CreateRow(string label)
    {
        var row = new HBoxContainer();
        var labelControl = new Label
        {
            Text = label,
            CustomMinimumSize = new Vector2(100, 0)
        };
        row.AddChild(labelControl);
        return row;
    }

    private Vector2I GetLargeDialogSize()
    {
        const float fillPercent = 0.70f;
        const int minWidth = 800;
        const int minHeight = 600;
        const int maxWidth = 1920;
        const int maxHeight = 1200;
        var screenSize = DisplayServer.ScreenGetSize();
        var width = Mathf.Clamp((int)(screenSize.X * fillPercent), minWidth, maxWidth);
        var height = Mathf.Clamp((int)(screenSize.Y * fillPercent), minHeight, maxHeight);
        return new Vector2I(width, height);
    }
}
#endif
