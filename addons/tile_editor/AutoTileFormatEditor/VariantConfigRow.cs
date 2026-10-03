#if TOOLS
using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// UI row for configuring a single variant's default Size in the format editor.
/// Only Size is configured at the format level (for tall tiles like platforms).
/// Offset and atlas coords are per-tile, configured in the tile properties panel.
/// </summary>
[Tool]
public partial class VariantConfigRow : HBoxContainer
{
    [Signal]
    public delegate void VariantChangedEventHandler(int bitmask);

    private readonly int _bitmask;
    private readonly EditableAutoTileFormat? _editableFormat;
    private readonly bool _isReadOnly;

    private TileShapePreview? _shapePreview;
    private Label? _bitmaskLabel;
    private SpinBox? _sizeXSpin;
    private SpinBox? _sizeYSpin;
    private bool _isUpdating;

    public VariantConfigRow() { }

    public VariantConfigRow(
        int bitmask,
        BitmaskType bitmaskType,
        EditableAutoTileFormat? editableFormat,
        bool isReadOnly)
    {
        _bitmask = bitmask;
        _editableFormat = editableFormat;
        _isReadOnly = isReadOnly;

        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(0, 36);

        AddShapePreview(bitmask, bitmaskType);
        AddBitmaskLabel(bitmask);
        AddSizeControls(isReadOnly);
        LoadFromFormat();
    }

    private void AddShapePreview(int bitmask, BitmaskType bitmaskType)
    {
        _shapePreview = new TileShapePreview
        {
            CustomMinimumSize = new Vector2(28, 28)
        };
        var previewFormat = bitmaskType switch
        {
            BitmaskType.Corner4 => TileShapePreview.Format.Corner16,
            BitmaskType.Edge4 => TileShapePreview.Format.Edge16,
            BitmaskType.Full8 => TileShapePreview.Format.Blob47,
            _ => TileShapePreview.Format.Corner16
        };
        _shapePreview.SetMask(bitmask, previewFormat);
        AddChild(_shapePreview);
    }

    private void AddBitmaskLabel(int bitmask)
    {
        _bitmaskLabel = new Label
        {
            Text = $"#{bitmask}",
            CustomMinimumSize = new Vector2(40, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _bitmaskLabel.AddThemeFontSizeOverride("font_size", 11);
        AddChild(_bitmaskLabel);
    }

    private void AddSizeControls(bool isReadOnly)
    {
        AddChild(new Label
        {
            Text = "Size:",
            CustomMinimumSize = new Vector2(35, 0),
            VerticalAlignment = VerticalAlignment.Center
        });

        _sizeXSpin = CreateSizeSpin(isReadOnly);
        _sizeXSpin.ValueChanged += OnValueChanged;
        AddChild(_sizeXSpin);
        AddChild(new Label { Text = "x", VerticalAlignment = VerticalAlignment.Center });

        _sizeYSpin = CreateSizeSpin(isReadOnly);
        _sizeYSpin.ValueChanged += OnValueChanged;
        AddChild(_sizeYSpin);
    }

    private static SpinBox CreateSizeSpin(bool isReadOnly)
    {
        return new SpinBox
        {
            MinValue = 1,
            MaxValue = 8,
            Value = 1,
            CustomMinimumSize = new Vector2(55, 0),
            Editable = !isReadOnly
        };
    }

    private void LoadFromFormat()
    {
        if (_editableFormat == null)
            return;

        _isUpdating = true;
        if (_editableFormat.VariantMappings.TryGetValue(_bitmask, out var variant))
        {
            _sizeXSpin!.Value = variant.SizeX;
            _sizeYSpin!.Value = variant.SizeY;
        }

        _isUpdating = false;
    }

    private void OnValueChanged(double value)
    {
        if (_isUpdating || _isReadOnly)
            return;

        EmitSignal(SignalName.VariantChanged, _bitmask);
    }

    public EditableFormatVariant GetVariant()
    {
        return new EditableFormatVariant
        {
            SizeX = (int)(_sizeXSpin?.Value ?? 1),
            SizeY = (int)(_sizeYSpin?.Value ?? 1),
            OffsetX = 0,
            OffsetY = 0
        };
    }
}
#endif
