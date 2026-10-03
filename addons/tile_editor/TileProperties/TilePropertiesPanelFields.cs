#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TilePropertiesPanel
{
    public void SelectTile(string tileId)
    {
        if (_service == null)
            return;

        _selectedTileId = tileId;
        _currentTile = _service.GetTile(tileId)?.Clone();
        if (_currentTile == null)
        {
            SetFieldsEnabled(false);
            return;
        }

        PopulateSourceDropdown();
        SetFieldsEnabled(true);
        PopulateFields();
    }

    private void PopulateFields()
    {
        if (_currentTile == null)
            return;

        _isUpdating = true;
        PopulateIdentityFields();
        PopulateAtlasFields();
        PopulateGeneralFields();
        PopulateAutoTileFields();
        PopulateTerrainFields();
        PopulateDecorationDensity();
        PopulateVariationFields();
        PopulateAnimationFields();
        UpdateAdvancedVariantsUI();
        _validationLabel!.Text = "";
        _isUpdating = false;
        UpdateSectionVisibility();
    }

    private void PopulateIdentityFields()
    {
        _idField!.Text = _currentTile!.Id;
        _idField.Editable = false;
        _nameField!.Text = _currentTile.Name;
        _descriptionField!.Text = _currentTile.Description ?? "";
        _passabilityField!.Selected = _currentTile.Passability.ToLowerInvariant() switch
        {
            "solid" => 1,
            "partially_passable" => 2,
            _ => 0
        };
    }

    private void PopulateAtlasFields()
    {
        for (var i = 0; i < _sourceDropdown!.ItemCount; i++)
        {
            if (_sourceDropdown.GetItemId(i) == _currentTile!.SourceId)
            {
                _sourceDropdown.Selected = i;
                break;
            }
        }

        _sourceIdField!.Value = _currentTile!.SourceId;
        UpdateSourceButtonText();
        UpdateAtlasButtonAppearance();
    }

    private void PopulateGeneralFields()
    {
        _tileModeDropdown!.Selected = _currentTile!.TileMode?.ToLowerInvariant() switch
        {
            "pertilevariations" => 1,
            "permapvariations" => 2,
            "autotile" => 3,
            "permapvariationautotile" => 4,
            "animated" => 5,
            _ => 0
        };
        _layerField!.Selected = _currentTile.Layer.ToLowerInvariant() switch
        {
            "decoration" => 1,
            "structure" => 2,
            "effects" => 3,
            _ => 0
        };
        _elevationField!.Value = _currentTile.Elevation;
        _transparentField!.ButtonPressed = _currentTile.IsTransparent;
        _sizeXField!.Value = _currentTile.SizeX;
        _sizeYField!.Value = _currentTile.SizeY;
        _sourceScaleDropdown!.Selected = _currentTile.SourceScale switch
        {
            0.5f => 0,
            2.0f => 2,
            _ => 1
        };

        foreach (var (biome, checkbox) in _biomeCheckboxes)
        {
            checkbox.ButtonPressed = _currentTile.Biomes.Contains(
                biome,
                StringComparer.OrdinalIgnoreCase);
        }
    }

    private void PopulateAutoTileFields()
    {
        var currentFormat = _currentTile!.AutoTileFormat ?? "corner16";
        _autoTileFormatDropdown!.Selected = GetFormatDropdownIndex(currentFormat);
        UpdateFormatDescription(currentFormat);
        var format = _service!.GetFormatDefinition(currentFormat);
        var variantCount = format?.AllowedBitmasks.Count ?? 16;
        RebuildVariantGrid(variantCount, currentFormat);
    }

    private void PopulateTerrainFields()
    {
        PopulateTerrainDropdowns();
        SelectInnerTerrain();
        SelectOuterTerrain();
    }

    private void SelectInnerTerrain()
    {
        if (string.IsNullOrEmpty(_currentTile!.InnerTerrainId))
        {
            _innerTerrainDropdown!.Selected = 0;
            return;
        }

        var terrainTiles = GetTerrainTiles();
        var index = terrainTiles.FindIndex(t => t.Id == _currentTile.InnerTerrainId);
        _innerTerrainDropdown!.Selected = index >= 0 ? index + 1 : 0;
    }

    private void SelectOuterTerrain()
    {
        if (string.IsNullOrEmpty(_currentTile!.OuterTerrainId))
        {
            _outerTerrainDropdown!.Selected = 0;
            return;
        }

        if (_currentTile.OuterTerrainId == "*")
        {
            _outerTerrainDropdown!.Selected = 1;
            return;
        }

        var terrainTiles = GetTerrainTiles();
        var index = terrainTiles.FindIndex(t => t.Id == _currentTile.OuterTerrainId);
        _outerTerrainDropdown!.Selected = index >= 0 ? index + 2 : 0;
    }

    private List<EditableTile> GetTerrainTiles()
    {
        return _service!.AllTiles
            .Where(t => t.Layer == "terrain" && !t.HasAutoTileVariants)
            .OrderBy(t => t.Name)
            .ToList();
    }

    private void PopulateDecorationDensity()
    {
        var isDecorationLayer = _currentTile!.Layer.ToLowerInvariant() == "decoration";
        _decorationDensityRow!.Visible = isDecorationLayer;
        _decorationDensityField!.Value = _currentTile.DecorationDensity * 100;
    }

    private void PopulateVariationFields()
    {
        _variationModeDropdown!.Selected = _currentTile!.VariationMode?.ToLowerInvariant() switch
        {
            "pergeneration" or "per_generation" => 1,
            _ => 0
        };
        RebuildVariationsList();
    }

    private void PopulateAnimationFields()
    {
        _animationFrameDurationField!.Value = _currentTile!.AnimationFrameDuration;
        RebuildAnimationFramesList();
    }

    private void OnDescriptionChanged()
    {
        if (_isUpdating || _currentTile == null)
            return;

        _currentTile.Description = string.IsNullOrWhiteSpace(_descriptionField!.Text)
            ? null
            : _descriptionField.Text;
        _service!.UpdateTile(_currentTile);
    }

    private void OnFieldChanged(string _)
    {
        if (_isUpdating || _currentTile == null)
            return;

        UpdateEditableFields();
        UpdateDecorationField();
        UpdateSelectedBiomes();
        ValidateAndSaveFields();
    }

    private void UpdateEditableFields()
    {
        _currentTile!.Name = _nameField!.Text;
        _currentTile.Passability = _passabilityField!.Selected switch
        {
            1 => "solid",
            2 => "partially_passable",
            _ => "passable"
        };
        _currentTile.SourceId = (int)_sourceIdField!.Value;
        _currentTile.Layer = _layerField!.Selected switch
        {
            1 => "decoration",
            2 => "structure",
            3 => "effects",
            _ => "terrain"
        };
        _currentTile.Elevation = (float)_elevationField!.Value;
        _currentTile.IsTransparent = _transparentField!.ButtonPressed;
        _currentTile.SizeX = (int)_sizeXField!.Value;
        _currentTile.SizeY = (int)_sizeYField!.Value;
        _currentTile.SourceScale = _sourceScaleDropdown!.Selected switch
        {
            0 => 0.5f,
            2 => 2.0f,
            _ => 1.0f
        };
    }

    private void UpdateDecorationField()
    {
        var isDecorationLayer = _currentTile!.Layer.ToLowerInvariant() == "decoration";
        _decorationDensityRow!.Visible = isDecorationLayer;
        _currentTile.DecorationDensity = (float)(_decorationDensityField!.Value / 100.0);
    }

    private void UpdateSelectedBiomes()
    {
        _currentTile!.Biomes.Clear();
        foreach (var (biome, checkbox) in _biomeCheckboxes)
        {
            if (checkbox.ButtonPressed)
                _currentTile.Biomes.Add(biome);
        }
    }

    private void ValidateAndSaveFields()
    {
        var (valid, message) = _service!.ValidateTile(_currentTile!);
        _validationLabel!.Text = valid ? "" : message;
        if (valid)
            _service.UpdateTile(_currentTile!);
    }

    private void SetFieldsEnabled(bool enabled)
    {
        _idField!.Editable = false;
        _nameField!.Editable = enabled;
        _descriptionField!.Editable = enabled;
        _tileModeDropdown!.Disabled = !enabled;
        _passabilityField!.Disabled = !enabled;
        _sourceDropdown!.Disabled = !enabled;
        _sourcePickerButton!.Disabled = !enabled;
        _atlasCoordButton!.Disabled = !enabled;
        _layerField!.Disabled = !enabled;
        _elevationField!.Editable = enabled;
        _transparentField!.Disabled = !enabled;
        _sizeXField!.Editable = enabled;
        _sizeYField!.Editable = enabled;
        _sourceScaleDropdown!.Disabled = !enabled;
        foreach (var checkbox in _biomeCheckboxes.Values)
            checkbox.Disabled = !enabled;
    }
}
#endif
