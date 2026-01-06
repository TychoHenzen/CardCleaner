#if TOOLS
using System;
using Godot;

namespace CardCleaner.Addons.TileEditor;

[Tool]
public partial class TileEditorDock : Control
{
    private TileAtlasPanel? _atlasPanel;
    private BiomePoolPanel? _biomePoolPanel;
    private AutoTilePreviewPanel? _autoTilePreviewPanel;
    private AutoTileFormatEditorPanel? _autoTileFormatEditorPanel;
    private UnusedSourcesPanel? _unusedSourcesPanel;
    private TransitionCoveragePanel? _transitionCoveragePanel;
    private bool _initialized;
    private bool _isDirty;
    private TilePropertiesPanel? _propertiesPanel;
    private Button? _saveButton;
    private TileEditorService? _service;
    private Label? _statusLabel;
    private TabContainer? _tabContainer;

    public override void _Ready()
    {
        try
        {
            CustomMinimumSize = new Vector2(0, 300);

            _service = new TileEditorService();
            _service.TilesLoaded += OnTilesLoaded;
            _service.TileModified += OnTileModified;

            SetupUI();
            _initialized = true;

            // Defer tile loading to next frame to ensure everything is set up
            CallDeferred(MethodName.LoadTilesDeferred);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorDock] Failed to initialize: {ex.Message}");
            GD.PrintErr(ex.StackTrace);
            ShowErrorState($"Init failed: {ex.Message}");
        }
    }

    private void ShowErrorState(string message)
    {
        // Create a simple error display if the main UI failed to load
        if (GetChildCount() == 0)
        {
            var errorLabel = new Label
            {
                Text = $"Tile Editor Error:\n{message}\n\nTry: Build > Rebuild Solution, then reload the plugin.",
                AutowrapMode = TextServer.AutowrapMode.Word,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            errorLabel.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(errorLabel);
        }
    }

    private void LoadTilesDeferred()
    {
        try
        {
            if (_service == null)
            {
                GD.PrintErr("[TileEditorDock] Service is null - reinitializing");
                ReinitializeService();
            }
            _service?.LoadTiles();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorDock] Failed to load tiles: {ex.Message}");
            if (_statusLabel != null)
                _statusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private void ReinitializeService()
    {
        try
        {
            _service = new TileEditorService();
            _service.TilesLoaded += OnTilesLoaded;
            _service.TileModified += OnTileModified;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorDock] Failed to reinitialize service: {ex.Message}");
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
            SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill
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

        // Auto-Tile Preview tab
        _autoTilePreviewPanel = new AutoTilePreviewPanel(_service!);
        _autoTilePreviewPanel.Name = "Auto-Tile Preview";
        _tabContainer.AddChild(_autoTilePreviewPanel);

        // Auto-Tile Formats tab
        _autoTileFormatEditorPanel = new AutoTileFormatEditorPanel(_service!);
        _autoTileFormatEditorPanel.Name = "Formats";
        _autoTileFormatEditorPanel.FormatModified += OnFormatModified;
        _autoTileFormatEditorPanel.FormatCreated += OnFormatCreated;
        _autoTileFormatEditorPanel.FormatDeleted += OnFormatDeleted;
        _tabContainer.AddChild(_autoTileFormatEditorPanel);

        // Unused Sources tab
        _unusedSourcesPanel = new UnusedSourcesPanel(_service!);
        _unusedSourcesPanel.Name = "Unused Sources";
        _tabContainer.AddChild(_unusedSourcesPanel);

        // Transition Coverage tab
        _transitionCoveragePanel = new TransitionCoveragePanel(_service!);
        _transitionCoveragePanel.Name = "Transitions";
        _tabContainer.AddChild(_transitionCoveragePanel);
    }

    private void OnTilesLoaded()
    {
        _statusLabel!.Text = $"Loaded {_service!.TileCount} tiles";
        _isDirty = false;
        UpdateTitle();
        _autoTilePreviewPanel?.Refresh();
    }

    private void OnTileModified(string tileId)
    {
        _isDirty = true;
        _saveButton!.Disabled = false;
        UpdateTitle();
    }

    private void OnTileSelected(string tileId)
    {
        _propertiesPanel?.SelectTile(tileId);
        _tabContainer!.CurrentTab = 1; // Switch to Properties tab
    }

    private void OnFormatModified(string formatName)
    {
        SyncCustomFormatsToService();
        _isDirty = true;
        _saveButton!.Disabled = false;
        UpdateTitle();
    }

    private void OnFormatCreated(string formatName)
    {
        SyncCustomFormatsToService();
        _isDirty = true;
        _saveButton!.Disabled = false;
        UpdateTitle();
    }

    private void OnFormatDeleted(string formatName)
    {
        SyncCustomFormatsToService();
        _isDirty = true;
        _saveButton!.Disabled = false;
        UpdateTitle();
    }

    private void SyncCustomFormatsToService()
    {
        if (_autoTileFormatEditorPanel == null || _service == null) return;
        _service.UpdateCustomAutoTileFormats(_autoTileFormatEditorPanel.GetCustomFormats());
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
            var dialog = new ConfirmationDialog { DialogText = "You have unsaved changes. Reload anyway?" };
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
