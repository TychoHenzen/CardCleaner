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

    private readonly TileEditorService _service;
    private ScrollContainer? _scrollContainer;
    private GridContainer? _tileGrid;
    private LineEdit? _searchBox;
    private OptionButton? _biomeFilter;
    private OptionButton? _passabilityFilter;

    private string? _selectedTileId;
    private readonly Dictionary<string, TileButton> _tileButtons = new();

    [Signal] public delegate void TileSelectedEventHandler(string tileId);

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

        EmitSignal(SignalName.TileSelected, tileId);
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
}

/// <summary>
/// Button representing a single tile in the grid - uses direct drawing for proper scaling
/// </summary>
[Tool]
public partial class TileButton : Button
{
    private readonly int _baseSize;
    private readonly int _padding;
    private readonly TileEditorService _service;
    private readonly EditableTile _tile;
    private readonly Texture2D? _texture;
    private readonly Rect2I _region;
    private readonly Color _bgColor;
    private readonly Vector2I _tileSize; // Tile dimensions (1x1, 2x2, 1x2, etc.)
    private bool _isSelected;

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
