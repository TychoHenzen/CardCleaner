#if TOOLS
using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TilePropertiesPanel
{
    private void OpenSourcePickerDialog()
    {
        if (_currentTile == null)
            return;

        EnsureSourcePickerDialog();
        _selectedSourceIdForPicker = _currentTile.SourceId;
        UpdateSourcePanelHighlights();
        ResetSourcePickerFilter();
        _sourcePickerDialog!.Popup();
    }

    private void EnsureSourcePickerDialog()
    {
        if (_sourcePickerDialog != null)
            return;

        _sourcePickerDialog = new AcceptDialog
        {
            Title = "Select Atlas Source",
            InitialPosition = Window.WindowInitialPosition.CenterMainWindowScreen,
            Size = GetLargeDialogSize(),
            OkButtonText = "Select"
        };
        _sourcePickerDialog.AddChild(CreateSourcePickerContent());
        AddChild(_sourcePickerDialog);
        _sourcePickerDialog.Confirmed += OnSourcePickerConfirmed;
    }

    private VBoxContainer CreateSourcePickerContent()
    {
        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddChild(CreateSourcePickerZoomRow());
        content.AddChild(CreateSourcePickerFilterRow());
        content.AddChild(CreateSourcePickerScroll());
        return content;
    }

    private HBoxContainer CreateSourcePickerZoomRow()
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = "Zoom:" });
        _sourcePickerZoomSlider = new HSlider
        {
            MinValue = 0.5,
            MaxValue = 5.0,
            Step = 0.1,
            Value = 1.0,
            CustomMinimumSize = new Vector2(150, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _sourcePickerZoomSlider.ValueChanged += OnSourcePickerZoomChanged;
        row.AddChild(_sourcePickerZoomSlider);
        var zoomLabel = new Label { Text = "100%" };
        _sourcePickerZoomSlider.ValueChanged += value =>
            zoomLabel.Text = $"{(int)(value * 100)}%";
        row.AddChild(zoomLabel);
        return row;
    }

    private HBoxContainer CreateSourcePickerFilterRow()
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = "Filter:" });
        _sourcePickerFilterField = new LineEdit
        {
            PlaceholderText = "Filter sources...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClearButtonEnabled = true
        };
        _sourcePickerFilterField.TextChanged += OnSourcePickerFilterChanged;
        row.AddChild(_sourcePickerFilterField);
        return row;
    }

    private ScrollContainer CreateSourcePickerScroll()
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        scroll.GuiInput += OnSourcePickerScrollInput;
        scroll.AddChild(CreateSourcePickerGrid());
        return scroll;
    }

    private GridContainer CreateSourcePickerGrid()
    {
        var grid = new GridContainer
        {
            Columns = (int)(2f / _sourcePickerZoomSlider!.Value),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 12);

        _sourcePanelsBySourceId = new Dictionary<int, PanelContainer>();
        _sourceDisplayNames = new Dictionary<int, string>();
        _sourcePickerThumbnails = new List<TextureRect>();
        foreach (var sourceInfo in _service!.GetAvailableAtlasSources())
            grid.AddChild(CreateSourcePickerPanel(sourceInfo));
        return grid;
    }

    private PanelContainer CreateSourcePickerPanel(AtlasSourceInfo sourceInfo)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(280, 300),
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        var vbox = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        var thumbnail = CreateSourcePickerThumbnail(sourceInfo);
        vbox.AddChild(thumbnail);
        var label = new Label
        {
            Text = sourceInfo.DisplayName,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.AddThemeFontSizeOverride("font_size", 10);
        vbox.AddChild(label);
        panel.AddChild(vbox);

        var sourceId = sourceInfo.SourceId;
        panel.GuiInput += evt => OnSourcePanelClicked(sourceId, evt);
        _sourcePanelsBySourceId![sourceId] = panel;
        _sourceDisplayNames![sourceId] = sourceInfo.DisplayName;
        return panel;
    }

    private TextureRect CreateSourcePickerThumbnail(AtlasSourceInfo sourceInfo)
    {
        var thumbnail = new TextureRect
        {
            CustomMinimumSize = new Vector2(256, 256),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Nearest,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        if (sourceInfo.Source?.Texture != null)
            thumbnail.Texture = sourceInfo.Source.Texture;
        _sourcePickerThumbnails!.Add(thumbnail);
        return thumbnail;
    }

    private void ResetSourcePickerFilter()
    {
        if (_sourcePickerFilterField == null)
            return;

        _sourcePickerFilterField.Text = "";
        OnSourcePickerFilterChanged("");
    }

    private void OnSourcePanelClicked(int sourceId, InputEvent evt)
    {
        if (evt is InputEventMouseButton mouseButton
            && mouseButton.Pressed
            && mouseButton.ButtonIndex == MouseButton.Left)
        {
            _selectedSourceIdForPicker = sourceId;
            UpdateSourcePanelHighlights();
        }
    }

    private void UpdateSourcePanelHighlights()
    {
        if (_sourcePanelsBySourceId == null)
            return;

        foreach (var (sourceId, panel) in _sourcePanelsBySourceId)
        {
            panel.Modulate = sourceId == _selectedSourceIdForPicker
                ? new Color(1.2f, 1.2f, 1.0f)
                : Colors.White;
        }
    }

    private void OnSourcePickerZoomChanged(double value)
    {
        _sourcePickerZoom = (float)value;
        UpdateSourcePickerThumbnailSizes();
    }

    private void OnSourcePickerScrollInput(InputEvent evt)
    {
        if (evt is not InputEventMouseButton mouseButton
            || !mouseButton.Pressed
            || !mouseButton.CtrlPressed)
        {
            return;
        }

        if (mouseButton.ButtonIndex == MouseButton.WheelUp)
            _sourcePickerZoom = Mathf.Min(_sourcePickerZoom + 0.1f, 2.0f);
        else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
            _sourcePickerZoom = Mathf.Max(_sourcePickerZoom - 0.1f, 0.5f);
        else
            return;

        if (_sourcePickerZoomSlider != null)
            _sourcePickerZoomSlider.Value = _sourcePickerZoom;
        UpdateSourcePickerThumbnailSizes();
    }

    private void UpdateSourcePickerThumbnailSizes()
    {
        if (_sourcePickerThumbnails == null || _sourcePanelsBySourceId == null)
            return;

        var thumbnailSize = new Vector2(256f * _sourcePickerZoom, 256f * _sourcePickerZoom);
        var panelSize = new Vector2(280f * _sourcePickerZoom, 300f * _sourcePickerZoom);
        foreach (var thumbnail in _sourcePickerThumbnails)
            thumbnail.CustomMinimumSize = thumbnailSize;
        foreach (var panel in _sourcePanelsBySourceId.Values)
            panel.CustomMinimumSize = panelSize;
    }

    private void OnSourcePickerFilterChanged(string filterText)
    {
        if (_sourcePanelsBySourceId == null || _sourceDisplayNames == null)
            return;

        var filter = filterText.ToLowerInvariant();
        foreach (var (sourceId, panel) in _sourcePanelsBySourceId)
        {
            var displayName = _sourceDisplayNames.GetValueOrDefault(sourceId, "");
            panel.Visible = string.IsNullOrEmpty(filter)
                || displayName.Contains(filter, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void OnSourcePickerConfirmed()
    {
        if (_currentTile == null)
            return;

        _currentTile.SourceId = _selectedSourceIdForPicker;
        _sourceIdField!.Value = _selectedSourceIdForPicker;
        for (var i = 0; i < _sourceDropdown!.ItemCount; i++)
        {
            if (_sourceDropdown.GetItemId(i) == _selectedSourceIdForPicker)
            {
                _sourceDropdown.Selected = i;
                break;
            }
        }

        UpdateSourceButtonText();
        UpdateAtlasButtonAppearance();
        _service!.UpdateTile(_currentTile);
    }
}
#endif
