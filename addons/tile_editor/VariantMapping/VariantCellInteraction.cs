#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class VariantCell
{
    private void OnAtlasButtonPressed()
    {
        if (_isReadOnly)
            return;

        var dialog = new AcceptDialog
        {
            Title = $"Set Atlas Coords (Bitmask {_bitmask})",
            Size = new Vector2I(300, 150)
        };

        var vbox = new VBoxContainer();
        var xSpin = AddCoordinateRow(vbox, "X:", _currentDefinition.AtlasX);
        var ySpin = AddCoordinateRow(vbox, "Y:", _currentDefinition.AtlasY);

        dialog.AddChild(vbox);
        dialog.Confirmed += () =>
        {
            _currentDefinition.AtlasX = (int)xSpin.Value;
            _currentDefinition.AtlasY = (int)ySpin.Value;
            UpdateUIFromDefinition();
            EmitVariantChanged();
            dialog.QueueFree();
        };
        dialog.Canceled += () => dialog.QueueFree();

        GetTree().Root.AddChild(dialog);
        dialog.PopupCentered();
    }

    private static SpinBox AddCoordinateRow(VBoxContainer parent, string labelText, int value)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(30, 0) });
        var spin = new SpinBox
        {
            MinValue = 0,
            MaxValue = 99,
            Value = value,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddChild(spin);
        parent.AddChild(row);
        return spin;
    }

    private void OnSizeChanged(double value)
    {
        if (_isUpdating || _isReadOnly)
            return;

        _currentDefinition.SizeX = (int)_sizeXSpin!.Value;
        _currentDefinition.SizeY = (int)_sizeYSpin!.Value;
        UpdatePreview();
        Validate();
        EmitVariantChanged();
    }

    private void OnOffsetChanged(double value)
    {
        if (_isUpdating || _isReadOnly)
            return;

        _currentDefinition.OffsetX = (int)_offsetXSpin!.Value;
        _currentDefinition.OffsetY = (int)_offsetYSpin!.Value;
        EmitVariantChanged();
    }

    private void EmitVariantChanged()
    {
        EmitSignal(SignalName.VariantChanged, _bitmask);
    }

    public void SetReadOnly(bool readOnly)
    {
        _isReadOnly = readOnly;
        _atlasButton!.Disabled = readOnly;
        _sizeXSpin!.Editable = !readOnly;
        _sizeYSpin!.Editable = !readOnly;
        _offsetXSpin!.Editable = !readOnly;
        _offsetYSpin!.Editable = !readOnly;
    }

    public EditableVariantDefinition GetVariantDefinition()
    {
        return _currentDefinition.Clone();
    }

    public void SetVariantDefinition(EditableVariantDefinition definition)
    {
        _currentDefinition = definition.Clone();
        UpdateUIFromDefinition();
    }
}
#endif
