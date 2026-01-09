#if TOOLS
using System;
using System.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

[Tool]
public partial class TileEditorDock : Control
{
    private TileAtlasPanel? _atlasPanel;
    private BiomePoolPanel? _biomePoolPanel;
    private AutoTilePreviewPanel? _autoTilePreviewPanel;
    private TmxPreviewPanel? _tmxPreviewPanel;
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

        toolbar.AddChild(new VSeparator());

        // TMX tools
        var compileTmxButton = new Button { Text = "Compile from TMX", TooltipText = "Compile atlas from TMX/TSX files" };
        compileTmxButton.Pressed += OnCompileTmxPressed;
        toolbar.AddChild(compileTmxButton);

        var insertPropsButton = new Button { Text = "Insert TSX Props", TooltipText = "Insert default properties into TSX files" };
        insertPropsButton.Pressed += OnInsertTsxPropsPressed;
        toolbar.AddChild(insertPropsButton);

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

        // TMX Preview tab
        _tmxPreviewPanel = new TmxPreviewPanel(_service!);
        _tmxPreviewPanel.Name = "TMX Preview";
        _tabContainer.AddChild(_tmxPreviewPanel);

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
        _propertiesPanel?.RefreshFormatDropdown();
        _isDirty = true;
        _saveButton!.Disabled = false;
        UpdateTitle();
    }

    private void OnFormatCreated(string formatName)
    {
        SyncCustomFormatsToService();
        _propertiesPanel?.RefreshFormatDropdown();
        _isDirty = true;
        _saveButton!.Disabled = false;
        UpdateTitle();
    }

    private void OnFormatDeleted(string formatName)
    {
        SyncCustomFormatsToService();
        _propertiesPanel?.RefreshFormatDropdown();
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

    private void OnCompileTmxPressed()
    {
        const string tiledDir = "res://Data/Tiled";
        _statusLabel!.Text = "Compiling from TMX...";

        // Run compilation asynchronously to avoid blocking UI
        CallDeferred(MethodName.DoCompileTmx, tiledDir);
    }

    private void DoCompileTmx(string tiledDir)
    {
        var compiler = new TmxAtlasCompiler();
        var (success, message) = compiler.CompileFromTmx(tiledDir);

        if (success)
        {
            _statusLabel!.Text = "TMX compilation complete!";
            GD.Print($"[TileEditorDock] {message}");

            // Show success dialog
            var dialog = new AcceptDialog { DialogText = message, Title = "TMX Compilation Complete" };
            dialog.Confirmed += () => dialog.QueueFree();
            AddChild(dialog);
            dialog.PopupCentered();
        }
        else
        {
            _statusLabel!.Text = $"TMX compilation failed: {message}";
            GD.PrintErr($"[TileEditorDock] TMX compilation failed: {message}");

            // Show error dialog
            var dialog = new AcceptDialog { DialogText = $"Compilation failed:\n{message}", Title = "TMX Compilation Error" };
            dialog.Confirmed += () => dialog.QueueFree();
            AddChild(dialog);
            dialog.PopupCentered();
        }
    }

    private void OnInsertTsxPropsPressed()
    {
        const string tiledDir = "res://Data/Tiled";

        // First analyze what would be changed
        var reports = TsxPropertyInserter.AnalyzePropertiesInDirectory(tiledDir);
        var filesWithMissing = reports.Count(r => r.HasMissingProperties);

        if (filesWithMissing == 0)
        {
            var noChangesDialog = new AcceptDialog
            {
                DialogText = "All TSX files already have the required properties.",
                Title = "No Changes Needed"
            };
            noChangesDialog.Confirmed += () => noChangesDialog.QueueFree();
            AddChild(noChangesDialog);
            noChangesDialog.PopupCentered();
            return;
        }

        // Confirm with user
        var dialog = new ConfirmationDialog
        {
            DialogText = $"Insert default properties into {filesWithMissing} TSX file(s)?",
            Title = "Insert TSX Properties"
        };
        dialog.Confirmed += () =>
        {
            DoInsertTsxProps(tiledDir);
            dialog.QueueFree();
        };
        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void DoInsertTsxProps(string tiledDir)
    {
        _statusLabel!.Text = "Inserting TSX properties...";

        var (success, message, filesModified, totalTiles, totalWangSets) =
            TsxPropertyInserter.InsertPropertiesInDirectory(tiledDir);

        if (success)
        {
            var resultMessage = $"Updated {filesModified} file(s):\n" +
                               $"- {totalTiles} tile(s) modified\n" +
                               $"- {totalWangSets} wang set(s) modified";
            _statusLabel!.Text = $"Inserted properties into {filesModified} files";
            GD.Print($"[TileEditorDock] TSX properties inserted: {resultMessage}");

            var successDialog = new AcceptDialog { DialogText = resultMessage, Title = "Properties Inserted" };
            successDialog.Confirmed += () => successDialog.QueueFree();
            AddChild(successDialog);
            successDialog.PopupCentered();
        }
        else
        {
            _statusLabel!.Text = $"Property insertion failed: {message}";
            GD.PrintErr($"[TileEditorDock] TSX property insertion failed: {message}");

            var errorDialog = new AcceptDialog { DialogText = $"Failed:\n{message}", Title = "Error" };
            errorDialog.Confirmed += () => errorDialog.QueueFree();
            AddChild(errorDialog);
            errorDialog.PopupCentered();
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
