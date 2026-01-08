using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Loads tile definitions from Tiled TSX tileset files or TMX map files.
/// Supports Wang tiles for auto-tiling and custom properties for game-specific data.
/// </summary>
public static class TiledTilesetLoader
{
    /// <summary>
    /// Load tiles from a TMX map file (loads all referenced tilesets).
    /// </summary>
    public static TileRegistryResult LoadFromTmx(string tmxPath)
    {
        var absolutePath = ProjectSettings.GlobalizePath(tmxPath);
        if (!File.Exists(absolutePath))
        {
            ILog.Print($"[TiledTilesetLoader] TMX file not found: {absolutePath}");
            return new TileRegistryResult("", [], TilesetConfig.Default);
        }

        try
        {
            var doc = XDocument.Load(absolutePath);
            var map = doc.Root;
            if (map == null || map.Name != "map")
            {
                ILog.Print("[TiledTilesetLoader] Invalid TMX: missing map root element");
                return new TileRegistryResult("", [], TilesetConfig.Default);
            }

            var tmxDir = Path.GetDirectoryName(absolutePath) ?? "";
            var allTiles = new List<TileDefinition>();
            TilesetConfig? config = null;

            // Load each referenced tileset
            foreach (var tilesetRef in map.Elements("tileset"))
            {
                var firstGid = ParseInt(tilesetRef.Attribute("firstgid")?.Value, 1);
                var source = tilesetRef.Attribute("source")?.Value;

                if (string.IsNullOrEmpty(source)) continue;

                // Resolve relative TSX path
                var tsxAbsolutePath = Path.GetFullPath(Path.Combine(tmxDir, source));
                var tsxResPath = $"res://{Path.GetRelativePath(ProjectSettings.GlobalizePath("res://"), tsxAbsolutePath).Replace('\\', '/')}";

                ILog.Print($"[TiledTilesetLoader] Loading tileset from TMX: {source} (firstgid={firstGid})");

                var result = LoadFromTsx(tsxAbsolutePath, firstGid);
                allTiles.AddRange(result.Tiles);

                // Use config from first tileset
                config ??= result.TilesetConfig;
            }

            ILog.Print($"[TiledTilesetLoader] Loaded {allTiles.Count} tiles from TMX {tmxPath}");
            return new TileRegistryResult("", allTiles, config ?? TilesetConfig.Default);
        }
        catch (Exception ex)
        {
            ILog.Print($"[TiledTilesetLoader] Error loading TMX: {ex.Message}");
            return new TileRegistryResult("", [], TilesetConfig.Default);
        }
    }

    /// <summary>
    /// Load tiles from a TSX tileset file.
    /// </summary>
    public static TileRegistryResult LoadFromTsx(string tsxPath, int sourceId = 0)
    {
        var absolutePath = ProjectSettings.GlobalizePath(tsxPath);
        if (!File.Exists(absolutePath))
        {
            ILog.Print($"[TiledTilesetLoader] TSX file not found: {absolutePath}");
            return new TileRegistryResult("", [], TilesetConfig.Default);
        }

        try
        {
            var doc = XDocument.Load(absolutePath);
            var tileset = doc.Root;
            if (tileset == null || tileset.Name != "tileset")
            {
                ILog.Print("[TiledTilesetLoader] Invalid TSX: missing tileset root element");
                return new TileRegistryResult("", [], TilesetConfig.Default);
            }

            var tileWidth = ParseInt(tileset.Attribute("tilewidth")?.Value, 16);
            var tileHeight = ParseInt(tileset.Attribute("tileheight")?.Value, 16);
            var columns = ParseInt(tileset.Attribute("columns")?.Value, 1);

            // Parse image source path
            var imageElement = tileset.Element("image");
            var imageSource = imageElement?.Attribute("source")?.Value ?? "";

            // Resolve relative path from TSX location
            if (!string.IsNullOrEmpty(imageSource) && !imageSource.StartsWith("res://"))
            {
                var tsxDir = Path.GetDirectoryName(absolutePath) ?? "";
                var fullImagePath = Path.GetFullPath(Path.Combine(tsxDir, imageSource));
                imageSource = fullImagePath; // Keep absolute for now, caller can convert
            }

            // Parse Wang sets for auto-tile mappings
            var wangData = ParseWangSets(tileset);

            // Parse per-tile properties
            var tileProperties = ParseAllTileProperties(tileset);

            // Build TileDefinitions
            var tiles = BuildTileDefinitions(tileProperties, wangData, columns, sourceId);

            var config = new TilesetConfig
            {
                BaseTileSize = new Vector2I(tileWidth, tileHeight)
            };

            ILog.Print($"[TiledTilesetLoader] Loaded {tiles.Count} tiles from {tsxPath}");
            return new TileRegistryResult(imageSource, tiles, config);
        }
        catch (Exception ex)
        {
            ILog.Print($"[TiledTilesetLoader] Error loading TSX: {ex.Message}");
            return new TileRegistryResult("", [], TilesetConfig.Default);
        }
    }

    #region Wang Set Parsing

    /// <summary>
    /// Parse all Wang sets and build bitmask → tile mappings.
    /// Multiple tiles per bitmask = variations.
    /// </summary>
    private static WangSetData ParseWangSets(XElement tileset)
    {
        var result = new WangSetData();
        var wangsets = tileset.Element("wangsets");
        if (wangsets == null) return result;

        foreach (var wangset in wangsets.Elements("wangset"))
        {
            var wangType = wangset.Attribute("type")?.Value ?? "corner";
            // Use 'class' attribute as the tile ID, fall back to 'name' for compatibility
            var setId = wangset.Attribute("class")?.Value ?? wangset.Attribute("name")?.Value ?? "unnamed";
            var setName = wangset.Attribute("name")?.Value ?? setId;

            // Parse which color index represents "terrain present"
            // Typically color 1 = foreground/terrain, color 2 = background
            var terrainColorIndex = 1; // Default assumption

            // Check wangset properties for custom terrain color and other properties
            var setProps = ParseProperties(wangset.Element("properties"));
            if (setProps.TryGetValue("terraincolor", out var tcVal))
                terrainColorIndex = ParseInt(tcVal, 1);

            // Determine bitmask type from wang type
            var bitmaskType = wangType switch
            {
                "corner" => BitmaskType.Corner4,
                "edge" => BitmaskType.Edge4,
                "mixed" => BitmaskType.Full8,
                _ => BitmaskType.Corner4
            };

            // Store Wang set metadata for later TileDefinition creation
            // setId is the tile ID (from class attr), setName is the display name
            result.WangSetInfo[setId] = new WangSetInfo(setName, bitmaskType, setProps);

            foreach (var wangtile in wangset.Elements("wangtile"))
            {
                var tileId = ParseInt(wangtile.Attribute("tileid")?.Value, -1);
                var wangidStr = wangtile.Attribute("wangid")?.Value ?? "";
                var probability = ParseFloat(wangtile.Attribute("probability")?.Value, 1f);

                if (tileId < 0 || string.IsNullOrEmpty(wangidStr)) continue;

                var bitmask = WangIdToBitmask(wangidStr, wangType, terrainColorIndex);

                // Track which tiles belong to this wang set (use setId for lookups)
                if (!result.TileToWangSet.ContainsKey(tileId))
                    result.TileToWangSet[tileId] = setId;

                // Track bitmask type per tile
                if (!result.TileBitmaskType.ContainsKey(tileId))
                    result.TileBitmaskType[tileId] = bitmaskType;

                // Build bitmask → tiles mapping (grouped by wang set ID)
                var key = (setId, bitmask);
                if (!result.BitmaskToTiles.ContainsKey(key))
                    result.BitmaskToTiles[key] = [];

                result.BitmaskToTiles[key].Add(new WangTileInfo(tileId, probability));
            }
        }

        return result;
    }

    /// <summary>
    /// Convert Tiled wangid string to bitmask value.
    /// </summary>
    private static int WangIdToBitmask(string wangidStr, string wangType, int terrainColorIndex)
    {
        // wangid format: "edge0,corner0,edge1,corner1,edge2,corner2,edge3,corner3"
        // For corner-only: edges are 0, corners are at indices 1,3,5,7 (TL,TR,BR,BL)
        // For edge-only: corners are 0, edges are at indices 0,2,4,6 (Top,Right,Bottom,Left)
        var parts = wangidStr.Split(',');
        if (parts.Length != 8) return 0;

        int bitmask = 0;

        if (wangType == "corner")
        {
            // Corner mode: indices 1,3,5,7 = TL,TR,BR,BL
            var tl = ParseInt(parts[1], 0);
            var tr = ParseInt(parts[3], 0);
            var br = ParseInt(parts[5], 0);
            var bl = ParseInt(parts[7], 0);

            // Bit order: 0=TL, 1=TR, 2=BR, 3=BL
            if (tl == terrainColorIndex) bitmask |= 1;
            if (tr == terrainColorIndex) bitmask |= 2;
            if (br == terrainColorIndex) bitmask |= 4;
            if (bl == terrainColorIndex) bitmask |= 8;
        }
        else if (wangType == "edge")
        {
            // Edge mode: indices 0,2,4,6 = Top,Right,Bottom,Left
            var top = ParseInt(parts[0], 0);
            var right = ParseInt(parts[2], 0);
            var bottom = ParseInt(parts[4], 0);
            var left = ParseInt(parts[6], 0);

            // Bit order: 0=Top, 1=Right, 2=Bottom, 3=Left
            if (top == terrainColorIndex) bitmask |= 1;
            if (right == terrainColorIndex) bitmask |= 2;
            if (bottom == terrainColorIndex) bitmask |= 4;
            if (left == terrainColorIndex) bitmask |= 8;
        }
        else if (wangType == "mixed")
        {
            // Full 8-bit: all positions used
            for (int i = 0; i < 8; i++)
            {
                if (ParseInt(parts[i], 0) == terrainColorIndex)
                    bitmask |= (1 << i);
            }
        }

        return bitmask;
    }

    #endregion

    #region Tile Property Parsing

    /// <summary>
    /// Parse custom properties for all tiles that have them.
    /// Also captures the 'type' attribute which serves as the tile's ID.
    /// </summary>
    private static Dictionary<int, TilePropertyData> ParseAllTileProperties(XElement tileset)
    {
        var result = new Dictionary<int, TilePropertyData>();

        foreach (var tile in tileset.Elements("tile"))
        {
            var tileId = ParseInt(tile.Attribute("id")?.Value, -1);
            if (tileId < 0) continue;

            var tileType = tile.Attribute("type")?.Value; // 'type' attr serves as the tile ID
            var props = ParseProperties(tile.Element("properties"));
            var animation = ParseAnimation(tile.Element("animation"));

            result[tileId] = new TilePropertyData(tileType, props, animation);
        }

        return result;
    }

    /// <summary>
    /// Parse a properties element into a dictionary.
    /// </summary>
    private static Dictionary<string, string> ParseProperties(XElement? propsElement)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (propsElement == null) return result;

        foreach (var prop in propsElement.Elements("property"))
        {
            var name = prop.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(name)) continue;

            // Value can be in attribute or element content
            var value = prop.Attribute("value")?.Value ?? prop.Value;
            result[name] = value;
        }

        return result;
    }

    /// <summary>
    /// Parse Tiled's native animation element.
    /// </summary>
    private static List<AnimationFrame>? ParseAnimation(XElement? animElement)
    {
        if (animElement == null) return null;

        var frames = new List<AnimationFrame>();
        foreach (var frame in animElement.Elements("frame"))
        {
            var tileId = ParseInt(frame.Attribute("tileid")?.Value, -1);
            var duration = ParseInt(frame.Attribute("duration")?.Value, 100); // ms

            if (tileId >= 0)
                frames.Add(new AnimationFrame(tileId, duration / 1000f)); // Convert to seconds
        }

        return frames.Count > 0 ? frames : null;
    }

    #endregion

    #region TileDefinition Building

    /// <summary>
    /// Build TileDefinition objects from parsed TSX data.
    /// Creates tiles from Wang sets (using the set name as ID) and from tiles with explicit "id" property.
    /// </summary>
    private static List<TileDefinition> BuildTileDefinitions(
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
                processedWangSets.Add(ToSnakeCase(setId));
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

            var atlasCoords = TileIdToAtlasCoords(tileId, columns);
            var tile = BuildTileDefinitionFromProperties(id, tileId, atlasCoords, propData, wangData, columns, sourceId);
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
            ILog.Print($"[TiledTilesetLoader] Wang set '{setId}' has no tiles, skipping");
            return null;
        }

        var atlasCoords = TileIdToAtlasCoords(baseTileId, columns);

        // Get properties from Wang set itself, with fallback to representative tile properties
        var props = new Dictionary<string, string>(setInfo.Properties, StringComparer.OrdinalIgnoreCase);
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
        var id = ToSnakeCase(setId);
        var name = GetString(props, "name", setInfo.Name);

        // Auto-tile format based on Wang type
        var autoTileFormat = setInfo.BitmaskType switch
        {
            BitmaskType.Corner4 => "corner16",
            BitmaskType.Edge4 => "edge16",
            BitmaskType.Full8 => "blob47",
            _ => "corner16"
        };

        // Build auto-tile variants
        var autoTileVariants = BuildAutoTileVariants(setId, wangData, columns, setInfo.BitmaskType);

        // Parse other properties
        var passability = ParsePassability(GetString(props, "passability", "passable"));
        var layer = ParseLayer(GetString(props, "layer", "terrain"));
        var elevation = GetFloat(props, "elevation", 0f);
        var isTransparent = GetBool(props, "istransparent", passability == TilePassability.Passable);
        var dominance = GetInt(props, "dominance", 0);
        var decorationDensity = GetFloat(props, "decorationdensity", 1f);
        var outerTerrain = GetStringOrNull(props, "outerterrain");
        var innerTerrain = GetStringOrNull(props, "innerterrain");
        var isGapTile = GetBool(props, "isgaptile", false);
        var biomes = ParseBiomeBooleans(props);

        // Size (for multi-cell tiles)
        Vector2I? size = null;
        var sizeStr = GetStringOrNull(props, "size");
        if (!string.IsNullOrEmpty(sizeStr))
        {
            var sizeParts = sizeStr.Split(',', 'x');
            if (sizeParts.Length == 2)
                size = new Vector2I(ParseInt(sizeParts[0], 1), ParseInt(sizeParts[1], 1));
        }

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
            isGapTile: isGapTile);
    }

    /// <summary>
    /// Find a representative tile ID for a Wang set (prefers bitmask 15 = all corners filled).
    /// </summary>
    private static int FindRepresentativeTile(string setName, WangSetData wangData)
    {
        // Prefer bitmask 15 (all corners) as it's the "full" tile
        var fullKey = (setName, 15);
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
    /// Convert a name to snake_case (e.g., "Grass3" → "grass3", "ForestFloor" → "forest_floor").
    /// </summary>
    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        var result = new System.Text.StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0 && !char.IsUpper(name[i - 1]))
            {
                result.Append('_');
            }
            result.Append(char.ToLowerInvariant(c));
        }
        return result.ToString();
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

        // Basic properties
        var name = GetString(props, "name", id);
        var passability = ParsePassability(GetString(props, "passability", "passable"));
        var layer = ParseLayer(GetString(props, "layer", "terrain"));
        var elevation = GetFloat(props, "elevation", 0f);
        var isTransparent = GetBool(props, "istransparent", passability == TilePassability.Passable);
        var dominance = GetInt(props, "dominance", 0);
        var decorationDensity = GetFloat(props, "decorationdensity", 1f);
        var outerTerrain = GetStringOrNull(props, "outerterrain");
        var innerTerrain = GetStringOrNull(props, "innerterrain");
        var isGapTile = GetBool(props, "isgaptile", false);

        // Size (for multi-cell tiles)
        Vector2I? size = null;
        var sizeStr = GetStringOrNull(props, "size");
        if (!string.IsNullOrEmpty(sizeStr))
        {
            var sizeParts = sizeStr.Split(',', 'x');
            if (sizeParts.Length == 2)
                size = new Vector2I(ParseInt(sizeParts[0], 1), ParseInt(sizeParts[1], 1));
        }

        // Biomes from boolean properties (biome_forest, biome_desert, etc.)
        var biomes = ParseBiomeBooleans(props);

        // Auto-tile format and variants from Wang data
        var autoTileFormat = GetString(props, "autotileformat", "corner16");
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
        var variationsStr = GetStringOrNull(props, "variations");
        if (!string.IsNullOrEmpty(variationsStr))
        {
            variations = ParseVariationsString(variationsStr, columns);
        }

        var variationMode = ParseVariationMode(GetString(props, "variationmode", "perinstance"));

        // Animation
        TileAnimation? animation = null;
        if (propData.Animation != null && propData.Animation.Count > 0)
        {
            var frames = propData.Animation
                .Select(f => TileIdToAtlasCoords(f.TileId, columns))
                .ToArray();
            var frameDuration = propData.Animation[0].Duration;
            animation = new TileAnimation(frames, frameDuration);
        }

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
            isGapTile: isGapTile);
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
        // Determine variant count based on bitmask type
        var variantCount = bitmaskType switch
        {
            BitmaskType.Corner4 => 16,  // 4-bit: 0-15
            BitmaskType.Edge4 => 16,    // 4-bit: 0-15
            BitmaskType.Full8 => 256,   // 8-bit: 0-255
            _ => 16
        };

        var variants = new Vector2I?[variantCount];
        var hasAnyVariant = false;

        for (int bitmask = 0; bitmask < variantCount; bitmask++)
        {
            var key = (wangSetName, bitmask);
            if (wangData.BitmaskToTiles.TryGetValue(key, out var tilesForMask) && tilesForMask.Count > 0)
            {
                // Use first tile's coords; additional tiles are variations
                var tileId = tilesForMask[0].TileId;
                variants[bitmask] = TileIdToAtlasCoords(tileId, columns);
                hasAnyVariant = true;
            }
        }

        return hasAnyVariant ? variants : null;
    }

    /// <summary>
    /// Parse biome boolean properties (biome_forest: true, etc.)
    /// </summary>
    private static HashSet<string>? ParseBiomeBooleans(Dictionary<string, string> props)
    {
        var biomes = new HashSet<string>();

        foreach (var (key, value) in props)
        {
            if (!key.StartsWith("biome_", StringComparison.OrdinalIgnoreCase))
                continue;

            if (ParseBool(value, false))
            {
                var biomeName = key.Substring(6).ToLowerInvariant(); // Remove "biome_" prefix
                biomes.Add(biomeName);
            }
        }

        return biomes.Count > 0 ? biomes : null;
    }

    /// <summary>
    /// Parse variations string like "3,4;5,6;7,8" into coordinates.
    /// </summary>
    private static Vector2I[]? ParseVariationsString(string str, int columns)
    {
        var variations = new List<Vector2I>();

        foreach (var part in str.Split(';'))
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Could be "x,y" coords or just a tile id
            if (trimmed.Contains(','))
            {
                var coords = trimmed.Split(',');
                if (coords.Length == 2)
                {
                    var x = ParseInt(coords[0], 0);
                    var y = ParseInt(coords[1], 0);
                    variations.Add(new Vector2I(x, y));
                }
            }
            else
            {
                // Treat as tile ID
                var tileId = ParseInt(trimmed, -1);
                if (tileId >= 0)
                    variations.Add(TileIdToAtlasCoords(tileId, columns));
            }
        }

        return variations.Count > 0 ? variations.ToArray() : null;
    }

    #endregion

    #region Helpers

    private static Vector2I TileIdToAtlasCoords(int tileId, int columns)
    {
        if (columns <= 0) columns = 1;
        return new Vector2I(tileId % columns, tileId / columns);
    }

    private static TilePassability ParsePassability(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "passable" => TilePassability.Passable,
            "solid" => TilePassability.Solid,
            "partially_passable" or "partial" => TilePassability.PartiallyPassable,
            _ => TilePassability.Passable
        };
    }

    private static TileLayer ParseLayer(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "terrain" => TileLayer.Terrain,
            "decoration" => TileLayer.Decoration,
            "structure" => TileLayer.Structure,
            "effects" => TileLayer.Effects,
            _ => TileLayer.Terrain
        };
    }

    private static VariationMode ParseVariationMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "pergeneration" or "per_generation" => VariationMode.PerGeneration,
            "contextual" => VariationMode.Contextual,
            _ => VariationMode.PerInstance
        };
    }

    private static string GetString(Dictionary<string, string> props, string key, string defaultValue)
        => props.GetValueOrDefault(key, defaultValue);

    private static string? GetStringOrNull(Dictionary<string, string> props, string key)
        => props.GetValueOrDefault(key);

    private static int GetInt(Dictionary<string, string> props, string key, int defaultValue)
        => props.TryGetValue(key, out var v) ? ParseInt(v, defaultValue) : defaultValue;

    private static float GetFloat(Dictionary<string, string> props, string key, float defaultValue)
        => props.TryGetValue(key, out var v) ? ParseFloat(v, defaultValue) : defaultValue;

    private static bool GetBool(Dictionary<string, string> props, string key, bool defaultValue)
        => props.TryGetValue(key, out var v) ? ParseBool(v, defaultValue) : defaultValue;

    private static int ParseInt(string? value, int defaultValue)
        => int.TryParse(value, out var result) ? result : defaultValue;

    private static float ParseFloat(string? value, float defaultValue)
        => float.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : defaultValue;

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrEmpty(value)) return defaultValue;
        return value.ToLowerInvariant() switch
        {
            "true" or "1" or "yes" => true,
            "false" or "0" or "no" => false,
            _ => defaultValue
        };
    }

    #endregion

    #region Data Classes

    private class WangSetData
    {
        /// <summary>
        /// Maps (wangSetName, bitmask) → list of tiles with that pattern.
        /// Multiple tiles = variations.
        /// </summary>
        public Dictionary<(string setName, int bitmask), List<WangTileInfo>> BitmaskToTiles { get; } = new();

        /// <summary>
        /// Maps tileId → which wang set it belongs to.
        /// </summary>
        public Dictionary<int, string> TileToWangSet { get; } = new();

        /// <summary>
        /// Maps tileId → bitmask type for that wang set.
        /// </summary>
        public Dictionary<int, BitmaskType> TileBitmaskType { get; } = new();

        /// <summary>
        /// Maps wang set name → info about the set (bitmask type, properties).
        /// </summary>
        public Dictionary<string, WangSetInfo> WangSetInfo { get; } = new();
    }

    private record WangSetInfo(string Name, BitmaskType BitmaskType, Dictionary<string, string> Properties);

    private record WangTileInfo(int TileId, float Probability);

    private record TilePropertyData(string? Type, Dictionary<string, string> Properties, List<AnimationFrame>? Animation);

    private record AnimationFrame(int TileId, float Duration);

    #endregion

    #region TMX Map Resolution

    /// <summary>
    /// Represents a chunk of tile data from a TMX infinite map.
    /// </summary>
    public class TmxChunk
    {
        public Vector2I ChunkPosition { get; }
        public int Width { get; }
        public int Height { get; }
        private readonly int[] _tiles;

        public TmxChunk(Vector2I chunkPosition, int width, int height, int[] tiles)
        {
            ChunkPosition = chunkPosition;
            Width = width;
            Height = height;
            _tiles = tiles;
        }

        public int GetTileAt(int localX, int localY)
        {
            if (localX < 0 || localX >= Width || localY < 0 || localY >= Height)
                return 0;
            return _tiles[localY * Width + localX];
        }
    }

    /// <summary>
    /// Reference to a tileset loaded from TMX, including tile definitions.
    /// </summary>
    public class TmxTilesetReference
    {
        public int FirstGid { get; }
        public int LastGid { get; }
        public string TsxPath { get; }
        public TileRegistryResult TilesetData { get; }
        public int Columns { get; }
        private readonly Dictionary<int, TileDefinition> _tileCache;

        public TmxTilesetReference(int firstGid, int lastGid, string tsxPath, TileRegistryResult tilesetData, int columns)
        {
            FirstGid = firstGid;
            LastGid = lastGid;
            TsxPath = tsxPath;
            TilesetData = tilesetData;
            Columns = columns;
            // Pre-build lookup cache - key by atlas coords converted to local ID
            _tileCache = new Dictionary<int, TileDefinition>();
            foreach (var tile in tilesetData.Tiles)
            {
                // Convert atlas coords back to local tile ID
                var localId = tile.AtlasCoords.Y * columns + tile.AtlasCoords.X;
                _tileCache[localId] = tile;
            }
        }

        public bool ContainsGlobalId(int globalId) => globalId >= FirstGid && globalId <= LastGid;

        public int GlobalToLocal(int globalId) => globalId - FirstGid;

        public TileDefinition? GetTileDefinition(int localId) => _tileCache.GetValueOrDefault(localId);

        /// <summary>
        /// Converts a local tile ID to atlas coordinates.
        /// </summary>
        public Vector2I LocalIdToAtlasCoords(int localId)
        {
            if (Columns <= 0) return new Vector2I(localId, 0);
            return new Vector2I(localId % Columns, localId / Columns);
        }
    }

    /// <summary>
    /// Result of resolving a tile at a specific map position.
    /// </summary>
    public record TmxTileResolution(
        int GlobalTileId,
        int LocalTileId,
        Vector2I AtlasCoords,
        TmxTilesetReference Tileset,
        TileDefinition? TileDefinition);

    /// <summary>
    /// Loaded TMX map data with chunk-based tile storage.
    /// </summary>
    public class TmxMapData
    {
        public Vector2I MapSize { get; }
        public List<TmxTilesetReference> Tilesets { get; }
        public int ChunkSize { get; }
        private readonly Dictionary<Vector2I, TmxChunk> _chunks;

        public TmxMapData(Vector2I mapSize, List<TmxTilesetReference> tilesets, Dictionary<Vector2I, TmxChunk> chunks, int chunkSize = 16)
        {
            MapSize = mapSize;
            Tilesets = tilesets;
            _chunks = chunks;
            ChunkSize = chunkSize;
        }

        /// <summary>
        /// Gets tile information at the specified world coordinates.
        /// </summary>
        public TmxTileResolution? GetTileAt(int x, int y)
        {
            // Calculate chunk coordinates (handle negative coords for infinite maps)
            var chunkX = x >= 0 ? x / ChunkSize : (x - ChunkSize + 1) / ChunkSize;
            var chunkY = y >= 0 ? y / ChunkSize : (y - ChunkSize + 1) / ChunkSize;
            var chunkCoord = new Vector2I(chunkX, chunkY);

            if (!_chunks.TryGetValue(chunkCoord, out var chunk))
                return null;

            // Calculate local position within chunk
            var localX = x - (chunkX * ChunkSize);
            var localY = y - (chunkY * ChunkSize);
            var globalId = chunk.GetTileAt(localX, localY);

            if (globalId == 0)
                return null;

            // Find which tileset contains this global ID
            foreach (var tileset in Tilesets)
            {
                if (tileset.ContainsGlobalId(globalId))
                {
                    var localId = tileset.GlobalToLocal(globalId);
                    var atlasCoords = tileset.LocalIdToAtlasCoords(localId);
                    var tileDef = tileset.GetTileDefinition(localId);
                    return new TmxTileResolution(globalId, localId, atlasCoords, tileset, tileDef);
                }
            }

            return null;
        }

        /// <summary>
        /// Gets all non-empty tiles in the map as coordinate-resolution pairs.
        /// </summary>
        public IEnumerable<(Vector2I Coord, TmxTileResolution Resolution)> GetAllTiles()
        {
            foreach (var (chunkCoord, chunk) in _chunks)
            {
                for (var ly = 0; ly < chunk.Height; ly++)
                for (var lx = 0; lx < chunk.Width; lx++)
                {
                    var worldX = chunkCoord.X * ChunkSize + lx;
                    var worldY = chunkCoord.Y * ChunkSize + ly;
                    var resolution = GetTileAt(worldX, worldY);
                    if (resolution != null)
                        yield return (new Vector2I(worldX, worldY), resolution);
                }
            }
        }

        /// <summary>
        /// Gets the bounds of the map (min/max coordinates with tiles).
        /// </summary>
        public (Vector2I Min, Vector2I Max) GetBounds()
        {
            if (_chunks.Count == 0)
                return (Vector2I.Zero, Vector2I.Zero);

            var minX = int.MaxValue;
            var minY = int.MaxValue;
            var maxX = int.MinValue;
            var maxY = int.MinValue;

            foreach (var chunkCoord in _chunks.Keys)
            {
                minX = Math.Min(minX, chunkCoord.X * ChunkSize);
                minY = Math.Min(minY, chunkCoord.Y * ChunkSize);
                maxX = Math.Max(maxX, (chunkCoord.X + 1) * ChunkSize - 1);
                maxY = Math.Max(maxY, (chunkCoord.Y + 1) * ChunkSize - 1);
            }

            return (new Vector2I(minX, minY), new Vector2I(maxX, maxY));
        }
    }

    /// <summary>
    /// Loads a TMX map file with full tile resolution support.
    /// </summary>
    public static TmxMapData? LoadTmxMap(string tmxPath)
    {
        var absolutePath = ProjectSettings.GlobalizePath(tmxPath);
        if (!File.Exists(absolutePath))
        {
            ILog.Print($"[TiledTilesetLoader] TMX file not found: {absolutePath}");
            return null;
        }

        try
        {
            var doc = XDocument.Load(absolutePath);
            var map = doc.Root;
            if (map == null || map.Name != "map")
            {
                ILog.Print("[TiledTilesetLoader] Invalid TMX: missing map root element");
                return null;
            }

            var tmxDir = Path.GetDirectoryName(absolutePath) ?? "";
            var mapWidth = ParseInt(map.Attribute("width")?.Value, 0);
            var mapHeight = ParseInt(map.Attribute("height")?.Value, 0);

            // Load tileset references
            var tilesets = new List<TmxTilesetReference>();
            foreach (var tilesetRef in map.Elements("tileset"))
            {
                var firstGid = ParseInt(tilesetRef.Attribute("firstgid")?.Value, 1);
                var source = tilesetRef.Attribute("source")?.Value;

                // Skip internal/builtin tilesets (e.g., ":/" paths)
                if (string.IsNullOrEmpty(source) || source.StartsWith(":/"))
                    continue;

                var tsxAbsolutePath = Path.GetFullPath(Path.Combine(tmxDir, source));
                if (!File.Exists(tsxAbsolutePath))
                {
                    ILog.Print($"[TiledTilesetLoader] TSX file not found: {tsxAbsolutePath}");
                    continue;
                }

                // Load TSX to get tile count and columns
                var tsxDoc = XDocument.Load(tsxAbsolutePath);
                var tileset = tsxDoc.Root;
                var tileCount = ParseInt(tileset?.Attribute("tilecount")?.Value, 0);
                var columns = ParseInt(tileset?.Attribute("columns")?.Value, 1);
                var lastGid = firstGid + tileCount - 1;

                // Load tile definitions
                var tilesetData = LoadFromTsx(tsxAbsolutePath, 0);

                tilesets.Add(new TmxTilesetReference(firstGid, lastGid, tsxAbsolutePath, tilesetData, columns));
            }

            // Load layer chunks
            var chunks = new Dictionary<Vector2I, TmxChunk>();
            foreach (var layer in map.Elements("layer"))
            {
                var data = layer.Element("data");
                if (data == null) continue;

                foreach (var chunkElement in data.Elements("chunk"))
                {
                    var chunkX = ParseInt(chunkElement.Attribute("x")?.Value, 0);
                    var chunkY = ParseInt(chunkElement.Attribute("y")?.Value, 0);
                    var chunkWidth = ParseInt(chunkElement.Attribute("width")?.Value, 16);
                    var chunkHeight = ParseInt(chunkElement.Attribute("height")?.Value, 16);

                    var csv = chunkElement.Value.Trim();
                    var tiles = csv.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => ParseInt(s.Trim(), 0))
                        .ToArray();

                    // Chunk coord is based on tile position divided by chunk size
                    var chunkCoord = new Vector2I(chunkX / chunkWidth, chunkY / chunkHeight);
                    chunks[chunkCoord] = new TmxChunk(chunkCoord, chunkWidth, chunkHeight, tiles);
                }
            }

            ILog.Print($"[TiledTilesetLoader] Loaded TMX map: {chunks.Count} chunks, {tilesets.Count} tilesets");
            return new TmxMapData(new Vector2I(mapWidth, mapHeight), tilesets, chunks);
        }
        catch (Exception ex)
        {
            ILog.Print($"[TiledTilesetLoader] Error loading TMX map: {ex.Message}");
            return null;
        }
    }

    #endregion
}
