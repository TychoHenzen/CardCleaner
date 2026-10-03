#if TOOLS
using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

[Tool]
public partial class VariantCell : VBoxContainer
{
    [Signal]
    public delegate void VariantChangedEventHandler(int bitmask);

    private readonly TileEditorService? _service;
    private readonly int _bitmask;
    private readonly EditableTile? _tile;
    private readonly AutoTileFormatDefinition? _format;

    private TileShapePreview? _shapePreview;
    private Label? _bitmaskLabel;
    private Button? _atlasButton;
    private SpinBox? _sizeXSpin;
    private SpinBox? _sizeYSpin;
    private SpinBox? _offsetXSpin;
    private SpinBox? _offsetYSpin;
    private TextureRect? _variantPreview;
    private Label? _validationLabel;

    private EditableVariantDefinition _currentDefinition = new();
    private bool _isUpdating;
    private bool _isReadOnly;

    public VariantCell() { }

    public VariantCell(
        TileEditorService service,
        int bitmask,
        EditableTile tile,
        AutoTileFormatDefinition format)
    {
        _service = service;
        _bitmask = bitmask;
        _tile = tile;
        _format = format;

        CustomMinimumSize = new Vector2(140, 200);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

        BuildUI();
        LoadFromTile();
    }
}
#endif
