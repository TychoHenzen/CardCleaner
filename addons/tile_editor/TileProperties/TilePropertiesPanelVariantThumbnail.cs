#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TilePropertiesPanel
{
    private void UpdateVariantThumbnail(int slotIndex)
    {
        var thumbnail = GetVariantThumbnail(slotIndex);
        if (thumbnail == null)
            return;

        var coords = GetVariantCoordinates(slotIndex);
        if (!coords.HasValue)
        {
            thumbnail.Texture = null;
            return;
        }

        var texture = _service!.GetTileTexture(_currentTile!);
        if (texture == null)
        {
            thumbnail.Texture = null;
            return;
        }

        var tileSize = GetActualTileSize();
        var variantSize = GetVariantSize(GetBitmaskForSlot(slotIndex));
        thumbnail.Texture = new AtlasTexture
        {
            Atlas = texture,
            Region = new Rect2I(
                coords.Value * tileSize,
                new Vector2I(tileSize.X * variantSize.X, tileSize.Y * variantSize.Y))
        };
    }

    private TextureRect? GetVariantThumbnail(int slotIndex)
    {
        if (_currentTile == null || _variantThumbnails == null
            || slotIndex < 0 || slotIndex >= _variantThumbnails.Length
            || _variantThumbnails[slotIndex] == null)
        {
            return null;
        }

        return _variantThumbnails[slotIndex];
    }

    private Vector2I? GetVariantCoordinates(int slotIndex)
    {
        var bitmask = GetBitmaskForSlot(slotIndex);
        if (_currentTile?.AutoTileVariants == null
            || bitmask >= _currentTile.AutoTileVariants.Length)
        {
            return null;
        }

        return _currentTile.AutoTileVariants[bitmask];
    }

    private Vector2I GetActualTileSize()
    {
        var baseTileSize = _service!.TileSet?.TileSize ?? new Vector2I(16, 16);
        return new Vector2I(
            (int)(baseTileSize.X / _currentTile!.SourceScale),
            (int)(baseTileSize.Y / _currentTile.SourceScale));
    }

    private Vector2I GetVariantSize(int bitmask)
    {
        var formatName = _currentTile!.AutoTileFormat ?? "corner16";
        var format = _service!.GetFormatDefinition(formatName);
        if (format == null)
            return Vector2I.One;

        var variant = format.GetVariant(bitmask);
        return variant.HasValue
            && (variant.Value.Size.X > 0 || variant.Value.Size.Y > 0)
            ? variant.Value.Size
            : Vector2I.One;
    }

    private int GetBitmaskForSlot(int slotIndex)
    {
        if (_slotIndexToBitmask != null
            && slotIndex >= 0
            && slotIndex < _slotIndexToBitmask.Count)
        {
            return _slotIndexToBitmask[slotIndex];
        }

        return slotIndex;
    }

    private void EnsureVariantArraySize(int bitmask)
    {
        if (_currentTile == null)
            return;

        var requiredSize = bitmask + 1;
        if (_currentTile.AutoTileVariants != null
            && _currentTile.AutoTileVariants.Length >= requiredSize)
        {
            return;
        }

        var newArray = new Vector2I?[requiredSize];
        if (_currentTile.AutoTileVariants != null)
        {
            System.Array.Copy(
                _currentTile.AutoTileVariants,
                newArray,
                _currentTile.AutoTileVariants.Length);
        }

        _currentTile.AutoTileVariants = newArray;
    }
}
#endif
