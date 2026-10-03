#if TOOLS
using System.IO;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Core.Services.TilesetLoading;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TmxPreviewPanel
{
    private void TryAutoLoadTmx()
    {
        var absoluteDirectory = ProjectSettings.GlobalizePath(DefaultTmxDir);
        if (!Directory.Exists(absoluteDirectory))
            return;

        var tmxFiles = Directory.GetFiles(absoluteDirectory, "*.tmx");
        if (tmxFiles.Length > 0)
            LoadTmxFile(tmxFiles[0]);
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
        var absoluteDirectory = ProjectSettings.GlobalizePath(DefaultTmxDir);
        if (Directory.Exists(absoluteDirectory))
            dialog.CurrentDir = absoluteDirectory;

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
            LoadTmxFile(_currentTmxPath);
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
            _currentMapData = null;
            PopulateAutoTileSelector();
            return;
        }

        _currentMapData = mapData;
        PopulateAutoTileSelector();
        PopulateBaseTileSelector();
        ClearTileSelections();
        _previewControl?.LoadTmxMap(mapData, (float)_scaleSlider!.Value);
    }

    private void ClearTileSelections()
    {
        _selectedAutoTile = null;
        _selectedAutoTileTileset = null;
        _selectedBaseTile = null;
        _selectedBaseTileTileset = null;
        _autoTileSelector!.Select(0);
        _baseTileSelector!.Select(0);
    }
}
#endif
