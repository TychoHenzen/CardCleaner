using System.Collections.Generic;
using System.Linq;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services.CompiledAtlas;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Builds the visual side of the tile catalog: the compiled atlas when it is available and its tile set builds,
/// otherwise the original tileset.
/// </summary>
internal static class TileVisualLoader
{
    internal static TileVisualCatalog Load(
        OriginalTileset original,
        string? atlasMappingPath,
        ICompiledAtlasSource atlas)
    {
        return TryUseCompiledAtlas(original, atlasMappingPath, atlas) ?? UseOriginalTileset(original);
    }

    /// <summary>The tileset the tile data files name, with the tiles read from them.</summary>
    internal readonly record struct OriginalTileset(
        string TilesetPath,
        IReadOnlyList<TileDefinition> Tiles,
        TilesetConfig TilesetConfig);

    private static TileVisualCatalog? TryUseCompiledAtlas(
        OriginalTileset original,
        string? atlasMappingPath,
        ICompiledAtlasSource atlas)
    {
        // Check if compiled atlas is available
        var isAvailable = atlas.IsAvailable(atlasMappingPath);
        ILog.Print($"[TileRegistry] Compiled atlas available: {isAvailable}");

        if (!isAvailable)
            return null;

        var atlasMapping = atlas.LoadMapping(atlasMappingPath);
        ILog.Print($"[TileRegistry] Atlas mapping loaded: {(atlasMapping != null ? "YES" : "NULL")}");

        if (atlasMapping == null)
        {
            ILog.Print("[TileRegistry] FAILED: mapping is null, falling back");
            return null;
        }

        // The loader owns the tile-set resource and caches it; the registry only asks whether it loads, so that
        // an atlas whose image cannot be loaded falls back to the original tileset, as before it moved out.
        if (!atlas.CanBuild(atlasMappingPath))
        {
            ILog.Print("[TileRegistry] FAILED: compiled atlas resource does not load, falling back");
            return null;
        }

        return UseCompiledAtlas(original, atlasMapping);
    }

    // Use compiled atlas mode - transition_map.json has all auto-tile transitions
    // Even if some base tiles can't be translated, auto-tiles will work
    private static TileVisualCatalog UseCompiledAtlas(
        OriginalTileset original,
        AtlasMapping.AtlasMappingData atlasMapping)
    {
        var tilesetPath = atlasMapping.Atlas?.Path ?? original.TilesetPath;

        // CRITICAL: Use compiled atlas tile size, NOT the TSX source tile size
        // TSX may have smaller source tiles (e.g., 8x8) that get composited to larger tiles (e.g., 16x16)
        var atlasTileSize = atlasMapping.Atlas?.TileSize ?? 16;
        var tilesetConfig = new TilesetConfig
        {
            BaseTileSize = new Vector2I(atlasTileSize, atlasTileSize)
        };

        // Translate WITH mapping - tiles not in atlas_mapping keep original coords
        // but auto-tiles use transition_map.json which IS complete
        var translator = new CompiledAtlasTileTranslator(atlasMapping);
        var tiles = original.Tiles.Select(tile => translator.Translate(tile)).ToList();

        ILog.Print(
            $"[TileRegistry] SUCCESS: Registered {CountDistinctIds(tiles)} tiles, " +
            "using compiled atlas for auto-tiles");
        return new TileVisualCatalog(tilesetPath, true, tilesetConfig, tiles);
    }

    // Fallback to original tileset
    private static TileVisualCatalog UseOriginalTileset(OriginalTileset original)
    {
        ILog.Print(
            $"[TileRegistry] FALLBACK: Registered {CountDistinctIds(original.Tiles)} tiles " +
            $"using tileset {original.TilesetPath}");
        return new TileVisualCatalog(original.TilesetPath, false, original.TilesetConfig, original.Tiles);
    }

    // The registry keys tiles by Id, so the count it logs is the number of distinct Ids.
    private static int CountDistinctIds(IEnumerable<TileDefinition> tiles)
        => tiles.Select(t => t.Id).Distinct().Count();
}
