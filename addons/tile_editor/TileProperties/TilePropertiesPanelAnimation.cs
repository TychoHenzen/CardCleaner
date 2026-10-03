#if TOOLS
using System;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TilePropertiesPanel
{
    private void OnAnimationDurationChanged(double value)
    {
        if (_isUpdating || _currentTile == null)
            return;

        _currentTile.AnimationFrameDuration = (float)value;
        _service!.UpdateTile(_currentTile);
    }

    private void RebuildAnimationFramesList()
    {
        if (_animationFramesContainer == null || _currentTile == null)
            return;

        foreach (var child in _animationFramesContainer.GetChildren())
            child.QueueFree();
        AddBaseAnimationFrame();
        AddAnimationFrames();
    }

    private void AddBaseAnimationFrame()
    {
        var tile = _currentTile!;
        var row = new HBoxContainer();
        row.AddChild(CreateVariationThumbnail(new Vector2I(tile.AtlasX, tile.AtlasY)));
        row.AddChild(new Label
        {
            Text = $"Frame 0 (Base): ({tile.AtlasX}, {tile.AtlasY})",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });
        _animationFramesContainer!.AddChild(row);
    }

    private void AddAnimationFrames()
    {
        if (_currentTile!.AnimationFrames == null)
            return;

        for (var index = 0; index < _currentTile.AnimationFrames.Length; index++)
        {
            var row = CreateAnimationFrameRow(index, _currentTile.AnimationFrames[index]);
            _animationFramesContainer!.AddChild(row);
        }
    }

    private HBoxContainer CreateAnimationFrameRow(int index, Vector2I coords)
    {
        var row = new HBoxContainer();
        row.AddChild(CreateVariationThumbnail(coords));
        row.AddChild(new Label
        {
            Text = $"Frame {index + 1}: ({coords.X}, {coords.Y})",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });
        var removeButton = new Button
        {
            Text = "X",
            CustomMinimumSize = new Vector2(24, 0),
            TooltipText = "Remove frame"
        };
        var capturedIndex = index;
        removeButton.Pressed += () => RemoveAnimationFrame(capturedIndex);
        row.AddChild(removeButton);
        return row;
    }

    private void OpenAddAnimationFrameDialog()
    {
        if (_currentTile == null)
            return;

        EnsureAnimationPickerDialog();
        var source = _service!.GetAtlasSource(_currentTile.SourceId);
        if (source != null && _animationFramePicker != null)
        {
            var tileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);
            _animationFramePicker.SetSource(
                source,
                tileSize,
                _currentTile.SourceId,
                _currentTile.SourceScale);
            _animationFramePicker.SelectedCoords =
                new Vector2I(_currentTile.AtlasX, _currentTile.AtlasY);
        }

        _animationFramePickerDialog!.Popup();
    }

    private void EnsureAnimationPickerDialog()
    {
        if (_animationFramePickerDialog != null)
            return;

        _animationFramePickerDialog = new AcceptDialog
        {
            Title = "Add Animation Frame",
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
        _animationFramePicker = new TilesetAtlasPicker();
        pickerScroll.AddChild(_animationFramePicker);
        dialogVBox.AddChild(pickerScroll);
        _animationFramePickerDialog.AddChild(dialogVBox);
        AddChild(_animationFramePickerDialog);
        _animationFramePickerDialog.Confirmed += OnAddAnimationFrameConfirmed;
    }

    private void OnAddAnimationFrameConfirmed()
    {
        if (_currentTile == null || _animationFramePicker == null)
            return;

        var existing = _currentTile.AnimationFrames ?? Array.Empty<Vector2I>();
        var newFrames = new Vector2I[existing.Length + 1];
        Array.Copy(existing, newFrames, existing.Length);
        newFrames[existing.Length] = _animationFramePicker.SelectedCoords;
        _currentTile.AnimationFrames = newFrames;
        RebuildAnimationFramesList();
        _service!.UpdateTile(_currentTile);
    }

    private void RemoveAnimationFrame(int index)
    {
        if (_currentTile?.AnimationFrames == null || index >= _currentTile.AnimationFrames.Length)
            return;

        var oldFrames = _currentTile.AnimationFrames;
        if (oldFrames.Length == 1)
        {
            _currentTile.AnimationFrames = null;
        }
        else
        {
            var newFrames = new Vector2I[oldFrames.Length - 1];
            var newIndex = 0;
            for (var i = 0; i < oldFrames.Length; i++)
            {
                if (i != index)
                    newFrames[newIndex++] = oldFrames[i];
            }
            _currentTile.AnimationFrames = newFrames;
        }

        RebuildAnimationFramesList();
        _service!.UpdateTile(_currentTile);
    }
}
#endif
