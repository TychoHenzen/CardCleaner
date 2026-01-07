#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Compiles tiles from TMX/TSX source files into a single atlas texture with coordinate mapping.
/// This is an alternative to TileAtlasCompiler that reads directly from Tiled files
/// instead of the JSON-based tiles.json.
/// </summary>
public class TmxAtlasCompiler
{
    private const int MaxAtlasSize = 16384;
    private const string CompiledAtlasDir = "res://Data/CompiledAtlas";
    private const string AtlasFileName = "terrain_atlas.png";
    private const string MappingFileName = "atlas_mapping.json";
    private const string TransitionMapFileName = "transition_map.json";

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Compiles all tiles from TSX files in the specified directory into a single atlas.
    /// Returns (success, message).
    /// </summary>
    /// <param name="tiledDirectory">Directory containing TSX files (e.g., "res://Data/Tiled")</param>
    /// <param name="targetTileSize">Target tile size in output atlas (default 16)</param>
    public (bool success, string message) CompileFromTmx(string tiledDirectory, int targetTileSize = 16)
    {
        var absoluteDir = ProjectSettings.GlobalizePath(tiledDirectory);
        if (!Directory.Exists(absoluteDir))
        {
            return (false, $"Directory not found: {absoluteDir}");
        }

        // Find all TSX files
        var tsxFiles = Directory.GetFiles(absoluteDir, "*.tsx", SearchOption.AllDirectories);
        if (tsxFiles.Length == 0)
        {
            return (false, "No TSX files found in directory");
        }

        GD.Print($"[TmxAtlasCompiler] Found {tsxFiles.Length} TSX file(s)");

        // Create output directory
        var outputDir = ProjectSettings.GlobalizePath(CompiledAtlasDir);
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        // Collect tiles from all TSX files
        var allTiles = new List<TsxTileData>();
        var allWangSets = new List<TsxWangSetData>();

        foreach (var tsxPath in tsxFiles)
        {
            var result = LoadTsxFile(tsxPath, targetTileSize);
            if (result.success)
            {
                allTiles.AddRange(result.tiles);
                allWangSets.AddRange(result.wangSets);
                GD.Print($"[TmxAtlasCompiler] Loaded {result.tiles.Count} tiles, {result.wangSets.Count} wang sets from {Path.GetFileName(tsxPath)}");
            }
            else
            {
                GD.PrintErr($"[TmxAtlasCompiler] Failed to load {tsxPath}: {result.error}");
            }
        }

        if (allTiles.Count == 0)
        {
            return (false, "No tiles found in TSX files");
        }

        // Pack tiles into atlas
        var packResult = PackTiles(allTiles, targetTileSize);
        if (!packResult.success)
        {
            return (false, packResult.message);
        }

        // Create atlas image
        var atlasImage = CreateAtlasImage(packResult.packedTiles, packResult.atlasSize, targetTileSize);
        if (atlasImage == null)
        {
            return (false, "Failed to create atlas image");
        }

        // Save atlas PNG
        var atlasPath = $"{CompiledAtlasDir}/{AtlasFileName}";
        var absoluteAtlasPath = ProjectSettings.GlobalizePath(atlasPath);
        var saveError = atlasImage.SavePng(absoluteAtlasPath);
        if (saveError != Error.Ok)
        {
            return (false, $"Failed to save atlas: {saveError}");
        }

        GD.Print($"[TmxAtlasCompiler] Saved atlas {packResult.atlasSize.X}x{packResult.atlasSize.Y} to {atlasPath}");

        // Save atlas mapping JSON
        var mappingResult = SaveAtlasMapping(packResult.mapping, packResult.atlasSize, targetTileSize, atlasPath);
        if (!mappingResult.success)
        {
            return (false, mappingResult.message);
        }

        // Generate and save transition map
        var transitionMap = GenerateTransitionMap(allTiles, allWangSets, packResult.mapping, targetTileSize);
        var transitionResult = SaveTransitionMap(transitionMap);
        if (!transitionResult.success)
        {
            return (false, transitionResult.message);
        }

        return (true, $"Compiled {allTiles.Count} tiles from {tsxFiles.Length} TSX file(s) to {atlasPath}");
    }

    /// <summary>
    /// Loads tile data from a TSX file.
    /// </summary>
    private (bool success, string error, List<TsxTileData> tiles, List<TsxWangSetData> wangSets) LoadTsxFile(
        string tsxPath, int targetTileSize)
    {
        var tiles = new List<TsxTileData>();
        var wangSets = new List<TsxWangSetData>();

        try
        {
            var doc = XDocument.Load(tsxPath);
            var tileset = doc.Root;
            if (tileset == null || tileset.Name != "tileset")
            {
                return (false, "Invalid TSX: missing tileset root", tiles, wangSets);
            }

            var tileWidth = int.Parse(tileset.Attribute("tilewidth")?.Value ?? "16");
            var tileHeight = int.Parse(tileset.Attribute("tileheight")?.Value ?? "16");
            var tileCount = int.Parse(tileset.Attribute("tilecount")?.Value ?? "0");
            var columns = int.Parse(tileset.Attribute("columns")?.Value ?? "1");

            // Calculate source scale (TSX tile size vs target tile size)
            var sourceScale = (float)targetTileSize / tileWidth;

            // Get image path
            var imageElement = tileset.Element("image");
            if (imageElement == null)
            {
                return (false, "TSX has no image element", tiles, wangSets);
            }

            var imageSource = imageElement.Attribute("source")?.Value;
            if (string.IsNullOrEmpty(imageSource))
            {
                return (false, "TSX image has no source", tiles, wangSets);
            }

            // Resolve image path relative to TSX
            var tsxDir = Path.GetDirectoryName(tsxPath) ?? "";
            var absoluteImagePath = Path.GetFullPath(Path.Combine(tsxDir, imageSource));

            if (!File.Exists(absoluteImagePath))
            {
                return (false, $"Image not found: {absoluteImagePath}", tiles, wangSets);
            }

            // Load the source image
            var image = Image.LoadFromFile(absoluteImagePath);
            if (image == null)
            {
                return (false, $"Failed to load image: {absoluteImagePath}", tiles, wangSets);
            }

            // Parse tile properties
            var tilePropsMap = new Dictionary<int, Dictionary<string, string>>();
            foreach (var tileElement in tileset.Elements("tile"))
            {
                var tileId = int.Parse(tileElement.Attribute("id")?.Value ?? "-1");
                if (tileId >= 0)
                {
                    var props = ParseProperties(tileElement.Element("properties"));
                    tilePropsMap[tileId] = props;
                }
            }

            // Create tile data for each tile in tileset
            for (var i = 0; i < tileCount; i++)
            {
                var atlasX = i % columns;
                var atlasY = i / columns;

                // Get properties for this tile
                tilePropsMap.TryGetValue(i, out var props);
                props ??= new Dictionary<string, string>();

                var tileData = new TsxTileData
                {
                    TileId = i,
                    TsxPath = tsxPath,
                    AtlasX = atlasX,
                    AtlasY = atlasY,
                    SourceTileWidth = tileWidth,
                    SourceTileHeight = tileHeight,
                    SourceScale = sourceScale,
                    SourceImage = image,
                    Properties = props,
                    Id = GetString(props, "id", $"tile_{i}"),
                    Layer = GetString(props, "layer", "terrain"),
                    Dominance = GetInt(props, "dominance", 0)
                };

                tiles.Add(tileData);
            }

            // Parse wang sets
            var wangSetsElement = tileset.Element("wangsets");
            if (wangSetsElement != null)
            {
                foreach (var wangSetElement in wangSetsElement.Elements("wangset"))
                {
                    var name = wangSetElement.Attribute("name")?.Value ?? "unknown";
                    var type = wangSetElement.Attribute("type")?.Value ?? "corner";
                    var props = ParseProperties(wangSetElement.Element("properties"));

                    var wangTiles = new Dictionary<int, int>(); // wangid -> tileId
                    foreach (var wangTile in wangSetElement.Elements("wangtile"))
                    {
                        var tileId = int.Parse(wangTile.Attribute("tileid")?.Value ?? "-1");
                        var wangIdStr = wangTile.Attribute("wangid")?.Value ?? "";

                        // Parse wangid (format: "0,1,0,1,0,1,0,1")
                        var wangId = ParseWangId(wangIdStr, type);
                        if (tileId >= 0 && wangId >= 0)
                        {
                            wangTiles[wangId] = tileId;
                        }
                    }

                    wangSets.Add(new TsxWangSetData
                    {
                        Name = name,
                        Type = type,
                        TsxPath = tsxPath,
                        Columns = columns,
                        SourceScale = sourceScale,
                        Properties = props,
                        WangTiles = wangTiles,
                        SourceImage = image,
                        SourceTileWidth = tileWidth,
                        SourceTileHeight = tileHeight,
                        IsTransparent = GetBool(props, "TransparentBackground", false),
                        OuterTerrain = GetString(props, "OuterTerrain", ""),
                        InnerTerrain = GetString(props, "InnerTerrain", "")
                    });
                }
            }

            return (true, "", tiles, wangSets);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, tiles, wangSets);
        }
    }

    /// <summary>
    /// Parses a Tiled wang ID string into a corner16 bitmask.
    /// Wang ID format: "0,TR,0,BR,0,BL,0,TL" for corners
    /// </summary>
    private int ParseWangId(string wangIdStr, string wangType)
    {
        if (string.IsNullOrEmpty(wangIdStr)) return -1;

        var parts = wangIdStr.Split(',');
        if (parts.Length != 8) return -1;

        // For corner wang sets, we extract the 4 corner values
        // Tiled order: 0,TR,0,BR,0,BL,0,TL (odd indices)
        var tr = int.Parse(parts[1]);
        var br = int.Parse(parts[3]);
        var bl = int.Parse(parts[5]);
        var tl = int.Parse(parts[7]);

        // Convert to our corner16 bitmask format:
        // bit 0 = TL, bit 1 = TR, bit 2 = BL, bit 3 = BR
        var bitmask = 0;
        if (tl > 0) bitmask |= 1;  // TL
        if (tr > 0) bitmask |= 2;  // TR
        if (bl > 0) bitmask |= 4;  // BL
        if (br > 0) bitmask |= 8;  // BR

        return bitmask;
    }

    private Dictionary<string, string> ParseProperties(XElement? propsElement)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (propsElement == null) return result;

        foreach (var prop in propsElement.Elements("property"))
        {
            var name = prop.Attribute("name")?.Value;
            var value = prop.Attribute("value")?.Value ?? prop.Value;
            if (!string.IsNullOrEmpty(name))
            {
                result[name] = value;
            }
        }

        return result;
    }

    private string GetString(Dictionary<string, string> props, string key, string defaultValue)
        => props.TryGetValue(key, out var value) ? value : defaultValue;

    private int GetInt(Dictionary<string, string> props, string key, int defaultValue)
        => props.TryGetValue(key, out var value) && int.TryParse(value, out var result) ? result : defaultValue;

    private bool GetBool(Dictionary<string, string> props, string key, bool defaultValue)
        => props.TryGetValue(key, out var value)
            ? value.ToLowerInvariant() is "true" or "1"
            : defaultValue;

    /// <summary>
    /// Packs tiles into an atlas using simple row-based bin packing.
    /// </summary>
    private (bool success, string message, List<PackedTsxTile> packedTiles, Vector2I atlasSize,
        Dictionary<string, Dictionary<string, TileAtlasRect>> mapping)
        PackTiles(List<TsxTileData> tiles, int targetTileSize)
    {
        var packed = new List<PackedTsxTile>();
        var mapping = new Dictionary<string, Dictionary<string, TileAtlasRect>>();

        // Deduplicate tiles by source image + atlas coords
        var uniqueTiles = new Dictionary<string, TsxTileData>();
        foreach (var tile in tiles)
        {
            var key = $"{tile.TsxPath}:{tile.AtlasX},{tile.AtlasY}";
            if (!uniqueTiles.ContainsKey(key))
            {
                uniqueTiles[key] = tile;
            }
        }

        var tilesToPack = uniqueTiles.Values.ToList();

        // Simple row-based packing
        var currentX = 0;
        var currentY = 0;
        var rowHeight = targetTileSize;
        var maxRowWidth = 0;

        foreach (var tile in tilesToPack)
        {
            var tilePixelWidth = targetTileSize;
            var tilePixelHeight = targetTileSize;

            // Check if we need to start a new row
            if (currentX + tilePixelWidth > MaxAtlasSize)
            {
                currentX = 0;
                currentY += rowHeight;
                rowHeight = tilePixelHeight;
            }

            // Check if atlas is full
            if (currentY + tilePixelHeight > MaxAtlasSize)
            {
                return (false, $"Atlas exceeds {MaxAtlasSize}x{MaxAtlasSize}", packed, Vector2I.Zero, mapping);
            }

            // Place tile
            packed.Add(new PackedTsxTile
            {
                Source = tile,
                AtlasX = currentX,
                AtlasY = currentY
            });

            // Create mapping entry
            var sourceKey = tile.TsxPath;
            var coordKey = $"{tile.AtlasX},{tile.AtlasY}";

            if (!mapping.ContainsKey(sourceKey))
                mapping[sourceKey] = new Dictionary<string, TileAtlasRect>();

            mapping[sourceKey][coordKey] = new TileAtlasRect
            {
                X = currentX / targetTileSize,
                Y = currentY / targetTileSize,
                W = 1,
                H = 1
            };

            currentX += tilePixelWidth;
            maxRowWidth = Math.Max(maxRowWidth, currentX);
        }

        // Calculate final atlas size (round up to power of 2)
        var finalHeight = currentY + rowHeight;
        var atlasWidth = NextPowerOf2(maxRowWidth);
        var atlasHeight = NextPowerOf2(finalHeight);

        return (true, "", packed, new Vector2I(atlasWidth, atlasHeight), mapping);
    }

    private int NextPowerOf2(int value)
    {
        var power = 1;
        while (power < value)
            power *= 2;
        return Math.Min(power, MaxAtlasSize);
    }

    /// <summary>
    /// Creates the atlas image from packed tiles.
    /// </summary>
    private Image? CreateAtlasImage(List<PackedTsxTile> packedTiles, Vector2I atlasSize, int targetTileSize)
    {
        try
        {
            var atlasImage = Image.CreateEmpty(atlasSize.X, atlasSize.Y, false, Image.Format.Rgba8);
            atlasImage.Fill(new Color(0, 0, 0, 0)); // Transparent background

            foreach (var packed in packedTiles)
            {
                var tile = packed.Source;
                var sourceImage = tile.SourceImage;

                // Calculate source rectangle
                var srcRect = new Rect2I(
                    tile.AtlasX * tile.SourceTileWidth,
                    tile.AtlasY * tile.SourceTileHeight,
                    tile.SourceTileWidth,
                    tile.SourceTileHeight
                );

                // Extract tile region
                var extractedTile = Image.CreateEmpty(tile.SourceTileWidth, tile.SourceTileHeight, false, Image.Format.Rgba8);
                extractedTile.BlitRect(sourceImage, srcRect, Vector2I.Zero);

                // Resize if needed
                if (tile.SourceTileWidth != targetTileSize || tile.SourceTileHeight != targetTileSize)
                {
                    extractedTile.Resize(targetTileSize, targetTileSize, Image.Interpolation.Nearest);
                }

                // Blit to atlas
                var dstPos = new Vector2I(packed.AtlasX, packed.AtlasY);
                atlasImage.BlitRect(extractedTile, new Rect2I(0, 0, targetTileSize, targetTileSize), dstPos);
            }

            return atlasImage;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TmxAtlasCompiler] Error creating atlas: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Generates the transition map for auto-tiles.
    /// </summary>
    private CompiledTransitionMap GenerateTransitionMap(
        List<TsxTileData> tiles,
        List<TsxWangSetData> wangSets,
        Dictionary<string, Dictionary<string, TileAtlasRect>> mapping,
        int targetTileSize)
    {
        var transitionMap = new CompiledTransitionMap();

        foreach (var wangSet in wangSets)
        {
            // Get mapped coordinates for each wang tile
            var variantCoords = new Vector2I[16]; // Corner16 has 16 variants
            var hasAnyVariants = false;

            for (var bitmask = 0; bitmask < 16; bitmask++)
            {
                if (wangSet.WangTiles.TryGetValue(bitmask, out var tileId))
                {
                    // Convert tile ID to atlas coords
                    var atlasX = tileId % wangSet.Columns;
                    var atlasY = tileId / wangSet.Columns;
                    var coordKey = $"{atlasX},{atlasY}";

                    if (mapping.TryGetValue(wangSet.TsxPath, out var sourceMapping) &&
                        sourceMapping.TryGetValue(coordKey, out var rect))
                    {
                        variantCoords[bitmask] = new Vector2I(rect.X, rect.Y);
                        hasAnyVariants = true;
                    }
                }
            }

            if (hasAnyVariants)
            {
                // Generate tile ID from wang set name
                var tileId = ToSnakeCase(wangSet.Name);
                var outerTerrain = string.IsNullOrEmpty(wangSet.OuterTerrain) ? "*" : wangSet.OuterTerrain;

                transitionMap.AddTransition(tileId, outerTerrain, "corner16", variantCoords);

                GD.Print($"[TmxAtlasCompiler] Added transition for {tileId} -> {outerTerrain}");
            }
        }

        return transitionMap;
    }

    private string ToSnakeCase(string name)
    {
        // Simple conversion: Grass3 -> grass3
        return name.ToLowerInvariant().Replace(" ", "_");
    }

    /// <summary>
    /// Saves the atlas mapping JSON file.
    /// </summary>
    private (bool success, string message) SaveAtlasMapping(
        Dictionary<string, Dictionary<string, TileAtlasRect>> mapping,
        Vector2I atlasSize,
        int tileSize,
        string atlasPath)
    {
        try
        {
            // Convert paths to source IDs (using hash of path as ID)
            var sourcesMapping = new Dictionary<string, Dictionary<string, TileAtlasRect>>();
            foreach (var (path, coords) in mapping)
            {
                var sourceId = Math.Abs(path.GetHashCode()).ToString();
                sourcesMapping[sourceId] = coords;
            }

            var data = new AtlasMappingData
            {
                Version = "1.0",
                Atlas = new AtlasInfo
                {
                    Path = atlasPath,
                    Width = atlasSize.X,
                    Height = atlasSize.Y,
                    TileSize = tileSize
                },
                Sources = sourcesMapping
            };

            var json = JsonSerializer.Serialize(data, JsonWriteOptions);
            var absolutePath = ProjectSettings.GlobalizePath($"{CompiledAtlasDir}/{MappingFileName}");
            File.WriteAllText(absolutePath, json);

            GD.Print($"[TmxAtlasCompiler] Saved mapping to {MappingFileName}");
            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, $"Error saving mapping: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves the transition map JSON file.
    /// </summary>
    private (bool success, string message) SaveTransitionMap(CompiledTransitionMap transitionMap)
    {
        try
        {
            var json = JsonSerializer.Serialize(transitionMap, JsonWriteOptions);
            var absolutePath = ProjectSettings.GlobalizePath($"{CompiledAtlasDir}/{TransitionMapFileName}");
            File.WriteAllText(absolutePath, json);

            GD.Print($"[TmxAtlasCompiler] Saved transition map with {transitionMap.Transitions.Count} entries");
            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, $"Error saving transition map: {ex.Message}");
        }
    }

    // Data classes
    private class TsxTileData
    {
        public int TileId { get; set; }
        public string TsxPath { get; set; } = "";
        public int AtlasX { get; set; }
        public int AtlasY { get; set; }
        public int SourceTileWidth { get; set; }
        public int SourceTileHeight { get; set; }
        public float SourceScale { get; set; }
        public Image SourceImage { get; set; } = null!;
        public Dictionary<string, string> Properties { get; set; } = new();
        public string Id { get; set; } = "";
        public string Layer { get; set; } = "terrain";
        public int Dominance { get; set; }
    }

    private class TsxWangSetData
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "corner";
        public string TsxPath { get; set; } = "";
        public int Columns { get; set; }
        public float SourceScale { get; set; }
        public Dictionary<string, string> Properties { get; set; } = new();
        public Dictionary<int, int> WangTiles { get; set; } = new(); // bitmask -> tileId
        public Image SourceImage { get; set; } = null!;
        public int SourceTileWidth { get; set; }
        public int SourceTileHeight { get; set; }
        public bool IsTransparent { get; set; }
        public string OuterTerrain { get; set; } = "";
        public string InnerTerrain { get; set; } = "";
    }

    private class PackedTsxTile
    {
        public TsxTileData Source { get; set; } = null!;
        public int AtlasX { get; set; }
        public int AtlasY { get; set; }
    }

    // JSON serialization classes
    private class AtlasMappingData
    {
        [JsonPropertyName("version")] public string Version { get; set; } = "1.0";
        [JsonPropertyName("atlas")] public AtlasInfo Atlas { get; set; } = new();
        [JsonPropertyName("sources")] public Dictionary<string, Dictionary<string, TileAtlasRect>> Sources { get; set; } = new();
    }

    private class AtlasInfo
    {
        [JsonPropertyName("path")] public string Path { get; set; } = "";
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
        [JsonPropertyName("tileSize")] public int TileSize { get; set; }
    }

    private class TileAtlasRect
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("w")] public int W { get; set; }
        [JsonPropertyName("h")] public int H { get; set; }
    }
}
#endif
