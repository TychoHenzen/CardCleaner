#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Expandable section for a single biome's tile pool - shows passable and blocked tiles with weights.
/// </summary>
[Tool]
public partial class BiomeSection : VBoxContainer
{
    private readonly string? _biomeId;
    private readonly TileEditorService? _service;
    private readonly bool _isUniversal;
    private Button? _headerButton;
    private VBoxContainer? _contentContainer;
    private VBoxContainer? _passableContainer;
    private VBoxContainer? _blockedContainer;
    private bool _isExpanded = true;

    public BiomeSection() { }

    public BiomeSection(string biomeId, TileEditorService service, bool isUniversal = false)
    {
        _biomeId = biomeId;
        _service = service;
        _isUniversal = isUniversal;
    }

    public override void _Ready()
    {
        if (_service == null || _biomeId == null)
            return;

        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var biome = _service.GetBiome(_biomeId);
        var displayName = _isUniversal
            ? "Universal (No Biome Restriction)"
            : biome?.DisplayName ?? _biomeId;
        _headerButton = new Button
        {
            Text = $"[-] {displayName} (0 tiles)",
            Alignment = HorizontalAlignment.Left,
            Flat = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _headerButton.Pressed += ToggleExpanded;
        AddChild(_headerButton);
        _contentContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        AddChild(_contentContainer);
        AddTileContainers();
        MouseFilter = MouseFilterEnum.Stop;
    }

    private void AddTileContainers()
    {
        if (_isUniversal)
        {
            _passableContainer = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            _contentContainer!.AddChild(_passableContainer);
            return;
        }

        var passableLabel = new Label
        {
            Text = "Passable Tiles:",
            Modulate = new Color(0.3f, 0.8f, 0.3f)
        };
        _contentContainer!.AddChild(passableLabel);
        _passableContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _contentContainer.AddChild(_passableContainer);

        var blockedLabel = new Label
        {
            Text = "Blocked Tiles:",
            Modulate = new Color(0.8f, 0.3f, 0.3f)
        };
        _contentContainer.AddChild(blockedLabel);
        _blockedContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _contentContainer.AddChild(_blockedContainer);
    }

    public void RefreshTiles()
    {
        if (_passableContainer == null)
            return;

        ClearTileContainers();
        if (_isUniversal)
            RefreshUniversalTiles();
        else
            RefreshBiomeTiles();
    }

    private void ClearTileContainers()
    {
        QueueChildrenForFree(_passableContainer!);
        if (_blockedContainer != null)
            QueueChildrenForFree(_blockedContainer);
    }

    private static void QueueChildrenForFree(Node container)
    {
        foreach (var child in container.GetChildren())
            child.QueueFree();
    }

    private void RefreshUniversalTiles()
    {
        var universalTiles = _service!.AllTiles
            .Where(tile => tile.Biomes.Count == 0)
            .ToList();
        UpdateHeader(universalTiles.Count);
        if (!_isExpanded)
            return;

        foreach (var tile in universalTiles.OrderBy(tile => tile.Id))
        {
            var entry = new BiomeTileEntry(
                tile,
                _biomeId!,
                _service,
                isUniversal: true,
                weight: 0,
                isBlocked: false);
            _passableContainer!.AddChild(entry);
        }

        if (universalTiles.Count == 0)
            _passableContainer!.AddChild(CreatePlaceholder("No universal tiles defined"));
    }

    private void RefreshBiomeTiles()
    {
        var biome = _service!.GetBiome(_biomeId!);
        if (biome == null)
        {
            UpdateHeader(0);
            return;
        }

        UpdateHeader(biome.PassableTiles.Count + biome.BlockedTiles.Count);
        if (!_isExpanded)
            return;

        AddWeightedTiles(
            biome.PassableTiles,
            _passableContainer!,
            isBlocked: false,
            "Drop passable tiles here");
        if (_blockedContainer != null)
        {
            AddWeightedTiles(
                biome.BlockedTiles,
                _blockedContainer,
                isBlocked: true,
                "Drop blocked tiles here");
        }
    }

    private void AddWeightedTiles(
        Dictionary<string, float> pool,
        VBoxContainer container,
        bool isBlocked,
        string emptyMessage)
    {
        foreach (var (tileId, weight) in pool.OrderByDescending(pair => pair.Value))
        {
            var tile = _service!.GetTile(tileId);
            if (tile == null)
                continue;

            var entry = new BiomeTileEntry(
                tile,
                _biomeId!,
                _service,
                isUniversal: false,
                weight,
                isBlocked);
            container.AddChild(entry);
        }

        if (pool.Count == 0)
            container.AddChild(CreatePlaceholder(emptyMessage));
    }

    private void UpdateHeader(int tileCount)
    {
        var biome = _service!.GetBiome(_biomeId!);
        var displayName = _isUniversal
            ? "Universal (No Biome Restriction)"
            : biome?.DisplayName ?? _biomeId;
        var prefix = _isExpanded ? "[-]" : "[+]";
        _headerButton!.Text = $"{prefix} {displayName} ({tileCount} tiles)";
    }

    private static Label CreatePlaceholder(string text)
    {
        return new Label
        {
            Text = text,
            Modulate = new Color(0.6f, 0.6f, 0.6f)
        };
    }

    private void ToggleExpanded()
    {
        _isExpanded = !_isExpanded;
        _contentContainer!.Visible = _isExpanded;
        RefreshTiles();
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (_isUniversal)
            return false;
        if (data.VariantType != Variant.Type.Dictionary)
            return false;

        var dict = data.AsGodotDictionary();
        return dict.ContainsKey("type") && dict["type"].AsString() == "tile";
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (_isUniversal)
            return;

        var dict = data.AsGodotDictionary();
        var tileId = dict["tile_id"].AsString();
        var tile = _service!.GetTile(tileId);
        if (tile == null)
            return;

        var biome = _service.GetBiome(_biomeId!);
        if (biome == null)
            return;

        var isBlocked = string.Equals(
            tile.Passability,
            "solid",
            StringComparison.OrdinalIgnoreCase);
        var pool = isBlocked ? biome.BlockedTiles : biome.PassableTiles;
        if (pool.ContainsKey(tileId))
            return;

        pool[tileId] = 0.1f;
        _service.UpdateBiome(biome);
    }
}
#endif
