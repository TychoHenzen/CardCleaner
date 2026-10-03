#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Panel that displays tiles in a browsable grid with selection support
/// </summary>
[Tool]
public partial class TileAtlasPanel : Control
{
    private const int TileDisplaySize = 96; // 16px tiles at 3x scale
    private const int TileDisplayPadding = 4;
    private const int TilesPerRow = 8;

    private readonly TileEditorService? _service;
    private ScrollContainer? _scrollContainer;
    private GridContainer? _tileGrid;
    private LineEdit? _searchBox;
    private OptionButton? _biomeFilter;
    private OptionButton? _passabilityFilter;
    private OptionButton? _layerFilter;

    private string? _selectedTileId;
    private readonly Dictionary<string, TileButton> _tileButtons = new();

    // Toolbar buttons
    private Button? _duplicateButton;
    private Button? _deleteButton;

    // Dialogs
    private AcceptDialog? _duplicateDialog;
    private LineEdit? _duplicateIdField;
    private Label? _duplicateValidationLabel;
    private ConfirmationDialog? _deleteDialog;

    [Signal] public delegate void TileSelectedEventHandler(string tileId);

    // Required by Godot for [Tool] classes
    public TileAtlasPanel() { }

    public TileAtlasPanel(TileEditorService service)
    {
        _service = service;
        _service.TilesLoaded += OnTilesLoaded;
        _service.TileModified += OnTileModified;
        _service.TileAdded += OnTileAdded;
        _service.TileRemoved += OnTileRemoved;
    }

}

#endif
