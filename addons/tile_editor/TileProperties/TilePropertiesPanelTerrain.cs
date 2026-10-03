#if TOOLS
using System;
using System.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TilePropertiesPanel
{
    private void OnAutoTileFormatChanged(long index)
    {
        if (_isUpdating || _currentTile == null || _autoTileFormatDropdown == null)
            return;

        var newFormat = _autoTileFormatDropdown.GetItemMetadata((int)index).AsString();
        if (string.IsNullOrEmpty(newFormat))
            newFormat = "corner16";
        if (string.Equals(
                _currentTile.AutoTileFormat,
                newFormat,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _currentTile.AutoTileFormat = newFormat;
        UpdateFormatDescription(newFormat);
        var format = _service!.GetFormatDefinition(newFormat);
        var variantCount = format?.AllowedBitmasks.Count ?? 16;
        ResizeVariantArray(variantCount);
        _currentTile.CustomVariantDefinitions?.Clear();
        RebuildVariantGrid(variantCount, newFormat);
        _service.UpdateTile(_currentTile);
    }

    private void ResizeVariantArray(int variantCount)
    {
        if (_currentTile!.AutoTileVariants == null)
            return;

        var oldVariants = _currentTile.AutoTileVariants;
        _currentTile.AutoTileVariants = new Vector2I?[variantCount];
        Array.Copy(
            oldVariants,
            _currentTile.AutoTileVariants,
            Math.Min(oldVariants.Length, variantCount));
    }

    private void PopulateTerrainDropdowns()
    {
        if (_innerTerrainDropdown == null || _outerTerrainDropdown == null)
            return;

        _innerTerrainDropdown.Clear();
        _outerTerrainDropdown.Clear();
        var terrainTiles = GetTerrainTiles();
        _innerTerrainDropdown.AddItem("(Default - use this tile's ID)", 0);
        for (var i = 0; i < terrainTiles.Count; i++)
        {
            _innerTerrainDropdown.AddItem(
                $"{terrainTiles[i].Name} ({terrainTiles[i].Id})",
                i + 1);
        }

        _outerTerrainDropdown.AddItem("(None - no transition)", 0);
        _outerTerrainDropdown.AddItem("* (Compositable - transparent)", 1);
        for (var i = 0; i < terrainTiles.Count; i++)
        {
            _outerTerrainDropdown.AddItem(
                $"{terrainTiles[i].Name} ({terrainTiles[i].Id})",
                i + 2);
        }
    }

    private void OnInnerTerrainChanged(long index)
    {
        if (_isUpdating || _currentTile == null)
            return;

        if (index == 0)
        {
            _currentTile.InnerTerrainId = null;
        }
        else
        {
            var terrainTiles = GetTerrainTiles();
            if (index - 1 < terrainTiles.Count)
                _currentTile.InnerTerrainId = terrainTiles[(int)index - 1].Id;
        }

        _service!.UpdateTile(_currentTile);
    }

    private void OnOuterTerrainChanged(long index)
    {
        if (_isUpdating || _currentTile == null)
            return;

        if (index == 0)
        {
            _currentTile.OuterTerrainId = null;
        }
        else if (index == 1)
        {
            _currentTile.OuterTerrainId = "*";
        }
        else
        {
            var terrainTiles = GetTerrainTiles();
            if (index - 2 < terrainTiles.Count)
                _currentTile.OuterTerrainId = terrainTiles[(int)index - 2].Id;
        }

        _service!.UpdateTile(_currentTile);
    }
}
#endif
