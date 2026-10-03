#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TileAtlasPanel
{
    private void OnTilesLoaded()
    {
        RefreshTileDisplay();
    }

    private void OnTileModified(string tileId)
    {
        if (!_tileButtons.TryGetValue(tileId, out var button))
            return;

        var tile = _service!.GetTile(tileId);
        if (tile != null)
            button.UpdateTile(tile);
    }

    private void OnTileAdded(string tileId)
    {
        RefreshTileDisplay();
    }

    private void OnTileRemoved(string tileId)
    {
        RefreshTileDisplay();
    }

    private void RefreshTileDisplay()
    {
        if (_tileGrid == null)
            return;

        foreach (var child in _tileGrid.GetChildren())
            child.QueueFree();
        _tileButtons.Clear();
        foreach (var tile in GetFilteredTiles())
        {
            var button = new TileButton(tile, TileDisplaySize, TileDisplayPadding, _service!);
            button.Pressed += () => SelectTile(tile.Id);
            _tileGrid.AddChild(button);
            _tileButtons[tile.Id] = button;
            if (tile.Id == _selectedTileId)
                button.SetSelected(true);
        }
    }

    private IEnumerable<EditableTile> GetFilteredTiles()
    {
        var tiles = _service!.AllTiles;
        tiles = ApplySearchFilter(tiles);
        tiles = ApplyBiomeFilter(tiles);
        tiles = ApplyPassabilityFilter(tiles);
        tiles = ApplyLayerFilter(tiles);
        return tiles.OrderBy(tile => tile.Id);
    }

    private IEnumerable<EditableTile> ApplySearchFilter(IEnumerable<EditableTile> tiles)
    {
        var search = _searchBox?.Text?.ToLowerInvariant() ?? "";
        if (string.IsNullOrEmpty(search))
            return tiles;

        return tiles.Where(tile =>
            tile.Id.Contains(search, StringComparison.OrdinalIgnoreCase)
            || tile.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerable<EditableTile> ApplyBiomeFilter(IEnumerable<EditableTile> tiles)
    {
        var biomeIndex = _biomeFilter?.Selected ?? 0;
        if (biomeIndex <= 0)
            return tiles;
        if (biomeIndex == 7)
            return tiles.Where(tile => tile.Biomes.Count == 0);

        var biomes = new[]
        {
            "", "plains", "forest", "desert", "tundra", "swamp", "mountains", ""
        };
        var biome = biomes[biomeIndex];
        return tiles.Where(tile =>
            tile.Biomes.Count == 0
            || tile.Biomes.Contains(biome, StringComparer.OrdinalIgnoreCase));
    }

    private IEnumerable<EditableTile> ApplyPassabilityFilter(
        IEnumerable<EditableTile> tiles)
    {
        var passabilityIndex = _passabilityFilter?.Selected ?? 0;
        if (passabilityIndex <= 0)
            return tiles;

        var passability = passabilityIndex == 1 ? "passable" : "solid";
        return tiles.Where(tile =>
            tile.Passability.Equals(passability, StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerable<EditableTile> ApplyLayerFilter(IEnumerable<EditableTile> tiles)
    {
        var layerIndex = _layerFilter?.Selected ?? 0;
        if (layerIndex <= 0)
            return tiles;

        var layers = new[] { "", "terrain", "decoration", "structure", "effects" };
        var layer = layers[layerIndex];
        return tiles.Where(tile =>
            (string.IsNullOrEmpty(tile.Layer) ? "terrain" : tile.Layer)
                .Equals(layer, StringComparison.OrdinalIgnoreCase));
    }

    private void SelectTile(string tileId)
    {
        if (_selectedTileId != null
            && _tileButtons.TryGetValue(_selectedTileId, out var previousButton))
        {
            previousButton.SetSelected(false);
        }

        _selectedTileId = tileId;
        if (_tileButtons.TryGetValue(tileId, out var newButton))
            newButton.SetSelected(true);
        UpdateToolbarState();
        EmitSignal(SignalName.TileSelected, tileId);
    }

    private void UpdateToolbarState()
    {
        var hasSelection = _selectedTileId != null;
        if (_duplicateButton != null)
            _duplicateButton.Disabled = !hasSelection;
        if (_deleteButton != null)
            _deleteButton.Disabled = !hasSelection;
    }

    public string? SelectedTileId => _selectedTileId;
}
#endif
