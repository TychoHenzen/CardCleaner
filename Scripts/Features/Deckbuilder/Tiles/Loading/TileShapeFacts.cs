using CardCleaner.Features.Worldgen.AutoTiling;

namespace CardCleaner.Scripts.Features.Deckbuilder.Tiles.Loading;

/// <summary>Resolves the shape facts of an auto-tile format name once, when a tile is built.</summary>
internal static class TileShapeFacts
{
    internal static TileShape Resolve(string formatName)
    {
        if (!AutoTileFormatRegistry.TryGet(formatName, out var format) || format == null)
            return new TileShape(null, null);
        return new TileShape(format.AllowedBitmasks, format.GetMaxMultiCellBounds());
    }
}
