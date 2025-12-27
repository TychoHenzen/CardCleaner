#if TOOLS
using System;
using Godot;

namespace CardCleaner.Addons.TileEditor;

[Tool]
public partial class TileEditorDock : Control
{
    private TabContainer? _tabContainer;
    private TileAtlasPanel? _atlasPanel;
    private TilePropertiesPanel? _propertiesPanel;
    private BiomePoolPanel? _biomePoolPanel;
    private TerrainGroupsPanel? _terrainGroupsPanel;
    private TransitionRulesPanel? _transitionRulesPanel;
    private BlobSettingsPanel? _blobSettingsPanel;
    private TilePickerPopup? _tilePickerPopup;
    private TileEditorService? _service;
    private Button? _saveButton;
    private Label? _statusLabel;
    private bool _isDirty;
    private bool _initialized;

    // Tile picker context
    private string? _pendingGroupId;
    private int _pendingRuleIndex = -1;
    private int _pendingBitmask = -1;

    public override void _Ready()
    {
        try
        {
            CustomMinimumSize = new Vector2(0, 300);

            _service = new TileEditorService();
            _service.TilesLoaded += OnTilesLoaded;
            _service.TileModified += OnTileModified;
            _service.TerrainGroupModified += OnTerrainModified;
            _service.TerrainGroupAdded += OnTerrainModified;
            _service.TerrainGroupRemoved += OnTerrainModified;
            _service.TransitionRuleModified += OnTransitionModified;
            _service.TransitionRuleAdded += OnTransitionModified;
            _service.TransitionRuleRemoved += OnTransitionModified;
            _service.BlobConfigModified += OnBlobConfigModified;

            SetupUI();
            _initialized = true;

            // Defer tile loading to next frame to ensure everything is set up
            CallDeferred(MethodName.LoadTilesDeferred);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorDock] Failed to initialize: {ex.Message}");
            GD.PrintErr(ex.StackTrace);
        }
    }

    private void LoadTilesDeferred()
    {
        try
        {
            _service?.LoadTiles();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorDock] Failed to load tiles: {ex.Message}");
            if (_statusLabel != null)
                _statusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private void SetupUI()
    {
        var mainVBox = new VBoxContainer();
        mainVBox.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(mainVBox);

        // Toolbar
        var toolbar = new HBoxContainer();
        mainVBox.AddChild(toolbar);

        _saveButton = new Button { Text = "Save", Disabled = true };
        _saveButton.Pressed += OnSavePressed;
        toolbar.AddChild(_saveButton);

        var reloadButton = new Button { Text = "Reload" };
        reloadButton.Pressed += OnReloadPressed;
        toolbar.AddChild(reloadButton);

        toolbar.AddChild(new HSeparator { SizeFlagsHorizontal = SizeFlags.Expand });

        _statusLabel = new Label { Text = "Loading..." };
        toolbar.AddChild(_statusLabel);

        // Tab container
        _tabContainer = new TabContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        mainVBox.AddChild(_tabContainer);

        // Atlas tab
        _atlasPanel = new TileAtlasPanel(_service!);
        _atlasPanel.Name = "Tile Browser";
        _atlasPanel.TileSelected += OnTileSelected;
        _tabContainer.AddChild(_atlasPanel);

        // Properties tab
        _propertiesPanel = new TilePropertiesPanel(_service!);
        _propertiesPanel.Name = "Properties";
        _tabContainer.AddChild(_propertiesPanel);

        // Biomes tab
        _biomePoolPanel = new BiomePoolPanel(_service!);
        _biomePoolPanel.Name = "Biomes";
        _tabContainer.AddChild(_biomePoolPanel);

        // Terrain Groups tab
        _terrainGroupsPanel = new TerrainGroupsPanel(_service!);
        _terrainGroupsPanel.Name = "Terrain Groups";
        _terrainGroupsPanel.TilePickerRequested += OnTerrainGroupTilePickerRequested;
        _tabContainer.AddChild(_terrainGroupsPanel);

        // Transition Rules tab
        _transitionRulesPanel = new TransitionRulesPanel(_service!);
        _transitionRulesPanel.Name = "Transitions";
        _transitionRulesPanel.TilePickerRequested += OnTransitionRuleTilePickerRequested;
        _tabContainer.AddChild(_transitionRulesPanel);

        // Blob Settings tab
        _blobSettingsPanel = new BlobSettingsPanel(_service!);
        _blobSettingsPanel.Name = "Blob Settings";
        _tabContainer.AddChild(_blobSettingsPanel);

        // Tile picker popup (shared)
        _tilePickerPopup = new TilePickerPopup(_service!);
        _tilePickerPopup.TileSelected += OnTilePickerSelection;
        AddChild(_tilePickerPopup);
    }

    private void OnTilesLoaded()
    {
        _statusLabel!.Text = $"Loaded {_service!.TileCount} tiles, {_service.TerrainGroupCount} groups, {_service.TransitionRuleCount} rules";
        _isDirty = false;
        UpdateTitle();
    }

    private void OnTileModified(string tileId)
    {
        _isDirty = true;
        _saveButton!.Disabled = false;
        UpdateTitle();
    }

    private void OnTerrainModified(string groupId)
    {
        _isDirty = true;
        _saveButton!.Disabled = false;
        UpdateTitle();
    }

    private void OnTransitionModified(int ruleIndex)
    {
        _isDirty = true;
        _saveButton!.Disabled = false;
        UpdateTitle();
    }

    private void OnBlobConfigModified()
    {
        _isDirty = true;
        _saveButton!.Disabled = false;
        UpdateTitle();
    }

    private void OnTerrainGroupTilePickerRequested(string groupId)
    {
        _pendingGroupId = groupId;
        _pendingRuleIndex = -1;
        _pendingBitmask = -1;
        _tilePickerPopup?.ShowPicker();
    }

    private void OnTransitionRuleTilePickerRequested(int ruleIndex, int bitmask)
    {
        _pendingGroupId = null;
        _pendingRuleIndex = ruleIndex;
        _pendingBitmask = bitmask;
        _tilePickerPopup?.ShowPicker();
    }

    private void OnTilePickerSelection(string? tileId)
    {
        if (_pendingGroupId != null)
        {
            // Adding tile to terrain group
            if (!string.IsNullOrEmpty(tileId))
            {
                _terrainGroupsPanel?.AddTileToSelectedGroup(tileId);
            }
        }
        else if (_pendingRuleIndex >= 0 && _pendingBitmask >= 0)
        {
            // Setting edge tile for transition rule
            _transitionRulesPanel?.SetEdgeTile(_pendingRuleIndex, _pendingBitmask, tileId);
        }

        // Reset pending state
        _pendingGroupId = null;
        _pendingRuleIndex = -1;
        _pendingBitmask = -1;
    }

    private void OnTileSelected(string tileId)
    {
        _propertiesPanel?.SelectTile(tileId);
        _tabContainer!.CurrentTab = 1; // Switch to Properties tab
    }

    private void OnSavePressed()
    {
        var (success, message) = _service!.SaveTiles();
        if (success)
        {
            _isDirty = false;
            _saveButton!.Disabled = true;
            UpdateTitle();
            _statusLabel!.Text = "Saved successfully";
        }
        else
        {
            _statusLabel!.Text = $"Save failed: {message}";
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.S && keyEvent.CtrlPressed)
            {
                if (_isDirty)
                {
                    OnSavePressed();
                    GetViewport().SetInputAsHandled();
                }
            }
        }
    }

    private void OnReloadPressed()
    {
        if (_isDirty)
        {
            var dialog = new ConfirmationDialog
            {
                DialogText = "You have unsaved changes. Reload anyway?"
            };
            dialog.Confirmed += () =>
            {
                _service!.LoadTiles();
                dialog.QueueFree();
            };
            dialog.Canceled += () => dialog.QueueFree();
            AddChild(dialog);
            dialog.PopupCentered();
        }
        else
        {
            _service!.LoadTiles();
        }
    }

    private void UpdateTitle()
    {
        // The dock title is managed by the plugin, but we can update status
        if (_isDirty)
        {
            _statusLabel!.Text = $"Modified ({_service!.TileCount} tiles) *";
        }
    }

    public override void _ExitTree()
    {
        if (_isDirty)
        {
            GD.Print("[TileEditor] Warning: Unsaved changes discarded");
        }
    }
}
#endif
