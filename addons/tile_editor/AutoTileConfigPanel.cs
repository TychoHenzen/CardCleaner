#if TOOLS
using System.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
///     Panel for editing auto-tile configurations.
///     Shows 16 slots for edge variants based on 4-bit NESW neighbor bitmask.
/// </summary>
[Tool]
public partial class AutoTileConfigPanel : ScrollContainer
{
    // Bitmask labels showing which neighbors are present (N=1, E=2, S=4, W=8)
    private static readonly string[] BitmaskLabels =
    {
        "None", // 0
        "N", // 1
        "E", // 2
        "N+E", // 3
        "S", // 4
        "N+S", // 5
        "E+S", // 6
        "N+E+S", // 7
        "W", // 8
        "N+W", // 9
        "E+W", // 10
        "N+E+W", // 11
        "S+W", // 12
        "N+S+W", // 13
        "E+S+W", // 14
        "All" // 15
    };

    private readonly TileEditorService _service;
    private readonly Button?[] _variantButtons = new Button?[16];
    private readonly Label?[] _variantLabels = new Label?[16];
    private OptionButton? _baseTileDropdown;
    private ItemList? _configList;
    private EditableAutoTileConfig? _currentConfig;
    private LineEdit? _displayNameEdit;
    private bool _isUpdating;
    private GridContainer? _variantGrid;

    public AutoTileConfigPanel(TileEditorService service)
    {
        _service = service;
        _service.TilesLoaded += RefreshAll;
        _service.AutoTileConfigModified += OnAutoTileConfigModified;

        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        SetupUI();
    }

    private void SetupUI()
    {
        var mainVBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(mainVBox);

        // Header
        var headerLabel = new Label { Text = "Auto-Tile Configurations", ThemeTypeVariation = "HeaderMedium" };
        mainVBox.AddChild(headerLabel);

        var descLabel = new Label
        {
            Text =
                "Define 16 edge variants for each terrain type.\nEach slot represents a neighbor bitmask (N=1, E=2, S=4, W=8).",
            AutowrapMode = TextServer.AutowrapMode.Word
        };
        mainVBox.AddChild(descLabel);

        mainVBox.AddChild(new HSeparator());

        // Config list panel
        var listSection = new VBoxContainer();
        mainVBox.AddChild(listSection);

        var listHeader = new HBoxContainer();
        listSection.AddChild(listHeader);
        listHeader.AddChild(new Label { Text = "Configurations:" });

        var addButton = new Button { Text = "+ New" };
        addButton.Pressed += OnAddConfigPressed;
        listHeader.AddChild(addButton);

        var removeButton = new Button { Text = "- Remove" };
        removeButton.Pressed += OnRemoveConfigPressed;
        listHeader.AddChild(removeButton);

        _configList = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 80), SelectMode = ItemList.SelectModeEnum.Single
        };
        _configList.ItemSelected += OnConfigSelected;
        listSection.AddChild(_configList);

        mainVBox.AddChild(new HSeparator());

        // Config editor section
        var editorSection = new VBoxContainer();
        mainVBox.AddChild(editorSection);

        // Base tile selector
        var baseTileRow = new HBoxContainer();
        editorSection.AddChild(baseTileRow);
        baseTileRow.AddChild(new Label { Text = "Base Tile:", CustomMinimumSize = new Vector2(100, 0) });
        _baseTileDropdown = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _baseTileDropdown.ItemSelected += OnBaseTileChanged;
        baseTileRow.AddChild(_baseTileDropdown);

        // Display name
        var nameRow = new HBoxContainer();
        editorSection.AddChild(nameRow);
        nameRow.AddChild(new Label { Text = "Display Name:", CustomMinimumSize = new Vector2(100, 0) });
        _displayNameEdit = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _displayNameEdit.TextChanged += OnDisplayNameChanged;
        nameRow.AddChild(_displayNameEdit);

        editorSection.AddChild(new HSeparator());

        // Variant grid header
        var gridHeader = new Label { Text = "Variants (click to assign tile, right-click to clear):" };
        editorSection.AddChild(gridHeader);

        // 4x4 grid of variant slots
        _variantGrid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        editorSection.AddChild(_variantGrid);

        for (var i = 0; i < 16; i++)
        {
            var slot = CreateVariantSlot(i);
            _variantGrid.AddChild(slot);
        }
    }

    private Control CreateVariantSlot(int bitmask)
    {
        var container = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(70, 80), SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        // Label showing which neighbors
        var label = new Label
        {
            Text = $"{bitmask}: {BitmaskLabels[bitmask]}", HorizontalAlignment = HorizontalAlignment.Center
        };
        _variantLabels[bitmask] = label;
        container.AddChild(label);

        // Button that shows the tile (or empty)
        var button = new Button
        {
            Text = "(none)",
            CustomMinimumSize = new Vector2(60, 40),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClipText = true
        };
        button.Pressed += () => OnVariantButtonPressed(bitmask);
        button.GuiInput += evt => OnVariantButtonGuiInput(evt, bitmask);
        _variantButtons[bitmask] = button;
        container.AddChild(button);

        return container;
    }

    private void OnVariantButtonGuiInput(InputEvent evt, int bitmask)
    {
        if (evt is InputEventMouseButton mouseEvent &&
            mouseEvent.Pressed &&
            mouseEvent.ButtonIndex == MouseButton.Right)
            // Right-click clears the variant
            ClearVariant(bitmask);
    }

    private void RefreshAll()
    {
        RefreshConfigList();
        RefreshBaseTileDropdown();
        RefreshCurrentConfig();
    }

    private void RefreshConfigList()
    {
        _configList?.Clear();
        foreach (var config in _service.AllAutoTileConfigs)
        {
            var displayText = string.IsNullOrEmpty(config.DisplayName)
                ? config.BaseTileId
                : $"{config.DisplayName} ({config.BaseTileId})";
            _configList?.AddItem(displayText);
        }
    }

    private void RefreshBaseTileDropdown()
    {
        _baseTileDropdown?.Clear();

        // Add passable terrain tiles as potential base tiles
        var terrainTiles = _service.AllTiles
            .Where(t => t.Layer == "terrain" && t.Passability == "passable")
            .OrderBy(t => t.Id)
            .ToList();

        foreach (var tile in terrainTiles) _baseTileDropdown?.AddItem($"{tile.Id} - {tile.Name}");
    }

    private void RefreshCurrentConfig()
    {
        _isUpdating = true;
        try
        {
            if (_currentConfig == null)
            {
                _baseTileDropdown!.Disabled = true;
                _displayNameEdit!.Editable = false;
                _displayNameEdit.Text = "";

                for (var i = 0; i < 16; i++)
                {
                    _variantButtons[i]!.Text = "(none)";
                    _variantButtons[i]!.Disabled = true;
                }

                return;
            }

            _baseTileDropdown!.Disabled = false;
            _displayNameEdit!.Editable = true;
            _displayNameEdit.Text = _currentConfig.DisplayName;

            // Select the base tile in dropdown
            var baseTileIndex = FindBaseTileIndex(_currentConfig.BaseTileId);
            if (baseTileIndex >= 0)
                _baseTileDropdown.Selected = baseTileIndex;

            // Update variant buttons
            for (var i = 0; i < 16; i++)
            {
                var variantId = _currentConfig.Variants[i];
                _variantButtons[i]!.Disabled = false;

                if (string.IsNullOrEmpty(variantId))
                {
                    _variantButtons[i]!.Text = "(base)";
                    _variantButtons[i]!.Modulate = new Color(0.6f, 0.6f, 0.6f);
                }
                else
                {
                    var tile = _service.GetTile(variantId);
                    _variantButtons[i]!.Text = tile?.Name ?? variantId;
                    _variantButtons[i]!.Modulate = Colors.White;
                }
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private int FindBaseTileIndex(string baseTileId)
    {
        var terrainTiles = _service.AllTiles
            .Where(t => t.Layer == "terrain" && t.Passability == "passable")
            .OrderBy(t => t.Id)
            .ToList();

        for (var i = 0; i < terrainTiles.Count; i++)
            if (terrainTiles[i].Id == baseTileId)
                return i;

        return -1;
    }

    private string? GetTileIdAtDropdownIndex(int index)
    {
        var terrainTiles = _service.AllTiles
            .Where(t => t.Layer == "terrain" && t.Passability == "passable")
            .OrderBy(t => t.Id)
            .ToList();

        if (index >= 0 && index < terrainTiles.Count)
            return terrainTiles[index].Id;
        return null;
    }

    private void OnAutoTileConfigModified(string baseTileId)
    {
        RefreshConfigList();
        if (_currentConfig?.BaseTileId == baseTileId)
        {
            _currentConfig = _service.GetAutoTileConfig(baseTileId);
            RefreshCurrentConfig();
        }
    }

    private void OnConfigSelected(long index)
    {
        var configs = _service.AllAutoTileConfigs.ToList();
        if (index >= 0 && index < configs.Count)
        {
            _currentConfig = configs[(int)index];
            RefreshCurrentConfig();
        }
    }

    private void OnAddConfigPressed()
    {
        // Create a new config with first available tile
        var firstTile = _service.AllTiles
            .Where(t => t.Layer == "terrain" && t.Passability == "passable")
            .OrderBy(t => t.Id)
            .FirstOrDefault();

        if (firstTile == null)
        {
            GD.PrintErr("[AutoTileConfigPanel] No terrain tiles available");
            return;
        }

        // Check if config already exists for this tile
        if (_service.GetAutoTileConfig(firstTile.Id) != null)
        {
            // Find first tile without a config
            var availableTile = _service.AllTiles
                .Where(t => t.Layer == "terrain" && t.Passability == "passable")
                .Where(t => _service.GetAutoTileConfig(t.Id) == null)
                .OrderBy(t => t.Id)
                .FirstOrDefault();

            if (availableTile == null)
            {
                GD.Print("[AutoTileConfigPanel] All terrain tiles already have auto-tile configs");
                return;
            }

            firstTile = availableTile;
        }

        var newConfig = new EditableAutoTileConfig { BaseTileId = firstTile.Id, DisplayName = firstTile.Name };

        _service.AddAutoTileConfig(newConfig);
        _currentConfig = newConfig;
        RefreshConfigList();
        RefreshCurrentConfig();

        // Select the new config in the list
        _configList!.Select(_configList.ItemCount - 1);
    }

    private void OnRemoveConfigPressed()
    {
        if (_currentConfig == null) return;

        _service.RemoveAutoTileConfig(_currentConfig.BaseTileId);
        _currentConfig = null;
        RefreshConfigList();
        RefreshCurrentConfig();
    }

    private void OnBaseTileChanged(long index)
    {
        if (_isUpdating || _currentConfig == null) return;

        var newBaseTileId = GetTileIdAtDropdownIndex((int)index);
        if (newBaseTileId == null || newBaseTileId == _currentConfig.BaseTileId) return;

        // Check if config already exists for new tile
        if (_service.GetAutoTileConfig(newBaseTileId) != null)
        {
            GD.Print($"[AutoTileConfigPanel] Config already exists for {newBaseTileId}");
            RefreshCurrentConfig(); // Revert dropdown
            return;
        }

        // Remove old config and add with new base tile
        var oldId = _currentConfig.BaseTileId;
        _currentConfig.BaseTileId = newBaseTileId;
        _service.RemoveAutoTileConfig(oldId);
        _service.AddAutoTileConfig(_currentConfig);

        RefreshConfigList();
    }

    private void OnDisplayNameChanged(string newText)
    {
        if (_isUpdating || _currentConfig == null) return;

        _currentConfig.DisplayName = newText;
        _service.UpdateAutoTileConfig(_currentConfig);
    }

    private void OnVariantButtonPressed(int bitmask)
    {
        if (_currentConfig == null) return;

        // Show tile picker popup
        ShowTilePicker(bitmask);
    }

    private void ShowTilePicker(int bitmask)
    {
        var popup = new PopupMenu();
        AddChild(popup);

        popup.AddItem("(Use Base Tile)", 0);
        popup.AddSeparator();

        // Add all tiles as options
        var tiles = _service.AllTiles.OrderBy(t => t.Id).ToList();
        for (var i = 0; i < tiles.Count; i++) popup.AddItem($"{tiles[i].Id} - {tiles[i].Name}", i + 1);

        popup.IdPressed += id =>
        {
            if (id == 0)
            {
                // Use base tile (null/empty)
                SetVariant(bitmask, null);
            }
            else
            {
                var tileIndex = (int)id - 1;
                if (tileIndex >= 0 && tileIndex < tiles.Count) SetVariant(bitmask, tiles[tileIndex].Id);
            }
        };

        popup.PopupHide += () => popup.QueueFree();

        popup.Position = (Vector2I)GetGlobalMousePosition();
        popup.Popup();
    }

    private void SetVariant(int bitmask, string? tileId)
    {
        if (_currentConfig == null) return;

        _currentConfig.Variants[bitmask] = tileId;
        _service.UpdateAutoTileConfig(_currentConfig);
        RefreshCurrentConfig();
    }

    private void ClearVariant(int bitmask) => SetVariant(bitmask, null);
}
#endif
