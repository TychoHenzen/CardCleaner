#if TOOLS
using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Panel for editing tile properties.
/// </summary>
[Tool]
public partial class TilePropertiesPanel : ScrollContainer
{
    private readonly TileEditorService? _service;
    private string? _selectedTileId;
    private EditableTile? _currentTile;

    private OptionButton? _sourceDropdown;
    private Button? _sourcePickerButton;
    private Button? _atlasCoordButton;

    private AcceptDialog? _sourcePickerDialog;
    private int _selectedSourceIdForPicker;
    private Dictionary<int, PanelContainer>? _sourcePanelsBySourceId;
    private Dictionary<int, string>? _sourceDisplayNames;
    private List<TextureRect>? _sourcePickerThumbnails;
    private HSlider? _sourcePickerZoomSlider;
    private LineEdit? _sourcePickerFilterField;
    private float _sourcePickerZoom = 1.0f;
    private TextureRect? _atlasButtonThumbnail;
    private Label? _atlasButtonLabel;
    private AcceptDialog? _atlasPickerDialog;
    private TilesetAtlasPicker? _atlasDialogPicker;

    private LineEdit? _idField;
    private LineEdit? _nameField;
    private OptionButton? _tileModeDropdown;
    private OptionButton? _passabilityField;
    private SpinBox? _sourceIdField;
    private OptionButton? _layerField;
    private SpinBox? _elevationField;
    private CheckBox? _transparentField;
    private SpinBox? _sizeXField;
    private SpinBox? _sizeYField;
    private OptionButton? _sourceScaleDropdown;
    private VBoxContainer? _biomesContainer;
    private readonly Dictionary<string, CheckBox> _biomeCheckboxes = new();
    private TextEdit? _descriptionField;
    private Label? _validationLabel;

    private FoldoutContainer? _generalFoldout;

    private FoldoutContainer? _autoTileFoldout;
    private OptionButton? _autoTileFormatDropdown;
    private Label? _formatDescriptionLabel;
    private OptionButton? _innerTerrainDropdown;
    private OptionButton? _outerTerrainDropdown;
    private VBoxContainer? _variantGridContainer;
    private GridContainer? _variantGrid;
    private TextureRect?[]? _variantThumbnails;
    private TileShapePreview?[]? _variantShapePreviews;
    private AcceptDialog? _variantPickerDialog;
    private TilesetAtlasPicker? _variantPicker;
    private int _editingVariantIndex = -1;
    private int _currentVariantCount = 16;
    private IReadOnlyList<int>? _slotIndexToBitmask;

    private FoldoutContainer? _advancedVariantsFoldout;
    private VariantMappingEditor? _variantMappingEditor;
    private CheckBox? _useAdvancedVariantsCheckbox;

    private HBoxContainer? _decorationDensityRow;
    private SpinBox? _decorationDensityField;

    private FoldoutContainer? _variationsFoldout;
    private OptionButton? _variationModeDropdown;
    private VBoxContainer? _variationsListContainer;
    private AcceptDialog? _variationPickerDialog;
    private TilesetAtlasPicker? _variationPicker;

    private FoldoutContainer? _animationFoldout;
    private SpinBox? _animationFrameDurationField;
    private VBoxContainer? _animationFramesContainer;
    private AcceptDialog? _animationFramePickerDialog;
    private TilesetAtlasPicker? _animationFramePicker;

    private bool _isUpdating;

    public TilePropertiesPanel() { }

    public TilePropertiesPanel(TileEditorService service)
    {
        _service = service;
    }
}
#endif
