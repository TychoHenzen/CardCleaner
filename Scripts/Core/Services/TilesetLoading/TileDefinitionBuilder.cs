using System.Collections.Generic;
using System.Linq;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading;

/// <summary>
/// Builds TileDefinition objects from parsed TSX data.
/// </summary>
internal static class TileDefinitionBuilder
{
    /// <summary>
    /// Build TileDefinition objects from parsed TSX data.
    /// Creates tiles from Wang sets (using the set name as ID) and from tiles with explicit "id" property.
    /// </summary>
    public static List<TileDefinition> BuildTileDefinitions(
        Dictionary<int, TilePropertyData> tileProperties,
        WangSetData wangData,
        int columns,
        int sourceId)
    {
        var tiles = new List<TileDefinition>();
        var processedWangSets = new HashSet<string>();

        // First pass: create TileDefinitions from Wang sets
        // Each Wang set becomes a tile with its 'class' attribute as the ID
        foreach (var (setId, setInfo) in wangData.WangSetInfo)
        {
            var tile = BuildTileDefinitionFromWangSet(setId, setInfo, wangData, tileProperties, columns, sourceId);
            if (tile != null)
            {
                tiles.Add(tile);
                processedWangSets.Add(ParseHelpers.ToSnakeCase(setId));
            }
        }

        // Second pass: create tiles from "type" attribute or explicit "id" property (for non-Wang tiles)
        foreach (var (tileId, propData) in tileProperties)
        {
            // Prefer 'type' attribute (set in propData.Type), fall back to 'id' property for compatibility
            var id = propData.Type;
            if (string.IsNullOrEmpty(id) && !propData.Properties.TryGetValue("id", out id))
                continue;
            if (string.IsNullOrEmpty(id))
                continue;

            // Skip if this tile's ID matches a Wang set we already processed
            if (processedWangSets.Contains(id))
                continue;

            var atlasCoords = ParseHelpers.TileIdToAtlasCoords(tileId, columns);
            var tile = BuildTileDefinitionFromProperties(
                id, tileId, atlasCoords, propData, wangData, columns, sourceId);
            tiles.Add(tile);
        }

        return tiles;
    }

    /// <summary>
    /// Build a TileDefinition from a Wang set.
    /// Uses the Wang set 'class' attribute (setId) as the tile ID and 'name' for display.
    /// </summary>
    private static TileDefinition? BuildTileDefinitionFromWangSet(
        string setId,
        WangSetInfo setInfo,
        WangSetData wangData,
        Dictionary<int, TilePropertyData> tileProperties,
        int columns,
        int sourceId)
    {
        // Find a representative tile for base atlas coords (prefer bitmask 15 = all corners)
        var baseTileId = FindRepresentativeTile(setId, wangData);
        if (baseTileId < 0)
        {
            ILog.Print($"[TileDefinitionBuilder] Wang set '{setId}' has no tiles, skipping");
            return null;
        }

        var atlasCoords = ParseHelpers.TileIdToAtlasCoords(baseTileId, columns);

        // Get properties from Wang set itself, with fallback to representative tile properties
        var props = new Dictionary<string, string>(setInfo.Properties, System.StringComparer.OrdinalIgnoreCase);
        if (tileProperties.TryGetValue(baseTileId, out var tilePropData))
        {
            // Merge tile properties (Wang set props take precedence)
            foreach (var (key, value) in tilePropData.Properties)
            {
                if (!props.ContainsKey(key))
                    props[key] = value;
            }
        }

        // Use setId as the tile ID (from 'class' attribute), setInfo.Name for display
        var id = ParseHelpers.ToSnakeCase(setId);
        var name = ParseHelpers.GetString(props, "name", setInfo.Name);

        // Dual-grid only samples 4 corners, so always use corner16 format
        // Blob47/mixed tilesets are converted to corner16 at load time
        var autoTileFormat = "corner16";

        // Build auto-tile variants
        var autoTileVariants = BuildAutoTileVariants(setId, wangData, columns, setInfo.BitmaskType);

        var (passability, layer, elevation, isTransparent, dominance, decorationDensity, outerTerrain, innerTerrain) =
            ParseCommonProperties(props);
        var isGapTile = ParseHelpers.GetBool(props, "isgaptile", false);
        var biomes = ParseHelpers.ParseBiomeBooleans(props);

        var size = ParseTileSize(props);

        // Get probability from the wang set (for variation group weighting)
        var probability = GetWangSetProbability(setId, wangData);

        return new TileDefinition(
            id: id,
            name: name,
            passability: passability,
            atlasCoords: atlasCoords,
            sourceId: sourceId,
            layer: layer,
            elevation: elevation,
            isTransparent: isTransparent,
            allowedBiomes: biomes,
            size: size,
            decorationDensity: decorationDensity,
            autoTileVariants: autoTileVariants,
            autoTileFormatName: autoTileFormat,
            variations: null,
            variationMode: VariationMode.PerInstance,
            animation: null,
            dominance: dominance,
            innerTerrainId: innerTerrain,
            outerTerrainId: outerTerrain,
            isGapTile: isGapTile,
            probability: probability);
    }

    /// <summary>
    /// Gets the probability for a Wang set. Uses the probability from the representative tile (bitmask 15),
    /// or the first tile if bitmask 15 is not present.
    /// </summary>
    private static float GetWangSetProbability(string setName, WangSetData wangData)
    {
        // Prefer bitmask 255 (all corners/edges in Full8 format) as it's the "full" tile
        // Wang data stores tiles at Full8 keys from WangIdToBitmask
        var fullKey = (setName, 255);
        if (wangData.BitmaskToTiles.TryGetValue(fullKey, out var fullTiles) && fullTiles.Count > 0)
            return fullTiles[0].Probability;

        // Fallback to any tile in the set
        foreach (var ((name, _), tiles) in wangData.BitmaskToTiles)
        {
            if (name == setName && tiles.Count > 0)
                return tiles[0].Probability;
        }

        return 1f; // Default probability
    }

    /// <summary>
    /// Find a representative tile ID for a Wang set (prefers bitmask 15 = all corners filled).
    /// </summary>
    private static int FindRepresentativeTile(string setName, WangSetData wangData)
    {
        // Prefer bitmask 255 (all corners/edges) as it's the "full" tile
        // Note: All wang types are now converted to Full8 format
        var fullKey = (setName, 255);
        if (wangData.BitmaskToTiles.TryGetValue(fullKey, out var fullTiles) && fullTiles.Count > 0)
            return fullTiles[0].TileId;

        // Fallback to any tile in the set
        foreach (var ((name, _), tiles) in wangData.BitmaskToTiles)
        {
            if (name == setName && tiles.Count > 0)
                return tiles[0].TileId;
        }

        return -1;
    }

    /// <summary>
    /// Build a TileDefinition from explicit tile properties (for non-Wang tiles).
    /// </summary>
    private static TileDefinition BuildTileDefinitionFromProperties(
        string id,
        int baseTileId,
        Vector2I atlasCoords,
        TilePropertyData propData,
        WangSetData wangData,
        int columns,
        int sourceId)
    {
        var props = propData.Properties;

        var name = ParseHelpers.GetString(props, "name", id);
        var (passability, layer, elevation, isTransparent, dominance, decorationDensity, outerTerrain, innerTerrain) =
            ParseCommonProperties(props);
        // Tiles without Wang set membership are gap tiles by default (base tiles for background layer)
        var hasWangSet = wangData.TileToWangSet.ContainsKey(baseTileId);
        var isGapTile = ParseHelpers.GetBool(props, "isgaptile", !hasWangSet);

        var size = ParseTileSize(props);

        // Biomes from boolean properties (biome_forest, biome_desert, etc.)
        var biomes = ParseHelpers.ParseBiomeBooleans(props);

        // Auto-tile format and variants from Wang data
        var autoTileFormat = ParseHelpers.GetString(props, "autotileformat", "corner16");
        Vector2I?[]? autoTileVariants = null;

        if (wangData.TileToWangSet.TryGetValue(baseTileId, out var wangSetName))
        {
            // Determine bitmask type from wang set
            var bitmaskType = wangData.TileBitmaskType.GetValueOrDefault(baseTileId, BitmaskType.Corner4);

            // Update format name based on wang type
            autoTileFormat = bitmaskType switch
            {
                BitmaskType.Corner4 => "corner16",
                BitmaskType.Edge4 => "edge16",
                BitmaskType.Full8 => "blob47",
                _ => autoTileFormat
            };

            autoTileVariants = BuildAutoTileVariants(wangSetName, wangData, columns, bitmaskType);
        }

        // Standalone variations (not from wang tiles)
        Vector2I[]? variations = null;
        var variationsStr = ParseHelpers.GetStringOrNull(props, "variations");
        if (!string.IsNullOrEmpty(variationsStr))
        {
            variations = ParseHelpers.ParseVariationsString(variationsStr, columns);
        }

        var variationMode = ParseHelpers.ParseVariationMode(
            ParseHelpers.GetString(props, "variationmode", "perinstance"));

        // Animation
        TileAnimation? animation = null;
        if (propData.Animation != null && propData.Animation.Count > 0)
        {
            var frames = propData.Animation
                .Select(f => ParseHelpers.TileIdToAtlasCoords(f.TileId, columns))
                .ToArray();
            var frameDuration = propData.Animation[0].Duration;
            animation = new TileAnimation(frames, frameDuration);
        }

        // Probability from tile properties (for non-Wang tiles)
        var probability = ParseHelpers.GetFloat(props, "probability", 1f);

        return new TileDefinition(
            id: id,
            name: name,
            passability: passability,
            atlasCoords: atlasCoords,
            sourceId: sourceId,
            layer: layer,
            elevation: elevation,
            isTransparent: isTransparent,
            allowedBiomes: biomes,
            size: size,
            decorationDensity: decorationDensity,
            autoTileVariants: autoTileVariants,
            autoTileFormatName: autoTileFormat,
            variations: variations,
            variationMode: variationMode,
            animation: animation,
            dominance: dominance,
            innerTerrainId: innerTerrain,
            outerTerrainId: outerTerrain,
            isGapTile: isGapTile,
            probability: probability);
    }

    private static (
        TilePassability Passability,
        TileLayer Layer,
        float Elevation,
        bool IsTransparent,
        int Dominance,
        float DecorationDensity,
        string? OuterTerrain,
        string? InnerTerrain) ParseCommonProperties(Dictionary<string, string> props)
    {
        var passability = ParseHelpers.ParsePassability(ParseHelpers.GetString(props, "passability", "passable"));
        var layer = ParseHelpers.ParseLayer(ParseHelpers.GetString(props, "layer", "terrain"));
        var elevation = ParseHelpers.GetFloat(props, "elevation", 0f);
        var isTransparent = ParseHelpers.GetBool(props, "istransparent", passability == TilePassability.Passable);
        var dominance = ParseHelpers.GetInt(props, "dominance", 0);
        var decorationDensity = ParseHelpers.GetFloat(props, "decorationdensity", 1f);
        var outerTerrain = ParseHelpers.GetStringOrNull(props, "outerterrain");
        var innerTerrain = ParseHelpers.GetStringOrNull(props, "innerterrain");

        return (passability, layer, elevation, isTransparent, dominance, decorationDensity, outerTerrain, innerTerrain);
    }

    private static Vector2I? ParseTileSize(Dictionary<string, string> props)
    {
        var sizeStr = ParseHelpers.GetStringOrNull(props, "size");
        if (string.IsNullOrEmpty(sizeStr))
            return null;

        var sizeParts = sizeStr.Split(',', 'x');
        return sizeParts.Length == 2
            ? new Vector2I(ParseHelpers.ParseInt(sizeParts[0], 1), ParseHelpers.ParseInt(sizeParts[1], 1))
            : null;
    }

    /// <summary>
    /// Build auto-tile variant array from Wang set data.
    /// For bitmasks with multiple tiles, picks the first (variations handled separately).
    /// </summary>
    private static Vector2I?[]? BuildAutoTileVariants(
        string wangSetName,
        WangSetData wangData,
        int columns,
        BitmaskType bitmaskType)
    {
        // Dual-grid only samples 4 corners, so we always use Corner16 format (16 variants)
        // For blob47/mixed tilesets, map Full8 bitmasks back to their Corner16 equivalents
        const int variantCount = 16;

        var variants = new Vector2I?[variantCount];
        var hasAnyVariant = false;

        // For each Corner16 bitmask (0-15), find the corresponding tile
        for (int corner16 = 0; corner16 < 16; corner16++)
        {
            // Convert Corner16 to Full8 to look up in the parsed wang data
            // (wang data stores tiles at Full8 bitmasks from WangIdToBitmask)
            var hasNE = (corner16 & NeighborBitmaskCorner.NorthEast) != 0;
            var hasSE = (corner16 & NeighborBitmaskCorner.SouthEast) != 0;
            var hasSW = (corner16 & NeighborBitmaskCorner.SouthWest) != 0;
            var hasNW = (corner16 & NeighborBitmaskCorner.NorthWest) != 0;
            var full8Mask = DualGridAutoTile.CornersToFull8Bitmask(hasNE, hasSE, hasSW, hasNW);

            var key = (wangSetName, full8Mask);
            if (!wangData.BitmaskToTiles.TryGetValue(key, out var tilesForMask) || tilesForMask.Count == 0)
                continue;

            // Store at Corner16 index
            var tileId = tilesForMask[0].TileId;
            variants[corner16] = ParseHelpers.TileIdToAtlasCoords(tileId, columns);
            hasAnyVariant = true;
        }

        return hasAnyVariant ? variants : null;
    }
}
