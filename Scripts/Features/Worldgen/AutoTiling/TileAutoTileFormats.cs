using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;

namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling;

/// <summary>
///     Resolves a tile's auto-tile format name through <see cref="AutoTileFormatRegistry" />. The tile definition holds
///     only the name, so the tile data does not depend on the format registry.
/// </summary>
public static class TileAutoTileFormats
{
    /// <summary>The format the tile names, or null when that format is not registered.</summary>
    public static AutoTileFormatDefinition? GetAutoTileFormat(this TileDefinition tile)
        => AutoTileFormatRegistry.TryGet(tile.AutoTileFormatName, out var format) ? format : null;

    /// <summary>The variant of the tile format for a bitmask, or null when the format or variant is unknown.</summary>
    public static VariantDefinition? GetVariantDefinition(this TileDefinition tile, int bitmask)
        => tile.GetAutoTileFormat()?.GetVariant(bitmask);

    /// <summary>The number of variants the tile's format expects (16 when the format is not registered).</summary>
    public static int GetExpectedVariantCount(this TileDefinition tile)
        => tile.GetAutoTileFormat()?.GetExpectedVariantCount() ?? 16;
}
