#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Panel for managing biome tile pools - dynamically loads biomes from tiles.json
/// </summary>
[Tool]
public partial class BiomePoolPanel : VBoxContainer
{
    private readonly TileEditorService _service;
    private ScrollContainer? _scrollContainer;
    private VBoxContainer? _biomesContainer;
    private readonly Dictionary<string, BiomeSection> _biomeSections = new();

    // Add Biome dialog
    private AcceptDialog? _addBiomeDialog;
    private LineEdit? _biomeIdField;
    private LineEdit? _biomeDisplayNameField;
    private Label? _biomeValidationLabel;

    public BiomePoolPanel(TileEditorService service)
    {
        _service = service;
        _service.TilesLoaded += OnTilesLoaded;
        _service.BiomesLoaded += OnBiomesLoaded;
        _service.TileModified += OnTileModified;
        _service.BiomeAdded += OnBiomeAdded;
        _service.BiomeRemoved += OnBiomeRemoved;
    }

    public override void _Ready()
    {
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        // Toolbar
        var toolbar = new HBoxContainer();
        AddChild(toolbar);

        var addBiomeButton = new Button
        {
            Text = "Add Biome",
            TooltipText = "Create a new biome"
        };
        addBiomeButton.Pressed += OnAddBiomePressed;
        toolbar.AddChild(addBiomeButton);

        toolbar.AddChild(new HSeparator { SizeFlagsHorizontal = SizeFlags.Expand });

        // Scroll container for biome sections
        _scrollContainer = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        AddChild(_scrollContainer);

        _biomesContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _scrollContainer.AddChild(_biomesContainer);

        // Sections will be created when BiomesLoaded signal fires
    }

    private void OnTilesLoaded()
    {
        RefreshDisplay();
    }

    private void OnBiomesLoaded()
    {
        RebuildBiomeSections();
    }

    private void OnBiomeAdded(string biomeId)
    {
        RebuildBiomeSections();
    }

    private void OnBiomeRemoved(string biomeId)
    {
        RebuildBiomeSections();
    }

    private void RebuildBiomeSections()
    {
        if (_biomesContainer == null) return;

        // Clear existing sections
        foreach (var child in _biomesContainer.GetChildren())
        {
            child.QueueFree();
        }
        _biomeSections.Clear();

        // Create sections for each loaded biome
        foreach (var biome in _service.AllBiomes.OrderBy(b => b.DisplayName))
        {
            var section = new BiomeSection(biome.Id, _service);
            _biomesContainer.AddChild(section);
            _biomeSections[biome.Id] = section;
        }

        // Universal tiles section (tiles with no biome restrictions)
        var universalSection = new BiomeSection("universal", _service, isUniversal: true);
        _biomesContainer.AddChild(universalSection);
        _biomeSections["universal"] = universalSection;

        // Refresh tile display after sections are built
        CallDeferred(nameof(RefreshDisplay));
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

    private void OnAddBiomePressed()
    {
        // Create dialog lazily
        if (_addBiomeDialog == null)
        {
            _addBiomeDialog = new AcceptDialog
            {
                Title = "Add New Biome",
                OkButtonText = "Create",
                Size = new Vector2I(400, 180)
            };

            var vbox = new VBoxContainer();
            _addBiomeDialog.AddChild(vbox);

            vbox.AddChild(new Label { Text = "Biome ID (snake_case):" });
            _biomeIdField = new LineEdit
            {
                PlaceholderText = "new_biome_id",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            _biomeIdField.TextChanged += OnBiomeIdChanged;
            vbox.AddChild(_biomeIdField);

            vbox.AddChild(new Label { Text = "Display Name:" });
            _biomeDisplayNameField = new LineEdit
            {
                PlaceholderText = "New Biome",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            vbox.AddChild(_biomeDisplayNameField);

            _biomeValidationLabel = new Label
            {
                Text = "",
                Modulate = new Color(1, 0.3f, 0.3f)
            };
            vbox.AddChild(_biomeValidationLabel);

            _addBiomeDialog.Confirmed += OnAddBiomeConfirmed;
            AddChild(_addBiomeDialog);
        }

        // Reset dialog
        _biomeIdField!.Text = "";
        _biomeDisplayNameField!.Text = "";
        OnBiomeIdChanged("");
        _addBiomeDialog.PopupCentered();
    }

    private void OnBiomeIdChanged(string newId)
    {
        if (_biomeValidationLabel == null || _addBiomeDialog == null) return;

        var (valid, message) = ValidateBiomeId(newId);
        _biomeValidationLabel.Text = valid ? "" : message;
        _addBiomeDialog.GetOkButton().Disabled = !valid;
    }

    private (bool valid, string message) ValidateBiomeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return (false, "ID is required");

        if (!Regex.IsMatch(id, @"^[a-z][a-z0-9_]*$"))
            return (false, "ID must be snake_case starting with a letter");

        if (_service.GetBiome(id) != null)
            return (false, $"Biome '{id}' already exists");

        return (true, "");
    }

    private void OnAddBiomeConfirmed()
    {
        if (_biomeIdField == null || _biomeDisplayNameField == null) return;

        var id = _biomeIdField.Text.Trim();
        var displayName = _biomeDisplayNameField.Text.Trim();
        if (string.IsNullOrEmpty(displayName))
            displayName = char.ToUpper(id[0]) + id[1..].Replace("_", " ");

        var (valid, _) = ValidateBiomeId(id);
        if (!valid) return;

        var biome = new EditableBiome
        {
            Id = id,
            DisplayName = displayName,
            Signature = new float[8],
            BlockedPercentage = 0.2f,
            PassableTiles = new Dictionary<string, float>(),
            BlockedTiles = new Dictionary<string, float>()
        };

        _service.AddBiome(biome);
    }
}

/// <summary>
/// Expandable section for a single biome's tile pool - shows passable and blocked tiles with weights
/// </summary>
[Tool]
public partial class BiomeSection : VBoxContainer
{
    private readonly string _biomeId;
    private readonly TileEditorService _service;
    private readonly bool _isUniversal;
    private Button? _headerButton;
    private VBoxContainer? _contentContainer;
    private VBoxContainer? _passableContainer;
    private VBoxContainer? _blockedContainer;
    private bool _isExpanded = true;

    public BiomeSection(string biomeId, TileEditorService service, bool isUniversal = false)
    {
        _biomeId = biomeId;
        _service = service;
        _isUniversal = isUniversal;
    }

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        // Get display name from biome data
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

        // Content container
        _contentContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        AddChild(_contentContainer);

        if (!_isUniversal)
        {
            // Passable tiles section
            var passableLabel = new Label { Text = "Passable Tiles:", Modulate = new Color(0.3f, 0.8f, 0.3f) };
            _contentContainer.AddChild(passableLabel);

            _passableContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _contentContainer.AddChild(_passableContainer);

            // Blocked tiles section
            var blockedLabel = new Label { Text = "Blocked Tiles:", Modulate = new Color(0.8f, 0.3f, 0.3f) };
            _contentContainer.AddChild(blockedLabel);

            _blockedContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _contentContainer.AddChild(_blockedContainer);
        }
        else
        {
            // Universal section just shows tiles with no biome restriction
            _passableContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _contentContainer.AddChild(_passableContainer);
        }

        // Make droppable
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void RefreshTiles()
    {
        if (_passableContainer == null) return;

        // Clear existing
        foreach (var child in _passableContainer.GetChildren())
            child.QueueFree();
        if (_blockedContainer != null)
        {
            foreach (var child in _blockedContainer.GetChildren())
                child.QueueFree();
        }

        if (_isUniversal)
        {
            // Show tiles with no biome restrictions
            var universalTiles = _service.AllTiles.Where(t => t.Biomes.Count == 0).ToList();
            UpdateHeader(universalTiles.Count);

            if (!_isExpanded) return;

            foreach (var tile in universalTiles.OrderBy(t => t.Id))
            {
                var entry = new BiomeTileEntry(tile, _biomeId, _service, isUniversal: true, weight: 0, isBlocked: false);
                _passableContainer.AddChild(entry);
            }

            if (universalTiles.Count == 0)
            {
                _passableContainer.AddChild(CreatePlaceholder("No universal tiles defined"));
            }
        }
        else
        {
            // Show tiles from biome pools with weights
            var biome = _service.GetBiome(_biomeId);
            if (biome == null)
            {
                UpdateHeader(0);
                return;
            }

            var totalTiles = biome.PassableTiles.Count + biome.BlockedTiles.Count;
            UpdateHeader(totalTiles);

            if (!_isExpanded) return;

            // Passable tiles with weights
            foreach (var (tileId, weight) in biome.PassableTiles.OrderByDescending(kvp => kvp.Value))
            {
                var tile = _service.GetTile(tileId);
                if (tile != null)
                {
                    var entry = new BiomeTileEntry(tile, _biomeId, _service, isUniversal: false, weight, isBlocked: false);
                    _passableContainer.AddChild(entry);
                }
            }

            if (biome.PassableTiles.Count == 0)
            {
                _passableContainer.AddChild(CreatePlaceholder("Drop passable tiles here"));
            }

            // Blocked tiles with weights
            if (_blockedContainer != null)
            {
                foreach (var (tileId, weight) in biome.BlockedTiles.OrderByDescending(kvp => kvp.Value))
                {
                    var tile = _service.GetTile(tileId);
                    if (tile != null)
                    {
                        var entry = new BiomeTileEntry(tile, _biomeId, _service, isUniversal: false, weight, isBlocked: true);
                        _blockedContainer.AddChild(entry);
                    }
                }

                if (biome.BlockedTiles.Count == 0)
                {
                    _blockedContainer.AddChild(CreatePlaceholder("Drop blocked tiles here"));
                }
            }
        }
    }

    private void UpdateHeader(int tileCount)
    {
        var biome = _service.GetBiome(_biomeId);
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

        var biome = _service.GetBiome(_biomeId);
        if (biome == null) return;

        // Add tile to appropriate pool based on passability
        var isBlocked = string.Equals(tile.Passability, "solid", StringComparison.OrdinalIgnoreCase);
        var pool = isBlocked ? biome.BlockedTiles : biome.PassableTiles;

        if (!pool.ContainsKey(tileId))
        {
            pool[tileId] = 0.1f; // Default weight for new tiles
            _service.UpdateBiome(biome);
        }
    }
}

/// <summary>
/// Entry for a tile within a biome pool - includes weight editing
/// </summary>
[Tool]
public partial class BiomeTileEntry : HBoxContainer
{
    private readonly EditableTile? _tile;
    private readonly string? _biomeId;
    private readonly TileEditorService? _service;
    private readonly bool _isUniversal;
    private readonly float _weight;
    private readonly bool _isBlocked;

    // Required by Godot for [Tool] classes
    public BiomeTileEntry() { }

    public BiomeTileEntry(EditableTile tile, string biomeId, TileEditorService service, bool isUniversal, float weight, bool isBlocked)
    {
        _tile = tile;
        _biomeId = biomeId;
        _service = service;
        _isUniversal = isUniversal;
        _weight = weight;
        _isBlocked = isBlocked;
    }

    public override void _Ready()
    {
        // Guard for Godot's parameterless constructor case
        if (_tile == null || _service == null) return;

        CustomMinimumSize = new Vector2(280, 30);

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
            Text = _tile.Name.Length > 12 ? _tile.Name[..12] + ".." : _tile.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = $"{_tile.Id}\n{_tile.Name}\n({_tile.AtlasX}, {_tile.AtlasY})"
        };
        AddChild(nameLabel);

        // Weight spinbox (only for biome pools, not universal)
        if (!_isUniversal)
        {
            var weightSpinBox = new SpinBox
            {
                MinValue = 0.01,
                MaxValue = 1.0,
                Step = 0.01,
                Value = _weight,
                CustomMinimumSize = new Vector2(70, 0),
                TooltipText = "Tile weight (will be normalized on save)"
            };
            weightSpinBox.ValueChanged += OnWeightChanged;
            AddChild(weightSpinBox);
        }

        // Remove button (only for non-universal)
        if (!_isUniversal)
        {
            var removeBtn = new Button
            {
                Text = "X",
                CustomMinimumSize = new Vector2(24, 24),
                TooltipText = $"Remove from biome pool"
            };
            removeBtn.Pressed += RemoveFromBiome;
            AddChild(removeBtn);
        }
    }

    private void OnWeightChanged(double newValue)
    {
        if (_service == null || _biomeId == null || _tile == null) return;

        var biome = _service.GetBiome(_biomeId);
        if (biome == null) return;

        var pool = _isBlocked ? biome.BlockedTiles : biome.PassableTiles;
        if (pool.ContainsKey(_tile.Id))
        {
            pool[_tile.Id] = (float)newValue;
            _service.UpdateBiome(biome);
        }
    }

    private void RemoveFromBiome()
    {
        if (_service == null || _biomeId == null || _tile == null) return;

        var biome = _service.GetBiome(_biomeId);
        if (biome == null) return;

        var pool = _isBlocked ? biome.BlockedTiles : biome.PassableTiles;
        if (pool.Remove(_tile.Id))
        {
            _service.UpdateBiome(biome);
        }
    }
}
#endif
