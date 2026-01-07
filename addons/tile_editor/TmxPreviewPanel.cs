#if TOOLS
using System;
using System.IO;
using CardCleaner.Scripts.Core.Services;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Panel showing a preview of tiles as they resolve in an actual TMX file.
/// Loads a TMX file and displays the tiles exactly as Tiled would render them,
/// allowing visual verification that auto-tile configurations work correctly.
/// </summary>
[Tool]
public partial class TmxPreviewPanel : VBoxContainer
{
    private const string DefaultTmxDir = "res://Data/Tiled";

    private readonly TileEditorService? _service;
    private LineEdit? _tmxPathEdit;
    private Button? _browseButton;
    private Button? _reloadButton;
    private HSlider? _scaleSlider;
    private Label? _scaleLabel;
    private Label? _infoLabel;
    private TmxPreviewControl? _previewControl;
    private string? _currentTmxPath;

    // Required by Godot for [Tool] classes
    public TmxPreviewPanel() { }

    public TmxPreviewPanel(TileEditorService service)
    {
        _service = service;
    }

    public override void _Ready()
    {
        if (_service == null) return;

        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        // Header
        var header = new Label
        {
            Text = "TMX Preview",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        header.AddThemeFontSizeOverride("font_size", 16);
        AddChild(header);

        AddChild(new HSeparator());

        // TMX file path row
        var pathRow = new HBoxContainer();
        pathRow.AddChild(new Label { Text = "TMX File:", CustomMinimumSize = new Vector2(60, 0) });

        _tmxPathEdit = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = "Select a TMX file...",
            Editable = false
        };
        pathRow.AddChild(_tmxPathEdit);

        _browseButton = new Button { Text = "Browse..." };
        _browseButton.Pressed += OnBrowsePressed;
        pathRow.AddChild(_browseButton);

        _reloadButton = new Button { Text = "⟳", TooltipText = "Reload", Disabled = true };
        _reloadButton.Pressed += OnReloadPressed;
        pathRow.AddChild(_reloadButton);

        AddChild(pathRow);

        // Scale slider row
        var scaleRow = new HBoxContainer();
        scaleRow.AddChild(new Label { Text = "Scale:", CustomMinimumSize = new Vector2(60, 0) });
        _scaleSlider = new HSlider
        {
            MinValue = 1,
            MaxValue = 4,
            Step = 0.5,
            Value = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _scaleSlider.ValueChanged += OnScaleChanged;
        scaleRow.AddChild(_scaleSlider);
        _scaleLabel = new Label { Text = "2x", CustomMinimumSize = new Vector2(30, 0) };
        scaleRow.AddChild(_scaleLabel);
        AddChild(scaleRow);

        AddChild(new HSeparator());

        // Info label
        _infoLabel = new Label
        {
            Text = "Select a TMX file to preview how tiles resolve.",
            Modulate = new Color(0.8f, 0.8f, 0.8f),
            AutowrapMode = TextServer.AutowrapMode.Word
        };
        _infoLabel.AddThemeFontSizeOverride("font_size", 11);
        AddChild(_infoLabel);

        // Preview in scroll container
        var previewScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };

        _previewControl = new TmxPreviewControl(_service);
        _previewControl.InfoChanged += OnInfoChanged;
        previewScroll.AddChild(_previewControl);
        AddChild(previewScroll);

        // Auto-load first TMX file if available
        CallDeferred(MethodName.TryAutoLoadTmx);
    }

    private void TryAutoLoadTmx()
    {
        var absoluteDir = ProjectSettings.GlobalizePath(DefaultTmxDir);
        if (!Directory.Exists(absoluteDir)) return;

        var tmxFiles = Directory.GetFiles(absoluteDir, "*.tmx");
        if (tmxFiles.Length > 0)
        {
            LoadTmxFile(tmxFiles[0]);
        }
    }

    private void OnBrowsePressed()
    {
        var dialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Filters = new[] { "*.tmx ; Tiled Map Files" },
            Title = "Select TMX File"
        };

        // Set initial directory
        var absoluteDir = ProjectSettings.GlobalizePath(DefaultTmxDir);
        if (Directory.Exists(absoluteDir))
        {
            dialog.CurrentDir = absoluteDir;
        }

        dialog.FileSelected += path =>
        {
            LoadTmxFile(path);
            dialog.QueueFree();
        };

        dialog.Canceled += () => dialog.QueueFree();

        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(600, 400));
    }

    private void OnReloadPressed()
    {
        if (!string.IsNullOrEmpty(_currentTmxPath))
        {
            LoadTmxFile(_currentTmxPath);
        }
    }

    private void LoadTmxFile(string path)
    {
        _currentTmxPath = path;
        _tmxPathEdit!.Text = Path.GetFileName(path);
        _reloadButton!.Disabled = false;

        var mapData = TiledTilesetLoader.LoadTmxMap(path);
        if (mapData == null)
        {
            _infoLabel!.Text = $"Failed to load TMX file: {path}";
            _previewControl?.ClearPreview();
            return;
        }

        _previewControl?.LoadTmxMap(mapData, (float)_scaleSlider!.Value);
    }

    private void OnScaleChanged(double value)
    {
        _scaleLabel!.Text = $"{value:F1}x";
        _previewControl?.SetScale((float)value);
    }

    private void OnInfoChanged(string info)
    {
        if (_infoLabel != null)
            _infoLabel.Text = info;
    }

    public void Refresh()
    {
        if (!string.IsNullOrEmpty(_currentTmxPath))
        {
            LoadTmxFile(_currentTmxPath);
        }
    }
}

/// <summary>
/// Control that renders tiles from a loaded TMX map.
/// Displays the actual tile resolution from the TMX data.
/// </summary>
[Tool]
public partial class TmxPreviewControl : Control
{
    [Signal]
    public delegate void InfoChangedEventHandler(string info);

    private readonly TileEditorService? _service;
    private TiledTilesetLoader.TmxMapData? _mapData;
    private float _scale = 2f;
    private Vector2I _tileSize = new(16, 16);

    // Required by Godot for [Tool] classes
    public TmxPreviewControl() { }

    public TmxPreviewControl(TileEditorService service)
    {
        _service = service;
        TextureFilter = TextureFilterEnum.Nearest;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void LoadTmxMap(TiledTilesetLoader.TmxMapData mapData, float scale)
    {
        _mapData = mapData;
        _scale = scale;
        _tileSize = _service?.TileSet?.TileSize ?? new Vector2I(16, 16);
        UpdateSize();
        QueueRedraw();
        EmitInfo();
    }

    public void SetScale(float scale)
    {
        _scale = scale;
        UpdateSize();
        QueueRedraw();
    }

    public void ClearPreview()
    {
        _mapData = null;
        UpdateSize();
        QueueRedraw();
        EmitSignal(SignalName.InfoChanged, "No TMX file loaded.");
    }

    private void UpdateSize()
    {
        if (_mapData == null)
        {
            CustomMinimumSize = Vector2.Zero;
            return;
        }

        var bounds = _mapData.GetBounds();
        var width = bounds.Max.X - bounds.Min.X + 1;
        var height = bounds.Max.Y - bounds.Min.Y + 1;

        var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;
        CustomMinimumSize = new Vector2(
            width * scaledTileSize.X,
            height * scaledTileSize.Y
        );
    }

    private void EmitInfo()
    {
        if (_mapData == null)
        {
            EmitSignal(SignalName.InfoChanged, "No TMX file loaded.");
            return;
        }

        var bounds = _mapData.GetBounds();
        var tileCount = 0;
        foreach (var _ in _mapData.GetAllTiles())
            tileCount++;

        var info = $"TMX loaded: {_mapData.Tilesets.Count} tileset(s), {tileCount} tiles. " +
                   $"Bounds: ({bounds.Min.X},{bounds.Min.Y}) to ({bounds.Max.X},{bounds.Max.Y}). " +
                   "Hover for tile info.";
        EmitSignal(SignalName.InfoChanged, info);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_mapData == null) return;

        if (@event is InputEventMouseMotion mouseMotion)
        {
            UpdateTooltip(mouseMotion.Position);
        }
    }

    private void UpdateTooltip(Vector2 localPos)
    {
        if (_mapData == null)
        {
            TooltipText = "";
            return;
        }

        var bounds = _mapData.GetBounds();
        var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;

        // Convert screen position to tile coordinates
        var tileX = bounds.Min.X + (int)(localPos.X / scaledTileSize.X);
        var tileY = bounds.Min.Y + (int)(localPos.Y / scaledTileSize.Y);

        var resolution = _mapData.GetTileAt(tileX, tileY);
        if (resolution != null)
        {
            var tooltip = $"Position: ({tileX}, {tileY})\n";
            tooltip += $"Global ID: {resolution.GlobalTileId}\n";
            tooltip += $"Local ID: {resolution.LocalTileId}\n";
            tooltip += $"Atlas: ({resolution.AtlasCoords.X}, {resolution.AtlasCoords.Y})\n";
            tooltip += $"Tileset: {Path.GetFileName(resolution.Tileset.TsxPath)}";

            if (resolution.TileDefinition != null)
            {
                var def = resolution.TileDefinition;
                tooltip += $"\n\nTile: {def.Id}";
                if (!string.IsNullOrEmpty(def.AutoTileFormatName))
                    tooltip += $"\nFormat: {def.AutoTileFormatName}";
            }

            TooltipText = tooltip;
        }
        else
        {
            TooltipText = $"Position: ({tileX}, {tileY})\nEmpty";
        }
    }

    public override void _Draw()
    {
        if (_service == null || _mapData == null)
            return;

        var bounds = _mapData.GetBounds();
        var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;

        // Draw background
        var totalSize = CustomMinimumSize;
        DrawRect(new Rect2(Vector2.Zero, totalSize), new Color(0.15f, 0.15f, 0.15f, 1f));

        // Draw all tiles from the TMX
        foreach (var (coord, resolution) in _mapData.GetAllTiles())
        {
            // Get the tileset's texture
            var tileset = resolution.Tileset;
            if (tileset.TilesetData.Tiles.Count == 0)
                continue;

            // Try to get texture from the first tile in the tileset
            var firstTile = tileset.TilesetData.Tiles[0];
            var texture = _service.GetTileTexture(new EditableTile { SourceId = firstTile.SourceId });
            if (texture == null)
            {
                // Fallback: try to get any texture from the service
                foreach (var tile in _service.AllTiles)
                {
                    texture = _service.GetTileTexture(tile);
                    if (texture != null) break;
                }
            }

            if (texture == null) continue;

            // Calculate positions
            var screenX = (coord.X - bounds.Min.X) * scaledTileSize.X;
            var screenY = (coord.Y - bounds.Min.Y) * scaledTileSize.Y;

            // Calculate source rect from atlas coords
            // Note: TMX uses flat tile IDs, we need to convert to atlas coords
            var atlasCoords = resolution.AtlasCoords;
            // Source scale comes from TSX tile width vs our target tile size
            // For now, use 1.0 as we assume TSX tiles match our runtime size
            var actualTileSize = _tileSize;

            var srcRect = new Rect2(
                atlasCoords.X * actualTileSize.X,
                atlasCoords.Y * actualTileSize.Y,
                actualTileSize.X,
                actualTileSize.Y
            );

            var destRect = new Rect2(screenX, screenY, scaledTileSize.X, scaledTileSize.Y);

            DrawTextureRectRegion(texture, destRect, srcRect);
        }

        // Draw grid overlay
        var gridColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);
        var width = bounds.Max.X - bounds.Min.X + 1;
        var height = bounds.Max.Y - bounds.Min.Y + 1;

        for (var x = 0; x <= width; x++)
        {
            DrawLine(
                new Vector2(x * scaledTileSize.X, 0),
                new Vector2(x * scaledTileSize.X, height * scaledTileSize.Y),
                gridColor
            );
        }

        for (var y = 0; y <= height; y++)
        {
            DrawLine(
                new Vector2(0, y * scaledTileSize.Y),
                new Vector2(width * scaledTileSize.X, y * scaledTileSize.Y),
                gridColor
            );
        }

        // Draw border
        DrawRect(new Rect2(Vector2.Zero, totalSize), new Color(0.7f, 0.7f, 0.7f, 0.5f), false, 2.0f);
    }
}
#endif
