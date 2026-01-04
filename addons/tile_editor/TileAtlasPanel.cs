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

    public override void _Ready()
    {
        // Guard for Godot's parameterless constructor case
        if (_service == null) return;

        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(vbox);

        // Filter bar
        var filterBar = new HBoxContainer();
        vbox.AddChild(filterBar);

        filterBar.AddChild(new Label { Text = "Search:" });
        _searchBox = new LineEdit
        {
            PlaceholderText = "Filter tiles...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _searchBox.TextChanged += _ => RefreshTileDisplay();
        filterBar.AddChild(_searchBox);

        filterBar.AddChild(new Label { Text = "Biome:" });
        _biomeFilter = new OptionButton();
        _biomeFilter.AddItem("All", 0);
        _biomeFilter.AddItem("Plains", 1);
        _biomeFilter.AddItem("Forest", 2);
        _biomeFilter.AddItem("Desert", 3);
        _biomeFilter.AddItem("Tundra", 4);
        _biomeFilter.AddItem("Swamp", 5);
        _biomeFilter.AddItem("Mountains", 6);
        _biomeFilter.AddItem("Universal", 7);
        _biomeFilter.ItemSelected += _ => RefreshTileDisplay();
        filterBar.AddChild(_biomeFilter);

        filterBar.AddChild(new Label { Text = "Passability:" });
        _passabilityFilter = new OptionButton();
        _passabilityFilter.AddItem("All", 0);
        _passabilityFilter.AddItem("Passable", 1);
        _passabilityFilter.AddItem("Solid", 2);
        filterBar.AddChild(_passabilityFilter);
        _passabilityFilter.ItemSelected += _ => RefreshTileDisplay();

        filterBar.AddChild(new Label { Text = "Layer:" });
        _layerFilter = new OptionButton();
        _layerFilter.AddItem("All", 0);
        _layerFilter.AddItem("Terrain", 1);
        _layerFilter.AddItem("Decoration", 2);
        _layerFilter.AddItem("Structure", 3);
        _layerFilter.AddItem("Effects", 4);
        filterBar.AddChild(_layerFilter);
        _layerFilter.ItemSelected += _ => RefreshTileDisplay();

        // Action toolbar
        var toolbar = new HBoxContainer();
        vbox.AddChild(toolbar);

        _duplicateButton = new Button
        {
            Text = "Duplicate",
            TooltipText = "Duplicate selected tile with a new ID",
            Disabled = true
        };
        _duplicateButton.Pressed += OnDuplicatePressed;
        toolbar.AddChild(_duplicateButton);

        _deleteButton = new Button
        {
            Text = "Delete",
            TooltipText = "Delete selected tile",
            Disabled = true
        };
        _deleteButton.Pressed += OnDeletePressed;
        toolbar.AddChild(_deleteButton);

        toolbar.AddChild(new HSeparator { SizeFlagsHorizontal = SizeFlags.Expand });

        // Scroll container for tile grid
        _scrollContainer = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        vbox.AddChild(_scrollContainer);

        _tileGrid = new GridContainer
        {
            Columns = TilesPerRow,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _scrollContainer.AddChild(_tileGrid);
    }

    private void OnTilesLoaded()
    {
        RefreshTileDisplay();
    }

    private void OnTileModified(string tileId)
    {
        if (_tileButtons.TryGetValue(tileId, out var button))
        {
            var tile = _service.GetTile(tileId);
            if (tile != null)
            {
                button.UpdateTile(tile);
            }
        }
    }

    private void OnTileAdded(string tileId)
    {
        RefreshTileDisplay();
    }

    private void OnTileRemoved(string tileId)
    {
        RefreshTileDisplay();
    }

    private void RefreshTileDisplay()
    {
        if (_tileGrid == null) return;

        // Clear existing buttons
        foreach (var child in _tileGrid.GetChildren())
        {
            child.QueueFree();
        }
        _tileButtons.Clear();

        // Get filtered tiles
        var tiles = GetFilteredTiles().ToList();

        foreach (var tile in tiles)
        {
            var button = new TileButton(tile, TileDisplaySize, TileDisplayPadding, _service);
            button.Pressed += () => SelectTile(tile.Id);
            _tileGrid.AddChild(button);
            _tileButtons[tile.Id] = button;

            if (tile.Id == _selectedTileId)
            {
                button.SetSelected(true);
            }
        }
    }

    private IEnumerable<EditableTile> GetFilteredTiles()
    {
        var tiles = _service.AllTiles;

        // Search filter
        var search = _searchBox?.Text?.ToLowerInvariant() ?? "";
        if (!string.IsNullOrEmpty(search))
        {
            tiles = tiles.Where(t =>
                t.Id.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                t.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        // Biome filter
        var biomeIndex = _biomeFilter?.Selected ?? 0;
        if (biomeIndex > 0)
        {
            var biomes = new[] { "", "plains", "forest", "desert", "tundra", "swamp", "mountains", "" };
            if (biomeIndex == 7) // Universal
            {
                tiles = tiles.Where(t => t.Biomes.Count == 0);
            }
            else
            {
                var biome = biomes[biomeIndex];
                tiles = tiles.Where(t =>
                    t.Biomes.Count == 0 ||
                    t.Biomes.Contains(biome, System.StringComparer.OrdinalIgnoreCase));
            }
        }

        // Passability filter
        var passabilityIndex = _passabilityFilter?.Selected ?? 0;
        if (passabilityIndex > 0)
        {
            var passability = passabilityIndex == 1 ? "passable" : "solid";
            tiles = tiles.Where(t =>
                t.Passability.Equals(passability, StringComparison.OrdinalIgnoreCase));
        }

        // Layer filter
        var layerIndex = _layerFilter?.Selected ?? 0;
        if (layerIndex > 0)
        {
            var layers = new[] { "", "terrain", "decoration", "structure", "effects" };
            var layer = layers[layerIndex];
            tiles = tiles.Where(t =>
                (string.IsNullOrEmpty(t.Layer) ? "terrain" : t.Layer)
                    .Equals(layer, StringComparison.OrdinalIgnoreCase));
        }

        return tiles.OrderBy(t => t.Id);
    }

    private void SelectTile(string tileId)
    {
        // Deselect previous
        if (_selectedTileId != null && _tileButtons.TryGetValue(_selectedTileId, out var prevButton))
        {
            prevButton.SetSelected(false);
        }

        // Select new
        _selectedTileId = tileId;
        if (_tileButtons.TryGetValue(tileId, out var newButton))
        {
            newButton.SetSelected(true);
        }

        // Update toolbar button states
        UpdateToolbarState();

        EmitSignal(SignalName.TileSelected, tileId);
    }

    private void UpdateToolbarState()
    {
        var hasSelection = _selectedTileId != null;
        if (_duplicateButton != null) _duplicateButton.Disabled = !hasSelection;
        if (_deleteButton != null) _deleteButton.Disabled = !hasSelection;
    }

    public string? SelectedTileId => _selectedTileId;

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (_selectedTileId == null) return default;

        var tile = _service.GetTile(_selectedTileId);
        if (tile == null) return default;

        // Create drag preview
        var preview = new Label
        {
            Text = tile.Name,
            Modulate = new Color(1, 1, 1, 0.8f)
        };
        SetDragPreview(preview);

        return new Godot.Collections.Dictionary
        {
            { "type", "tile" },
            { "tile_id", _selectedTileId }
        };
    }

    private void OnDuplicatePressed()
    {
        if (_selectedTileId == null) return;
        var tile = _service.GetTile(_selectedTileId);
        if (tile == null) return;

        // Create duplicate dialog lazily
        if (_duplicateDialog == null)
        {
            _duplicateDialog = new AcceptDialog
            {
                Title = "Duplicate Tile",
                OkButtonText = "Duplicate",
                Size = new Vector2I(400, 150)
            };

            var vbox = new VBoxContainer();
            _duplicateDialog.AddChild(vbox);

            vbox.AddChild(new Label { Text = "Enter a new ID for the duplicated tile:" });

            _duplicateIdField = new LineEdit
            {
                PlaceholderText = "new_tile_id (snake_case)",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            _duplicateIdField.TextChanged += OnDuplicateIdChanged;
            vbox.AddChild(_duplicateIdField);

            _duplicateValidationLabel = new Label
            {
                Text = "",
                Modulate = new Color(1, 0.3f, 0.3f)
            };
            vbox.AddChild(_duplicateValidationLabel);

            _duplicateDialog.Confirmed += OnDuplicateConfirmed;
            AddChild(_duplicateDialog);
        }

        // Reset dialog state
        _duplicateIdField!.Text = tile.Id + "_copy";
        OnDuplicateIdChanged(_duplicateIdField.Text);
        _duplicateDialog.PopupCentered();
    }

    private void OnDuplicateIdChanged(string newId)
    {
        if (_duplicateValidationLabel == null || _duplicateDialog == null) return;

        var (valid, message) = ValidateTileId(newId);
        _duplicateValidationLabel.Text = valid ? "" : message;
        _duplicateDialog.GetOkButton().Disabled = !valid;
    }

    private (bool valid, string message) ValidateTileId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return (false, "ID is required");

        if (!System.Text.RegularExpressions.Regex.IsMatch(id, @"^[a-z][a-z0-9_]*$"))
            return (false, "ID must be snake_case starting with a letter");

        if (_service.GetTile(id) != null)
            return (false, $"Tile '{id}' already exists");

        return (true, "");
    }

    private void OnDuplicateConfirmed()
    {
        if (_selectedTileId == null || _duplicateIdField == null) return;

        var sourceTile = _service.GetTile(_selectedTileId);
        if (sourceTile == null) return;

        var newId = _duplicateIdField.Text.Trim();
        var (valid, _) = ValidateTileId(newId);
        if (!valid) return;

        // Clone and add
        var clone = sourceTile.Clone();
        clone.Id = newId;

        if (_service.AddTile(clone))
        {
            // Select the new tile after the grid refreshes
            CallDeferred(nameof(SelectTileDeferred), newId);
        }
    }

    private void SelectTileDeferred(string tileId)
    {
        SelectTile(tileId);
    }

    private void OnDeletePressed()
    {
        if (_selectedTileId == null) return;
        var tile = _service.GetTile(_selectedTileId);
        if (tile == null) return;

        // Create delete dialog lazily
        if (_deleteDialog == null)
        {
            _deleteDialog = new ConfirmationDialog
            {
                Title = "Delete Tile",
                OkButtonText = "Delete",
                Size = new Vector2I(400, 120)
            };
            _deleteDialog.Confirmed += OnDeleteConfirmed;
            AddChild(_deleteDialog);
        }

        _deleteDialog.DialogText = $"Are you sure you want to delete tile '{tile.Name}' ({tile.Id})?\n\nThis action cannot be undone.";
        _deleteDialog.PopupCentered();
    }

    private void OnDeleteConfirmed()
    {
        if (_selectedTileId == null) return;

        var idToDelete = _selectedTileId;
        _selectedTileId = null;
        UpdateToolbarState();

        _service.RemoveTile(idToDelete);
    }
}

/// <summary>
/// Button representing a single tile in the grid - uses direct drawing for proper scaling
/// </summary>
[Tool]
public partial class TileButton : Button
{
    private readonly int _baseSize;
    private readonly int _padding;
    private readonly TileEditorService? _service;
    private readonly EditableTile? _tile;
    private readonly Texture2D? _texture;
    private readonly Rect2I _region;
    private readonly Color _bgColor;
    private readonly Vector2I _tileSize; // Tile dimensions (1x1, 2x2, 1x2, etc.)
    private bool _isSelected;

    // Required by Godot for [Tool] classes
    public TileButton() { }

    public TileButton(EditableTile tile, int baseSize, int padding, TileEditorService service)
    {
        _baseSize = baseSize;
        _padding = padding;
        _service = service;
        _tile = tile;
        _tileSize = new Vector2I(tile.SizeX, tile.SizeY);

        // Scale button size based on tile dimensions
        var widthSize = baseSize * tile.SizeX / Mathf.Max(tile.SizeX, tile.SizeY);
        var heightSize = baseSize * tile.SizeY / Mathf.Max(tile.SizeX, tile.SizeY);
        var totalWidth = widthSize + padding * 2;
        var totalHeight = heightSize + padding * 2;

        CustomMinimumSize = new Vector2(totalWidth, totalHeight + 16);

        var sizeLabel = (tile.SizeX > 1 || tile.SizeY > 1) ? $" [{tile.SizeX}x{tile.SizeY}]" : "";
        TooltipText = $"{tile.Name}{sizeLabel}\n{tile.Id}\n{tile.Passability}\nSource: {tile.SourceId}, Atlas: ({tile.AtlasX},{tile.AtlasY})";
        Flat = true;
        TextureFilter = TextureFilterEnum.Nearest;

        _texture = service.GetTileTexture(tile);
        _region = service.GetTileTextureRegion(tile);

        _bgColor = tile.Passability.ToLowerInvariant() switch
        {
            "solid" => new Color(0.6f, 0.3f, 0.3f, 0.5f),
            "partially_passable" => new Color(0.6f, 0.6f, 0.3f, 0.5f),
            _ => new Color(0.3f, 0.6f, 0.3f, 0.5f)
        };
    }

    public override void _Draw()
    {
        // Guard for Godot's parameterless constructor case
        if (_tile == null || _service == null) return;

        // Calculate display size preserving aspect ratio
        var maxDim = Mathf.Max(_tileSize.X, _tileSize.Y);
        var displayWidth = _baseSize * _tileSize.X / maxDim;
        var displayHeight = _baseSize * _tileSize.Y / maxDim;
        var totalWidth = displayWidth + _padding * 2;
        var totalHeight = displayHeight + _padding * 2;

        // Background
        DrawRect(new Rect2(0, 0, totalWidth, totalHeight), _bgColor);

        // Selection border
        if (_isSelected)
        {
            DrawRect(new Rect2(0, 0, totalWidth, totalHeight), new Color(1, 0.8f, 0, 1), false, 2);
        }

        // Multi-tile indicator
        if (_tileSize.X > 1 || _tileSize.Y > 1)
        {
            var sizeText = $"{_tileSize.X}x{_tileSize.Y}";
            DrawString(ThemeDB.FallbackFont, new Vector2(totalWidth - 24, 12), sizeText,
                HorizontalAlignment.Right, 24, 10, new Color(1, 1, 0, 0.9f));
        }

        // Tile texture scaled to fit
        if (_texture != null)
        {
            var srcRect = new Rect2(_region.Position.X, _region.Position.Y, _region.Size.X, _region.Size.Y);
            var destRect = new Rect2(_padding, _padding, displayWidth, displayHeight);
            DrawTextureRectRegion(_texture, destRect, srcRect);
        }
        else
        {
            // Fallback text
            DrawString(ThemeDB.FallbackFont, new Vector2(_padding, totalHeight / 2),
                $"({_tile.AtlasX},{_tile.AtlasY})", HorizontalAlignment.Center, displayWidth);
        }

        // Tile name at bottom
        var displayName = _tile.Name.Length > 10 ? _tile.Name[..10] + ".." : _tile.Name;
        DrawString(ThemeDB.FallbackFont, new Vector2(0, totalHeight + 12),
            displayName, HorizontalAlignment.Center, totalWidth, 10);
    }

    public void UpdateTile(EditableTile tile)
    {
        TooltipText = $"{tile.Name}\n{tile.Id}\n{tile.Passability}\nSource: {tile.SourceId}, Atlas: ({tile.AtlasX},{tile.AtlasY})";
        QueueRedraw();
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        QueueRedraw();
    }
}
#endif
