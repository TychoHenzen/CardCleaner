#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class VariantCell
{
    private void LoadFromTile()
    {
        if (_tile == null)
            return;

        _currentDefinition = _tile.GetVariantDefinition(_bitmask);
        var hasCustomDefinition = _tile.CustomVariantDefinitions?.ContainsKey(_bitmask) ?? false;
        if (!hasCustomDefinition &&
            _format != null &&
            _format.VariantMappings.TryGetValue(_bitmask, out var formatVariant))
        {
            _currentDefinition.SizeX = formatVariant.Size.X > 0 ? formatVariant.Size.X : 1;
            _currentDefinition.SizeY = formatVariant.Size.Y > 0 ? formatVariant.Size.Y : 1;
        }

        UpdateUIFromDefinition();
    }

    private void UpdateUIFromDefinition()
    {
        _isUpdating = true;

        _atlasButton!.Text = $"Atlas: ({_currentDefinition.AtlasX}, {_currentDefinition.AtlasY})";
        _sizeXSpin!.Value = _currentDefinition.SizeX;
        _sizeYSpin!.Value = _currentDefinition.SizeY;
        _offsetXSpin!.Value = _currentDefinition.OffsetX;
        _offsetYSpin!.Value = _currentDefinition.OffsetY;

        UpdatePreview();
        Validate();

        _isUpdating = false;
    }

    private void UpdatePreview()
    {
        if (_service == null || _tile == null || _variantPreview == null)
            return;

        var texture = _service.GetTileTexture(_tile);
        if (texture == null)
        {
            _variantPreview.Texture = null;
            return;
        }

        var actualTileSize = GetActualTileSize();
        var atlasTexture = new AtlasTexture
        {
            Atlas = texture,
            Region = new Rect2(
                _currentDefinition.AtlasX * actualTileSize.X,
                _currentDefinition.AtlasY * actualTileSize.Y,
                actualTileSize.X * _currentDefinition.SizeX,
                actualTileSize.Y * _currentDefinition.SizeY
            )
        };

        _variantPreview.Texture = atlasTexture;
    }

    private Vector2I GetActualTileSize()
    {
        var tileSize = _service!.TileSet?.TileSize ?? new Vector2I(16, 16);
        return new Vector2I(
            (int)(tileSize.X / _tile!.SourceScale),
            (int)(tileSize.Y / _tile.SourceScale)
        );
    }

    private void Validate()
    {
        if (_service == null || _tile == null || _validationLabel == null)
            return;

        var texture = _service.GetTileTexture(_tile);
        if (texture == null)
        {
            _validationLabel.Text = "No texture";
            _validationLabel.Modulate = new Color(1f, 0.6f, 0.2f);
            return;
        }

        var actualTileSize = GetActualTileSize();
        var regionRight = (_currentDefinition.AtlasX + _currentDefinition.SizeX) * actualTileSize.X;
        var regionBottom = (_currentDefinition.AtlasY + _currentDefinition.SizeY) * actualTileSize.Y;

        if (regionRight > texture.GetWidth() || regionBottom > texture.GetHeight())
        {
            _validationLabel.Text = "Out of bounds!";
            _validationLabel.Modulate = new Color(1f, 0.4f, 0.4f);
            TooltipText = $"Atlas region exceeds texture bounds ({texture.GetWidth()}x{texture.GetHeight()})";
        }
        else
        {
            _validationLabel.Text = "";
            TooltipText =
                $"Bitmask {_bitmask}: {_currentDefinition.SizeX}x{_currentDefinition.SizeY} " +
                $"at ({_currentDefinition.AtlasX}, {_currentDefinition.AtlasY})";
        }
    }
}
#endif
