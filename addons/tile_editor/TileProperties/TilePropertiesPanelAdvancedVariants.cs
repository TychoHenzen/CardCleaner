#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TilePropertiesPanel
{
    private void OnAdvancedVariantsToggled(bool pressed)
    {
        if (_isUpdating || _currentTile == null)
            return;

        _variantMappingEditor!.Visible = pressed;
        if (pressed)
        {
            var formatName = _currentTile.AutoTileFormat ?? "corner16";
            var format = _service!.GetFormatDefinition(formatName);
            _variantMappingEditor.Configure(_currentTile, format, false);
        }
        else
        {
            _currentTile.CustomVariantDefinitions?.Clear();
            _service!.UpdateTile(_currentTile);
        }
    }

    private void OnVariantMappingsModified()
    {
        if (_isUpdating || _currentTile == null || _variantMappingEditor == null)
            return;

        _currentTile.CustomVariantDefinitions = _variantMappingEditor.GetVariantDefinitions();
        SyncVariantDefinitionsToAutoTileVariants();
        _service!.UpdateTile(_currentTile);
    }

    private void SyncVariantDefinitionsToAutoTileVariants()
    {
        if (_currentTile?.CustomVariantDefinitions == null)
            return;

        _currentTile.AutoTileVariants ??= new Vector2I?[_currentVariantCount];
        foreach (var (bitmask, definition) in _currentTile.CustomVariantDefinitions)
        {
            if (bitmask < _currentTile.AutoTileVariants.Length)
                _currentTile.AutoTileVariants[bitmask] = definition.AtlasCoords;
        }
    }

    private void UpdateAdvancedVariantsUI()
    {
        if (_currentTile == null || _useAdvancedVariantsCheckbox == null
            || _variantMappingEditor == null)
        {
            return;
        }

        var hasAdvancedVariants = _currentTile.HasCustomVariantDefinitions;
        _useAdvancedVariantsCheckbox.ButtonPressed = hasAdvancedVariants;
        _variantMappingEditor.Visible = hasAdvancedVariants;
        if (!hasAdvancedVariants)
            return;

        var formatName = _currentTile.AutoTileFormat ?? "corner16";
        var format = _service!.GetFormatDefinition(formatName);
        _variantMappingEditor.Configure(_currentTile, format, false);
    }
}
#endif
