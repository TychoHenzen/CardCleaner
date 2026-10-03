#if TOOLS
using System;
using Godot;

namespace CardCleaner.Addons.TileEditor;

[Tool]
public partial class TileEditorDock : Control
{
    private TileEditorPanels? _panels;
    private TileEditorToolCommands? _toolCommands;
    private bool _initialized;
    private bool _isDirty;
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

        SetupToolbar(mainVBox);

        _tabContainer = new TabContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        mainVBox.AddChild(_tabContainer);

        _panels = new TileEditorPanels(_service!);
        _panels.Atlas.TileSelected += OnTileSelected;
        _panels.FormatEditor.FormatModified += OnFormatChanged;
        _panels.FormatEditor.FormatCreated += OnFormatChanged;
        _panels.FormatEditor.FormatDeleted += OnFormatChanged;
        _panels.AddTo(_tabContainer);
    }

    private void SetupToolbar(Control parent)
    {
        var toolbar = TileEditorToolbar.CreateIn(parent);

        _saveButton = toolbar.SaveButton;
        _saveButton.Pressed += OnSavePressed;
        toolbar.ReloadButton.Pressed += OnReloadPressed;

        _compileTmxButton = toolbar.CompileTmxButton;
        _compileTmxButton.Pressed += OnCompileTmxPressed;
        _insertPropsButton = toolbar.InsertPropsButton;
        _insertPropsButton.Pressed += OnInsertTsxPropsPressed;

        _statusLabel = toolbar.StatusLabel;
        _toolCommands = new TileEditorToolCommands(_statusLabel, ShowToolDialog);
    }

    private void OnTilesLoaded()
    {
        _statusLabel!.Text = $"Loaded {_service!.TileCount} tiles";
        _isDirty = false;
        UpdateTitle();
        _panels?.AutoTilePreview.Refresh();
    }

    private void OnTileModified(string tileId)
    {
        _isDirty = true;
        _saveButton!.Disabled = false;
        UpdateTitle();
    }

    private void OnTileSelected(string tileId)
    {
        _panels?.Properties.SelectTile(tileId);
        _tabContainer!.CurrentTab = 1; // Switch to Properties tab
    }

    private void OnFormatChanged(string formatName)
    {
        SyncCustomFormatsToService();
        _panels?.Properties.RefreshFormatDropdown();
        _isDirty = true;
        _saveButton!.Disabled = false;
        UpdateTitle();
    }

    private void SyncCustomFormatsToService()
    {
        if (_panels == null || _service == null) return;
        _service.UpdateCustomAutoTileFormats(_panels.FormatEditor.GetCustomFormats());
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
        _statusLabel!.Text = "Compiling from TMX...";

        // Run compilation asynchronously to avoid blocking UI
        CallDeferred(MethodName.DoCompileTmx, TiledDirectory);
    }

    private void DoCompileTmx(string tiledDir)
    {
        _toolCommands!.CompileTmx(tiledDir);
    }

    private void OnInsertTsxPropsPressed()
    {
        _toolCommands!.ConfirmInsertTsxProps(TiledDirectory);
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
