#if TOOLS
using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class VariantCell
{
    private void BuildUI()
    {
        AddShapePreview();
        AddBitmaskLabel();
        AddAtlasButton();
        AddSizeControls();
        AddOffsetControls();
        AddVariantPreview();
        AddValidationLabel();
    }

    private void AddShapePreview()
    {
        _shapePreview = new TileShapePreview
        {
            CustomMinimumSize = new Vector2(30, 30),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        var previewFormat = _format?.BitmaskType switch
        {
            BitmaskType.Corner4 => TileShapePreview.Format.Corner16,
            BitmaskType.Edge4 => TileShapePreview.Format.Edge16,
            BitmaskType.Full8 => TileShapePreview.Format.Blob47,
            _ => TileShapePreview.Format.Corner16
        };
        _shapePreview.SetMask(_bitmask, previewFormat);
        AddChild(_shapePreview);
    }

    private void AddBitmaskLabel()
    {
        _bitmaskLabel = new Label
        {
            Text = $"Bitmask: {_bitmask}",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _bitmaskLabel.AddThemeFontSizeOverride("font_size", 10);
        AddChild(_bitmaskLabel);
    }

    private void AddAtlasButton()
    {
        _atlasButton = new Button
        {
            Text = "Atlas: (0, 0)",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Click to select atlas coordinates"
        };
        _atlasButton.Pressed += OnAtlasButtonPressed;
        AddChild(_atlasButton);
    }

    private void AddSizeControls()
    {
        var sizeRow = new HBoxContainer();
        sizeRow.AddChild(new Label { Text = "Size:", CustomMinimumSize = new Vector2(35, 0) });
        _sizeXSpin = CreateSizeSpinBox();
        _sizeXSpin.ValueChanged += OnSizeChanged;
        sizeRow.AddChild(_sizeXSpin);
        sizeRow.AddChild(new Label { Text = "x" });
        _sizeYSpin = CreateSizeSpinBox();
        _sizeYSpin.ValueChanged += OnSizeChanged;
        sizeRow.AddChild(_sizeYSpin);
        AddChild(sizeRow);
    }

    private static SpinBox CreateSizeSpinBox()
    {
        return new SpinBox
        {
            MinValue = 1,
            MaxValue = 8,
            Value = 1,
            CustomMinimumSize = new Vector2(50, 0)
        };
    }

    private void AddOffsetControls()
    {
        var offsetRow = new HBoxContainer();
        offsetRow.AddChild(new Label { Text = "Offset:", CustomMinimumSize = new Vector2(35, 0) });
        _offsetXSpin = CreateOffsetSpinBox();
        _offsetXSpin.ValueChanged += OnOffsetChanged;
        offsetRow.AddChild(_offsetXSpin);
        offsetRow.AddChild(new Label { Text = "," });
        _offsetYSpin = CreateOffsetSpinBox();
        _offsetYSpin.ValueChanged += OnOffsetChanged;
        offsetRow.AddChild(_offsetYSpin);
        AddChild(offsetRow);
    }

    private static SpinBox CreateOffsetSpinBox()
    {
        return new SpinBox
        {
            MinValue = -10,
            MaxValue = 10,
            Value = 0,
            CustomMinimumSize = new Vector2(50, 0)
        };
    }

    private void AddVariantPreview()
    {
        _variantPreview = new TextureRect
        {
            CustomMinimumSize = new Vector2(48, 48),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Nearest
        };
        AddChild(_variantPreview);
    }

    private void AddValidationLabel()
    {
        _validationLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1f, 0.4f, 0.4f)
        };
        _validationLabel.AddThemeFontSizeOverride("font_size", 9);
        AddChild(_validationLabel);
    }
}
#endif
