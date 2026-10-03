#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Panel showing auto-tile variants in an interactive dual-grid preview.
/// </summary>
[Tool]
public partial class AutoTilePreviewPanel : ScrollContainer
{
    private readonly TileEditorService? _service;
    private OptionButton? _tileSelector;
    private OptionButton? _baseTileSelector;
    private OptionButton? _formatOverrideDropdown;
    private HSlider? _scaleSlider;
    private Label? _scaleLabel;
    private Label? _infoLabel;
    private CheckBox? _showDataGridCheckbox;
    private DualGridAutoTilePreview? _mapPreview;
    private string? _selectedTileId;
    private string? _selectedBaseTileId;
    private string? _formatOverride;

    public AutoTilePreviewPanel() { }

    public AutoTilePreviewPanel(TileEditorService service)
    {
        _service = service;
    }
}
#endif
