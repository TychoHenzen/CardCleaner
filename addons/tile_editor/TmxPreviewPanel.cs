#if TOOLS
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Services.TilesetLoading;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Panel showing a preview of tiles as they resolve in an actual TMX file.
/// </summary>
[Tool]
public partial class TmxPreviewPanel : VBoxContainer
{
    private const string DefaultTmxDir = "res://Data/Tiled";

    private readonly TileEditorService? _service;
    private LineEdit? _tmxPathEdit;
    private Button? _browseButton;
    private Button? _reloadButton;
    private OptionButton? _baseTileSelector;
    private OptionButton? _autoTileSelector;
    private OptionButton? _formatOverrideDropdown;
    private CheckBox? _showDataGridCheckbox;
    private HSlider? _scaleSlider;
    private Label? _scaleLabel;
    private Label? _infoLabel;
    private TmxPreviewControl? _previewControl;
    private string? _currentTmxPath;
    private TileDefinition? _selectedBaseTile;
    private TmxTilesetReference? _selectedBaseTileTileset;
    private TileDefinition? _selectedAutoTile;
    private TmxTilesetReference? _selectedAutoTileTileset;
    private string? _formatOverride;
    private TmxMapData? _currentMapData;
    private readonly List<TmxPreviewTileOption> _availableAutoTiles = new();
    private readonly List<TmxPreviewTileOption> _availableBaseTiles = new();

    public TmxPreviewPanel() { }

    public TmxPreviewPanel(TileEditorService service)
    {
        _service = service;
    }
}
#endif
