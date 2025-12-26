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
    private const int TileDisplaySize = 48;
    private const int TilesPerRow = 10;

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
            var button = new TileButton(tile, TileDisplaySize, _service);
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
/// Button representing a single tile in the grid
/// </summary>
[Tool]
public partial class TileButton : Button
{
    private readonly int _size;
    private ColorRect? _selectionBorder;
    private Label? _nameLabel;
    private TextureRect? _tilePreview;

    public TileButton(EditableTile tile, int size, TileEditorService service)
    {
        _size = size;

        CustomMinimumSize = new Vector2(size, size + 20);
        TooltipText = $"{tile.Name}\n{tile.Id}\n{tile.Passability}\nSource: {tile.SourceId}, Atlas: ({tile.AtlasX},{tile.AtlasY})";
        Flat = true;

        var vbox = new VBoxContainer();
        AddChild(vbox);

        // Tile preview container
        var previewContainer = new Control
        {
            CustomMinimumSize = new Vector2(size, size)
        };
        vbox.AddChild(previewContainer);

        // Background color based on passability
        var bgColor = tile.Passability.ToLowerInvariant() switch
        {
            "solid" => new Color(0.6f, 0.3f, 0.3f, 0.5f),
            "partially_passable" => new Color(0.6f, 0.6f, 0.3f, 0.5f),
            _ => new Color(0.3f, 0.6f, 0.3f, 0.5f)
        };

        var bg = new ColorRect
        {
            Color = bgColor,
            Size = new Vector2(size, size)
        };
        previewContainer.AddChild(bg);

        // Selection border
        _selectionBorder = new ColorRect
        {
            Color = new Color(1, 0.8f, 0, 1),
            Size = new Vector2(size, size),
            Visible = false
        };
        previewContainer.AddChild(_selectionBorder);

        var innerBg = new ColorRect
        {
            Color = bgColor,
            Position = new Vector2(2, 2),
            Size = new Vector2(size - 4, size - 4)
        };
        previewContainer.AddChild(innerBg);

        // Tile texture preview
        var texture = service.GetTileTexture(tile);
        if (texture != null)
        {
            var region = service.GetTileTextureRegion(tile);

            // Create an AtlasTexture to show just this tile
            var atlasTexture = new AtlasTexture
            {
                Atlas = texture,
                Region = new Rect2(region.Position.X, region.Position.Y, region.Size.X, region.Size.Y)
            };

            _tilePreview = new TextureRect
            {
                Texture = atlasTexture,
                Position = new Vector2(4, 4),
                Size = new Vector2(size - 8, size - 8),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            previewContainer.AddChild(_tilePreview);
        }
        else
        {
            // Fallback: show atlas coordinate label when no texture available
            var coordLabel = new Label
            {
                Text = $"({tile.AtlasX},{tile.AtlasY})",
                HorizontalAlignment = HorizontalAlignment.Center,
                Position = new Vector2(0, size / 2 - 10),
                Size = new Vector2(size, 20)
            };
            coordLabel.AddThemeFontSizeOverride("font_size", 10);
            previewContainer.AddChild(coordLabel);
        }

        // Tile name
        _nameLabel = new Label
        {
            Text = tile.Name.Length > 10 ? tile.Name[..10] + "..." : tile.Name,
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(size, 20)
        };
        _nameLabel.AddThemeFontSizeOverride("font_size", 9);
        vbox.AddChild(_nameLabel);
    }

    public void UpdateTile(EditableTile tile)
    {
        TooltipText = $"{tile.Name}\n{tile.Id}\n{tile.Passability}\nSource: {tile.SourceId}, Atlas: ({tile.AtlasX},{tile.AtlasY})";
        if (_nameLabel != null)
        {
            _nameLabel.Text = tile.Name.Length > 10 ? tile.Name[..10] + "..." : tile.Name;
        }
    }

    public void SetSelected(bool selected)
    {
        if (_selectionBorder != null)
        {
            _selectionBorder.Visible = selected;
        }
    }
}
#endif
