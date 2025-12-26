#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Panel for managing biome tile pools
/// </summary>
[Tool]
public partial class BiomePoolPanel : ScrollContainer
{
    private readonly TileEditorService _service;
    private VBoxContainer? _container;
    private readonly Dictionary<string, BiomeSection> _biomeSections = new();

    private static readonly string[] Biomes = { "plains", "forest", "desert", "tundra", "swamp", "mountains" };

    public BiomePoolPanel(TileEditorService service)
    {
        _service = service;
        _service.TilesLoaded += RefreshDisplay;
        _service.TileModified += OnTileModified;
    }

    public override void _Ready()
    {
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        HorizontalScrollMode = ScrollMode.Disabled;

        _container = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        AddChild(_container);

        // Create sections for each biome
        foreach (var biome in Biomes)
        {
            var section = new BiomeSection(biome, _service);
            _container.AddChild(section);
            _biomeSections[biome] = section;
        }

        // Universal tiles section
        var universalSection = new BiomeSection("universal", _service, isUniversal: true);
        _container.AddChild(universalSection);
        _biomeSections["universal"] = universalSection;
    }

    private void RefreshDisplay()
    {
        foreach (var section in _biomeSections.Values)
        {
            section.RefreshTiles();
        }
    }

    private void OnTileModified(string tileId)
    {
        // Refresh all sections as tile biomes may have changed
        RefreshDisplay();
    }
}

/// <summary>
/// Expandable section for a single biome's tile pool
/// </summary>
[Tool]
public partial class BiomeSection : VBoxContainer
{
    private readonly string _biomeName;
    private readonly TileEditorService _service;
    private readonly bool _isUniversal;
    private Button? _headerButton;
    private VBoxContainer? _contentContainer;
    private FlowContainer? _tileFlow;
    private bool _isExpanded = true;

    public BiomeSection(string biomeName, TileEditorService service, bool isUniversal = false)
    {
        _biomeName = biomeName;
        _service = service;
        _isUniversal = isUniversal;
    }

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        // Header button (expand/collapse)
        var displayName = _isUniversal
            ? "Universal (No Biome)"
            : char.ToUpper(_biomeName[0]) + _biomeName[1..];

        _headerButton = new Button
        {
            Text = $"[-] {displayName} (0 tiles)",
            Alignment = HorizontalAlignment.Left,
            Flat = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _headerButton.Pressed += ToggleExpanded;
        AddChild(_headerButton);

        // Content container
        _contentContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        AddChild(_contentContainer);

        // Panel background for drop target
        var dropPanel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 60)
        };
        _contentContainer.AddChild(dropPanel);

        // Tile flow container
        _tileFlow = new FlowContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        dropPanel.AddChild(_tileFlow);

        // Make droppable
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void RefreshTiles()
    {
        if (_tileFlow == null) return;

        // Clear existing
        foreach (var child in _tileFlow.GetChildren())
        {
            child.QueueFree();
        }

        // Get tiles for this biome
        var tiles = _isUniversal
            ? _service.AllTiles.Where(t => t.Biomes.Count == 0).ToList()
            : _service.AllTiles.Where(t => t.Biomes.Contains(_biomeName, StringComparer.OrdinalIgnoreCase)).ToList();

        // Update header
        var displayName = _isUniversal
            ? "Universal (No Biome)"
            : char.ToUpper(_biomeName[0]) + _biomeName[1..];

        var prefix = _isExpanded ? "[-]" : "[+]";
        _headerButton!.Text = $"{prefix} {displayName} ({tiles.Count} tiles)";

        if (!_isExpanded) return;

        // Add tile entries
        foreach (var tile in tiles.OrderBy(t => t.Id))
        {
            var entry = new BiomeTileEntry(tile, _biomeName, _service, _isUniversal);
            _tileFlow.AddChild(entry);
        }

        if (tiles.Count == 0)
        {
            var placeholder = new Label
            {
                Text = _isUniversal
                    ? "Tiles with no biome restrictions appear here"
                    : "Drop tiles here to add to biome",
                Modulate = new Color(0.6f, 0.6f, 0.6f)
            };
            placeholder.AddThemeFontSizeOverride("font_size", 11);
            _tileFlow.AddChild(placeholder);
        }
    }

    private void ToggleExpanded()
    {
        _isExpanded = !_isExpanded;
        _contentContainer!.Visible = _isExpanded;
        RefreshTiles();
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (_isUniversal) return false; // Can't drop to universal

        if (data.VariantType != Variant.Type.Dictionary) return false;

        var dict = data.AsGodotDictionary();
        return dict.ContainsKey("type") && dict["type"].AsString() == "tile";
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (_isUniversal) return;

        var dict = data.AsGodotDictionary();
        var tileId = dict["tile_id"].AsString();

        var tile = _service.GetTile(tileId);
        if (tile == null) return;

        // Add biome to tile
        if (!tile.Biomes.Contains(_biomeName, StringComparer.OrdinalIgnoreCase))
        {
            tile.Biomes.Add(_biomeName);
            _service.UpdateTile(tile);
        }
    }
}

/// <summary>
/// Entry for a tile within a biome pool
/// </summary>
[Tool]
public partial class BiomeTileEntry : HBoxContainer
{
    private readonly EditableTile _tile;
    private readonly string _biomeName;
    private readonly TileEditorService _service;
    private readonly bool _isUniversal;

    public BiomeTileEntry(EditableTile tile, string biomeName, TileEditorService service, bool isUniversal)
    {
        _tile = tile;
        _biomeName = biomeName;
        _service = service;
        _isUniversal = isUniversal;
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(180, 30);

        // Tile indicator (passability color)
        var indicator = new ColorRect
        {
            CustomMinimumSize = new Vector2(16, 16),
            Color = _tile.Passability.ToLowerInvariant() switch
            {
                "solid" => new Color(0.8f, 0.3f, 0.3f),
                "partially_passable" => new Color(0.8f, 0.8f, 0.3f),
                _ => new Color(0.3f, 0.8f, 0.3f)
            }
        };
        AddChild(indicator);

        // Tile name
        var nameLabel = new Label
        {
            Text = _tile.Name.Length > 15 ? _tile.Name[..15] + "..." : _tile.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = $"{_tile.Id}\n{_tile.Name}\n({_tile.AtlasX}, {_tile.AtlasY})"
        };
        AddChild(nameLabel);

        // Remove button (only for non-universal)
        if (!_isUniversal)
        {
            var removeBtn = new Button
            {
                Text = "X",
                CustomMinimumSize = new Vector2(24, 24),
                TooltipText = $"Remove from {_biomeName}"
            };
            removeBtn.Pressed += RemoveFromBiome;
            AddChild(removeBtn);
        }
    }

    private void RemoveFromBiome()
    {
        _tile.Biomes.Remove(_biomeName);
        _service.UpdateTile(_tile);
    }
}
#endif
