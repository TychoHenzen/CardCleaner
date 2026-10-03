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
                GD.Print(
                    $"[TmxAtlasCompiler] Loaded {result.tiles.Count} tiles, " +
                    $"{result.wangSets.Count} wang sets from {Path.GetFileName(tsxPath)}");
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

        // Identify simple base terrain tiles:
        // - Must have class/type attribute in TSX (explicitly defined game tiles)
        // - Must have layer="terrain" (not "wang")
        // - Must not be part of any Wang set (uses AllMemberTileIds to include filtered tiles)
        var simpleBaseTerrains = allTiles
            .Where(t => t.HasClassAttribute && // Only tiles with class/type attr in TSX
                   t.Layer.ToLowerInvariant() == "terrain" &&
                   !allWangSets.Any(ws => ws.AllMemberTileIds.Contains(t.TileId) && ws.TsxPath == t.TsxPath))
            .GroupBy(t => t.Id)
            .Select(g => g.First())
            .ToList();

        // Identify compositable wang sets (OuterTerrain="*" and TransparentBackground=true)
        var compositableWangSets = allWangSets
            .Where(ws => ws.OuterTerrain == "*" && ws.IsTransparent)
            .ToList();

        // Identify fixed wang sets (have specific outer terrain or no transparency)
        var fixedWangSets = allWangSets
            .Where(ws => ws.OuterTerrain != "*" || !ws.IsTransparent)
            .ToList();

        // Wang sets with empty InnerTerrain (or $self) can use their solid fill (bitmask 15) as base terrain
        // This allows compositing different Wang sets onto each other
        var wangSetSolidFills = new List<TsxTileData>();
        foreach (var ws in compositableWangSets)
        {
            var innerTerrain = ws.InnerTerrain;
            if (string.IsNullOrEmpty(innerTerrain) || innerTerrain == "$self")
            {
                // Bitmask 15 is the solid fill (all corners same terrain)
                if (ws.WangTiles.TryGetValue(15, out var solidTileIds) && solidTileIds.Count > 0)
                {
                    var solidTileId = solidTileIds[0]; // Use first variant as representative
                    var solidTile = allTiles.FirstOrDefault(t =>
                        t.TileId == solidTileId && t.TsxPath == ws.TsxPath);
                    if (solidTile != null)
                    {
                        // Create a virtual base terrain entry for this wang set's solid fill
                        var solidFillBase = new TsxTileData
                        {
                            TileId = solidTileId,
                            TsxPath = ws.TsxPath,
                            AtlasX = solidTileId % ws.Columns,
                            AtlasY = solidTileId / ws.Columns,
                            SourceImage = ws.SourceImage,
                            SourceTileWidth = ws.SourceTileWidth,
                            SourceTileHeight = ws.SourceTileHeight,
                            SourceScale = ws.SourceScale,
                            Id = ToSnakeCase(ws.Name),
                            Layer = "terrain",
                            Dominance = 0,
                            Properties = solidTile.Properties
                        };
                        wangSetSolidFills.Add(solidFillBase);
                    }
                }
            }
        }

        // Combined base terrains: simple tiles + wang set solid fills
        var baseTerrains = simpleBaseTerrains.Concat(wangSetSolidFills).ToList();

        GD.Print($"[TmxAtlasCompiler] Found {simpleBaseTerrains.Count} simple base terrains, " +
                 $"{wangSetSolidFills.Count} wang set solid fills, " +
                 $"{compositableWangSets.Count} compositable wang sets, " +
                 $"{fixedWangSets.Count} fixed wang sets");

        // Pack base tiles into atlas (excluding compositable wang set tiles)
        var tilesToPack = allTiles
            .Where(t => !compositableWangSets.Any(ws =>
                ws.AllMemberTileIds.Contains(t.TileId) && ws.TsxPath == t.TsxPath))
            .ToList();

        // Estimate total tile count to calculate optimal atlas width
        // Composites: for each wang set × base terrain, generate up to 16 bitmasks with variants + 2 diagonals
        var estimatedComposites = 0;
        foreach (var wangSet in compositableWangSets)
        {
            var validBaseTerrains = baseTerrains.Count(bt => bt.Id != ToSnakeCase(wangSet.Name));
            var variantsPerWangSet = wangSet.WangTiles.Values.Sum(list => list.Count);
            // Each base terrain gets: variants for existing bitmasks + 16 fallback tiles + up to 2 diagonals
            estimatedComposites += validBaseTerrains * (variantsPerWangSet + 16 + 2);
        }

        var totalEstimatedTiles = tilesToPack.Count + estimatedComposites;
        var optimalAtlasWidth = CalculateOptimalAtlasWidth(totalEstimatedTiles, targetTileSize);

        GD.Print(
            $"[TmxAtlasCompiler] Estimated {totalEstimatedTiles} total tiles, " +
            $"using atlas width {optimalAtlasWidth}px");

        var packResult = PackTiles(tilesToPack, targetTileSize, optimalAtlasWidth);
        if (!packResult.success)
        {
            return (false, packResult.message);
        }

        // Create atlas image with base tiles, using optimal width
        var atlasImage = CreateAtlasImage(packResult.packedTiles, packResult.atlasSize, targetTileSize);
        if (atlasImage == null)
        {
            return (false, "Failed to create atlas image");
        }

        // Initialize transition map
        var transitionMap = new CompiledTransitionMap();

        // Track current atlas position for composite tiles
        var compositeCurrentX = 0;
        var compositeCurrentY = packResult.atlasSize.Y; // Start below packed tiles
        var compositeRowHeight = targetTileSize;
        var compositeAtlasWidth = optimalAtlasWidth; // Use optimal width, not packed width
        var compositesGenerated = 0;

        // Generate composite tiles for compositable wang sets
        foreach (var wangSet in compositableWangSets)
        {
            var wangSetId = ToSnakeCase(wangSet.Name);

            foreach (var baseTerrain in baseTerrains)
            {
                // Skip if base terrain is the same wang set's solid fill
                if (baseTerrain.Id == wangSetId)
                    continue;

                // Generate composites for all 16 bitmasks, with multiple variants per bitmask
                var variantCoords = new List<Vector2I>[16];
                for (var i = 0; i < 16; i++)
                    variantCoords[i] = new List<Vector2I>();

                for (var bitmask = 0; bitmask < 16; bitmask++)
                {
                    if (wangSet.WangTiles.TryGetValue(bitmask, out var borderTileIds) && borderTileIds.Count > 0)
                    {
                        // Generate a composite for EACH variant tile
                        foreach (var borderTileId in borderTileIds)
                        {
                            // Extract base terrain image
                            var baseImage = ExtractTileRegion(
                                baseTerrain.SourceImage,
                                baseTerrain.AtlasX,
                                baseTerrain.AtlasY,
                                baseTerrain.SourceTileWidth,
                                baseTerrain.SourceTileHeight,
                                targetTileSize);

                            // Extract border variant image
                            var borderAtlasX = borderTileId % wangSet.Columns;
                            var borderAtlasY = borderTileId / wangSet.Columns;
                            var borderImage = ExtractTileRegion(
                                wangSet.SourceImage,
                                borderAtlasX,
                                borderAtlasY,
                                wangSet.SourceTileWidth,
                                wangSet.SourceTileHeight,
                                targetTileSize);

                            // Composite border onto base
                            var compositeImage = CompositeImages(baseImage, borderImage);

                            // Pack into atlas
                            (atlasImage, compositeCurrentX, compositeCurrentY) = PackCompositeToAtlas(
                                atlasImage, compositeImage, targetTileSize,
                                compositeCurrentX, compositeCurrentY, compositeRowHeight, compositeAtlasWidth);

                            // Track variant position (in tile units)
                            variantCoords[bitmask].Add(
                                new Vector2I(
                                    compositeCurrentX / targetTileSize,
                                    compositeCurrentY / targetTileSize));

                            compositeCurrentX += targetTileSize;
                            compositesGenerated++;
                        }
                    }
                    else
                    {
                        // No border for this bitmask, use base terrain directly (single variant)
                        var compositeImage = ExtractTileRegion(
                            baseTerrain.SourceImage,
                            baseTerrain.AtlasX,
                            baseTerrain.AtlasY,
                            baseTerrain.SourceTileWidth,
                            baseTerrain.SourceTileHeight,
                            targetTileSize);

                        // Pack into atlas
                        (atlasImage, compositeCurrentX, compositeCurrentY) = PackCompositeToAtlas(
                            atlasImage, compositeImage, targetTileSize,
                            compositeCurrentX, compositeCurrentY, compositeRowHeight, compositeAtlasWidth);

                        // Track variant position (in tile units)
                        variantCoords[bitmask].Add(
                            new Vector2I(
                                compositeCurrentX / targetTileSize,
                                compositeCurrentY / targetTileSize));

                        compositeCurrentX += targetTileSize;
                        compositesGenerated++;
                    }
                }

                // Generate missing diagonal tiles (bitmask 5 = NE+SW, bitmask 10 = SE+NW)
                // Only generate if the diagonal bitmask is NOT present in the source wang set
                // Layering: base terrain → north corner → south corner (north below south)
                GenerateDiagonalIfMissing(wangSet, baseTerrain, 5, 1, 4, targetTileSize,
                    ref atlasImage, ref compositeCurrentX, ref compositeCurrentY,
                    compositeRowHeight, compositeAtlasWidth, variantCoords, ref compositesGenerated);

                GenerateDiagonalIfMissing(wangSet, baseTerrain, 10, 8, 2, targetTileSize,
                    ref atlasImage, ref compositeCurrentX, ref compositeCurrentY,
                    compositeRowHeight, compositeAtlasWidth, variantCoords, ref compositesGenerated);

                // Add to transition map with all variants
                var variantArrays = variantCoords.Select(list => list.ToArray()).ToArray();
                transitionMap.AddTransitionWithVariants(wangSetId, baseTerrain.Id, "corner16", variantArrays);
            }
        }

        // Add fixed wang sets to transition map (non-compositable)
        foreach (var wangSet in fixedWangSets)
        {
            var wangSetId = ToSnakeCase(wangSet.Name);
            var variantCoords = new List<Vector2I>[16];
            for (var i = 0; i < 16; i++)
                variantCoords[i] = new List<Vector2I>();
            var hasAnyVariants = false;

            for (var bitmask = 0; bitmask < 16; bitmask++)
            {
                if (wangSet.WangTiles.TryGetValue(bitmask, out var tileIds) && tileIds.Count > 0)
                {
                    // Collect ALL variant tiles for this bitmask
                    foreach (var tileId in tileIds)
                    {
                        var atlasX = tileId % wangSet.Columns;
                        var atlasY = tileId / wangSet.Columns;
                        var coordKey = $"{atlasX},{atlasY}";

                        if (packResult.mapping.TryGetValue(wangSet.TsxPath, out var sourceMapping) &&
                            sourceMapping.TryGetValue(coordKey, out var rect))
                        {
                            variantCoords[bitmask].Add(new Vector2I(rect.X, rect.Y));
                            hasAnyVariants = true;
                        }
                    }
                }
            }

            if (hasAnyVariants)
            {
                var outerTerrain = string.IsNullOrEmpty(wangSet.OuterTerrain) ? "*" : wangSet.OuterTerrain;
                var variantArrays = variantCoords.Select(list => list.ToArray()).ToArray();
                transitionMap.AddTransitionWithVariants(wangSetId, outerTerrain, "corner16", variantArrays);
            }
        }

        GD.Print($"[TmxAtlasCompiler] Generated {compositesGenerated} composite tiles");

        // Trim atlas to actual used height
        var finalHeight = compositeCurrentY + (compositeCurrentX > 0 ? compositeRowHeight : 0);
        finalHeight = NextPowerOf2(Math.Max(finalHeight, packResult.atlasSize.Y));
        if (finalHeight < atlasImage.GetHeight())
        {
            var trimmedAtlas = Image.CreateEmpty(atlasImage.GetWidth(), finalHeight, false, Image.Format.Rgba8);
            trimmedAtlas.BlitRect(atlasImage, new Rect2I(0, 0, atlasImage.GetWidth(), finalHeight), Vector2I.Zero);
            atlasImage = trimmedAtlas;
        }

        // Save atlas PNG
        var atlasPath = $"{CompiledAtlasDir}/{AtlasFileName}";
        var absoluteAtlasPath = ProjectSettings.GlobalizePath(atlasPath);
        var saveError = atlasImage.SavePng(absoluteAtlasPath);
        if (saveError != Error.Ok)
        {
            return (false, $"Failed to save atlas: {saveError}");
        }

        var finalAtlasSize = new Vector2I(atlasImage.GetWidth(), atlasImage.GetHeight());
        GD.Print($"[TmxAtlasCompiler] Saved atlas {finalAtlasSize.X}x{finalAtlasSize.Y} to {atlasPath}");

        // Save atlas mapping JSON
        var mappingResult = SaveAtlasMapping(packResult.mapping, finalAtlasSize, targetTileSize, atlasPath);
        if (!mappingResult.success)
        {
            return (false, mappingResult.message);
        }

        // Save transition map
        var transitionResult = SaveTransitionMap(transitionMap);
        if (!transitionResult.success)
        {
            return (false, transitionResult.message);
        }

        var totalTiles = tilesToPack.Count + compositesGenerated;
        return (
            true,
            $"Compiled {totalTiles} tiles ({compositesGenerated} composites) " +
            $"from {tsxFiles.Length} TSX file(s) to {atlasPath}");
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

            // Parse tile elements - only tiles with explicit <tile> elements are defined tiles
            // Track which tiles have class/type attributes (explicitly defined in Tiled)
            var tilePropsMap = new Dictionary<int, Dictionary<string, string>>();
            var tileClassMap = new Dictionary<int, string>(); // tileId -> class/type attribute value
            foreach (var tileElement in tileset.Elements("tile"))
            {
                var tileId = int.Parse(tileElement.Attribute("id")?.Value ?? "-1");
                if (tileId >= 0)
                {
                    var props = ParseProperties(tileElement.Element("properties"));
                    tilePropsMap[tileId] = props;

                    // Check if tile has class or type attribute (Tiled's way of marking defined tiles)
                    // Use class/type as the tile's ID for consistency with TiledTilesetLoader
                    var tileClass = tileElement.Attribute("class")?.Value ?? tileElement.Attribute("type")?.Value;
                    if (!string.IsNullOrEmpty(tileClass))
                    {
                        tileClassMap[tileId] = tileClass;
                    }
                }
            }

            // Also collect tile IDs from wang sets - these need to be packed even without class attr
            var wangTileIds = new HashSet<int>();
            var wangSetsElement = tileset.Element("wangsets");
            if (wangSetsElement != null)
            {
                foreach (var wangSetElement in wangSetsElement.Elements("wangset"))
                {
                    foreach (var wangTile in wangSetElement.Elements("wangtile"))
                    {
                        var tileId = int.Parse(wangTile.Attribute("tileid")?.Value ?? "-1");
                        if (tileId >= 0)
                        {
                            wangTileIds.Add(tileId);
                        }
                    }
                }
            }

            // Create tile data for:
            // 1. Tiles with class/type attribute (explicitly defined game tiles)
            // 2. Tiles referenced by wang sets (needed for packing/compositing)
            var tilesToInclude = new HashSet<int>(tileClassMap.Keys);
            tilesToInclude.UnionWith(wangTileIds);

            foreach (var tileId in tilesToInclude)
            {
                var atlasX = tileId % columns;
                var atlasY = tileId / columns;

                tilePropsMap.TryGetValue(tileId, out var props);
                props ??= new Dictionary<string, string>();

                var hasExplicitId = props.ContainsKey("id");
                var isWangTile = wangTileIds.Contains(tileId);
                var hasClassAttr = tileClassMap.ContainsKey(tileId);

                // Determine tile ID: prefer class/type attribute, then "id" property, then generated name
                string tileIdStr;
                if (tileClassMap.TryGetValue(tileId, out var tileClass))
                    tileIdStr = tileClass; // Use class/type attribute directly (no snake_case conversion)
                else if (props.TryGetValue("id", out var propId))
                    tileIdStr = propId;
                else
                    tileIdStr = $"tile_{tileId}";

                var tileData = new TsxTileData
                {
                    TileId = tileId,
                    TsxPath = tsxPath,
                    AtlasX = atlasX,
                    AtlasY = atlasY,
                    SourceTileWidth = tileWidth,
                    SourceTileHeight = tileHeight,
                    SourceScale = sourceScale,
                    SourceImage = image,
                    Properties = props,
                    Id = tileIdStr,
                    Layer = isWangTile && !hasClassAttr ? "wang" : GetString(props, "layer", "terrain"),
                    Dominance = GetInt(props, "dominance", 0),
                    HasExplicitId = hasExplicitId,
                    HasClassAttribute = hasClassAttr
                };

                tiles.Add(tileData);
            }

            // Parse wang sets (reuse wangSetsElement from above)
            if (wangSetsElement != null)
            {
                foreach (var wangSetElement in wangSetsElement.Elements("wangset"))
                {
                    // Use 'class' attribute as the ID (like TiledTilesetLoader), fall back to 'name'
                    var name = wangSetElement.Attribute("class")?.Value
                               ?? wangSetElement.Attribute("name")?.Value
                               ?? "unknown";
                    var type = wangSetElement.Attribute("type")?.Value ?? "corner";
                    var props = ParseProperties(wangSetElement.Element("properties"));

                    var wangTiles = new Dictionary<int, List<int>>(); // bitmask -> list of tileIds (variants)
                    var allMemberTileIds = new HashSet<int>(); // ALL tiles in wang set
                    foreach (var wangTile in wangSetElement.Elements("wangtile"))
                    {
                        var tileId = int.Parse(wangTile.Attribute("tileid")?.Value ?? "-1");
                        var wangIdStr = wangTile.Attribute("wangid")?.Value ?? "";

                        if (tileId < 0) continue;

                        // Track ALL tiles as members (for exclusion from base terrains)
                        allMemberTileIds.Add(tileId);

                        // Parse wangid (format: "0,1,0,1,0,1,0,1")
                        var wangId = ParseWangId(wangIdStr, type);
                        if (wangId >= 0)
                        {
                            // For mixed wangsets, only use blob-compliant tiles for bitmask lookup
                            // (edges must match what corners imply)
                            if (type == "mixed" && !IsBlobCompliant(wangIdStr))
                                continue;

                            // Accumulate tiles with same bitmask as variants
                            if (!wangTiles.ContainsKey(wangId))
                                wangTiles[wangId] = new List<int>();
                            wangTiles[wangId].Add(tileId);
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
                        AllMemberTileIds = allMemberTileIds,
                        SourceImage = image,
                        SourceTileWidth = tileWidth,
                        SourceTileHeight = tileHeight,
                        IsTransparent = GetBool(props, "TransparentBackground", true),
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

        // Convert to our corner16 bitmask format (matches NeighborBitmaskCorner):
        // NE = 1, SE = 2, SW = 4, NW = 8
        // Where: TL=NW, TR=NE, BL=SW, BR=SE
        var bitmask = 0;
        if (tr > 0) bitmask |= 1;  // TR = NE = bit 0
        if (br > 0) bitmask |= 2;  // BR = SE = bit 1
        if (bl > 0) bitmask |= 4;  // BL = SW = bit 2
        if (tl > 0) bitmask |= 8;  // TL = NW = bit 3

        return bitmask;
    }

    /// <summary>
    /// Checks if a wangid has blob-compliant edges (edges match what corners imply).
    /// For dual-grid Corner16 tilesets, we only want tiles where edges are derived from corners.
    /// </summary>
    private bool IsBlobCompliant(string wangIdStr)
    {
        var parts = wangIdStr.Split(',');
        if (parts.Length != 8) return false;

        // Parse all values: 0=N, 1=NE/TR, 2=E, 3=SE/BR, 4=S, 5=SW/BL, 6=W, 7=NW/TL
        var n = int.Parse(parts[0]);
        var tr = int.Parse(parts[1]);  // NE
        var e = int.Parse(parts[2]);
        var br = int.Parse(parts[3]);  // SE
        var s = int.Parse(parts[4]);
        var bl = int.Parse(parts[5]);  // SW
        var w = int.Parse(parts[6]);
        var tl = int.Parse(parts[7]);  // NW

        // Blob constraint: edge is set if EITHER adjacent corner is set
        var blobN = (tl > 0 || tr > 0) ? 1 : 0;
        var blobE = (tr > 0 || br > 0) ? 1 : 0;
        var blobS = (br > 0 || bl > 0) ? 1 : 0;
        var blobW = (bl > 0 || tl > 0) ? 1 : 0;

        // Check if actual edges match blob-derived edges
        return n == blobN && e == blobE && s == blobS && w == blobW;
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
    /// Calculates the optimal atlas width for a given number of tiles.
    /// Aims for a near-square, power-of-2 texture up to MaxAtlasSize.
    /// </summary>
    /// <param name="totalTiles">Total number of tiles to pack</param>
    /// <param name="tileSize">Size of each tile in pixels</param>
    /// <returns>Optimal atlas width in pixels (power of 2)</returns>
    private int CalculateOptimalAtlasWidth(int totalTiles, int tileSize)
    {
        if (totalTiles <= 0)
            return tileSize; // Minimum 1 tile wide

        // Calculate the side length needed for a square atlas
        var tilesPerSide = (int)Math.Ceiling(Math.Sqrt(totalTiles));
        var idealWidth = tilesPerSide * tileSize;

        // Round up to next power of 2, capped at MaxAtlasSize
        return NextPowerOf2(idealWidth);
    }

    /// <summary>
    /// Packs tiles into an atlas using simple row-based bin packing.
    /// </summary>
    /// <param name="tiles">Tiles to pack</param>
    /// <param name="targetTileSize">Target tile size in pixels</param>
    /// <param name="targetWidth">Target atlas width in pixels (0 = auto-calculate)</param>
    private (bool success, string message, List<PackedTsxTile> packedTiles, Vector2I atlasSize,
        Dictionary<string, Dictionary<string, TileAtlasRect>> mapping)
        PackTiles(List<TsxTileData> tiles, int targetTileSize, int targetWidth = 0)
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

        // Use target width if provided, otherwise calculate based on tile count
        var atlasWidth = targetWidth > 0
            ? targetWidth
            : CalculateOptimalAtlasWidth(tilesToPack.Count, targetTileSize);

        // Simple row-based packing
        var currentX = 0;
        var currentY = 0;
        var rowHeight = targetTileSize;

        foreach (var tile in tilesToPack)
        {
            var tilePixelWidth = targetTileSize;
            var tilePixelHeight = targetTileSize;

            // Check if we need to start a new row
            if (currentX + tilePixelWidth > atlasWidth)
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
        }

        // Calculate final atlas size (use target width, round height to power of 2)
        var finalHeight = currentY + rowHeight;
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
    /// Checks if a value is a power of 2.
    /// </summary>
    private static bool IsPowerOfTwo(int value)
    {
        return value > 0 && (value & (value - 1)) == 0;
    }

    /// <summary>
    /// Selects the appropriate interpolation mode based on source dimensions.
    /// Uses nearest-neighbor for power-of-2 sizes (preserves pixel art crispness),
    /// bilinear for non-power-of-2 sizes.
    /// </summary>
    private static Image.Interpolation GetScalingInterpolation(int sourceWidth, int sourceHeight)
    {
        // Use nearest-neighbor for power-of-2 dimensions (pixel art friendly)
        if (IsPowerOfTwo(sourceWidth) && IsPowerOfTwo(sourceHeight))
            return Image.Interpolation.Nearest;

        // Use bilinear for non-power-of-2 (smoother scaling for non-standard sizes)
        return Image.Interpolation.Bilinear;
    }

    /// <summary>
    /// Packs a composite image into the atlas, expanding the atlas if needed.
    /// Returns the updated atlas and positions.
    /// </summary>
    private (Image atlas, int currentX, int currentY) PackCompositeToAtlas(
        Image atlasImage, Image compositeImage, int targetTileSize,
        int currentX, int currentY, int rowHeight, int atlasWidth)
    {
        // Check if we need to start a new row
        if (currentX + targetTileSize > atlasWidth)
        {
            currentX = 0;
            currentY += rowHeight;
        }

        // Expand atlas if needed
        if (currentY + targetTileSize > atlasImage.GetHeight())
        {
            var newHeight = Math.Min(atlasImage.GetHeight() * 2, MaxAtlasSize);
            var expandedAtlas = Image.CreateEmpty(atlasImage.GetWidth(), newHeight, false, Image.Format.Rgba8);
            expandedAtlas.Fill(new Color(0, 0, 0, 0));
            expandedAtlas.BlitRect(
                atlasImage,
                new Rect2I(
                    0,
                    0,
                    atlasImage.GetWidth(),
                    atlasImage.GetHeight()),
                Vector2I.Zero);
            atlasImage = expandedAtlas;
        }

        // Write composite to atlas
        atlasImage.BlitRect(compositeImage, new Rect2I(0, 0, targetTileSize, targetTileSize),
            new Vector2I(currentX, currentY));

        return (atlasImage, currentX, currentY);
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
                var extractedTile = Image.CreateEmpty(
                    tile.SourceTileWidth,
                    tile.SourceTileHeight,
                    false,
                    Image.Format.Rgba8);
                extractedTile.BlitRect(sourceImage, srcRect, Vector2I.Zero);

                // Resize if needed using appropriate interpolation
                if (tile.SourceTileWidth != targetTileSize || tile.SourceTileHeight != targetTileSize)
                {
                    var interpolation = GetScalingInterpolation(tile.SourceTileWidth, tile.SourceTileHeight);
                    extractedTile.Resize(targetTileSize, targetTileSize, interpolation);
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

    private string ToSnakeCase(string name)
    {
        // Simple conversion: Grass3 -> grass3
        return name.ToLowerInvariant().Replace(" ", "_");
    }

    /// <summary>
    /// Extracts a tile region from a source image with optional scaling.
    /// Uses nearest-neighbor for power-of-2 source sizes, bilinear otherwise.
    /// </summary>
    private static Image ExtractTileRegion(
        Image sourceImage,
        int atlasX,
        int atlasY,
        int sourceTileWidth,
        int sourceTileHeight,
        int targetTileSize)
    {
        // Source rectangle
        var srcRect = new Rect2I(
            atlasX * sourceTileWidth,
            atlasY * sourceTileHeight,
            sourceTileWidth,
            sourceTileHeight);

        // Extract tile region
        var extracted = Image.CreateEmpty(sourceTileWidth, sourceTileHeight, false, Image.Format.Rgba8);
        extracted.BlitRect(sourceImage, srcRect, Vector2I.Zero);

        // Scale if needed using appropriate interpolation
        if (sourceTileWidth != targetTileSize || sourceTileHeight != targetTileSize)
        {
            var interpolation = GetScalingInterpolation(sourceTileWidth, sourceTileHeight);
            extracted.Resize(targetTileSize, targetTileSize, interpolation);
        }

        return extracted;
    }

    /// <summary>
    /// Composites a transparent border tile onto a base terrain tile using alpha blending.
    /// Creates a new image with the border rendered on top of the base.
    /// </summary>
    private static Image CompositeImages(Image baseImage, Image borderImage)
    {
        var width = baseImage.GetWidth();
        var height = baseImage.GetHeight();

        // Ensure border matches base dimensions
        if (borderImage.GetWidth() != width || borderImage.GetHeight() != height)
        {
            var resizedBorder = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
            resizedBorder.BlitRect(borderImage,
                new Rect2I(0, 0, borderImage.GetWidth(), borderImage.GetHeight()),
                Vector2I.Zero);
            borderImage = resizedBorder;
        }

        // Create result image starting with base
        var result = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        result.BlitRect(baseImage, new Rect2I(0, 0, width, height), Vector2I.Zero);

        // Alpha-blend border on top pixel by pixel
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var baseColor = result.GetPixel(x, y);
                var borderColor = borderImage.GetPixel(x, y);

                // Standard alpha blending: result = border * alpha + base * (1 - alpha)
                var alpha = borderColor.A;
                if (alpha > 0)
                {
                    var blended = new Color(
                        borderColor.R * alpha + baseColor.R * (1 - alpha),
                        borderColor.G * alpha + baseColor.G * (1 - alpha),
                        borderColor.B * alpha + baseColor.B * (1 - alpha),
                        Math.Max(baseColor.A, alpha)
                    );
                    result.SetPixel(x, y, blended);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Generates a missing diagonal tile (bitmask 5 or 10) by compositing two corner tiles.
    /// Only generates if the diagonal bitmask is not already present in the wang set.
    /// Compositing order: base terrain → north corner → south corner (north rendered below south).
    /// </summary>
    /// <param name="wangSet">The wang set containing corner tiles</param>
    /// <param name="baseTerrain">The base terrain tile to composite onto</param>
    /// <param name="targetBitmask">The diagonal bitmask to generate (5 or 10)</param>
    /// <param name="northCornerBitmask">The north corner bitmask (1 for NE, 8 for NW)</param>
    /// <param name="southCornerBitmask">The south corner bitmask (4 for SW, 2 for SE)</param>
    /// <param name="targetTileSize">Target tile size in pixels</param>
    /// <param name="atlasImage">Atlas image (may be expanded)</param>
    /// <param name="compositeCurrentX">Current X position for next composite</param>
    /// <param name="compositeCurrentY">Current Y position for next composite</param>
    /// <param name="compositeRowHeight">Height of composite row</param>
    /// <param name="compositeAtlasWidth">Width of composite atlas area</param>
    /// <param name="variantCoords">Array of variant coordinates indexed by bitmask</param>
    /// <param name="compositesGenerated">Counter for generated composites</param>
    private void GenerateDiagonalIfMissing(
        TsxWangSetData wangSet,
        TsxTileData baseTerrain,
        int targetBitmask,
        int northCornerBitmask,
        int southCornerBitmask,
        int targetTileSize,
        ref Image atlasImage,
        ref int compositeCurrentX,
        ref int compositeCurrentY,
        int compositeRowHeight,
        int compositeAtlasWidth,
        List<Vector2I>[] variantCoords,
        ref int compositesGenerated)
    {
        // Skip if the diagonal bitmask already exists in the source data
        if (wangSet.WangTiles.TryGetValue(targetBitmask, out var existingTiles) && existingTiles.Count > 0)
            return;

        // Skip if we don't have the required corner tiles
        if (!wangSet.WangTiles.TryGetValue(northCornerBitmask, out var northTiles) || northTiles.Count == 0)
            return;

        if (!wangSet.WangTiles.TryGetValue(southCornerBitmask, out var southTiles) || southTiles.Count == 0)
            return;

        // Clear any fallback entries that were added in the main loop's else branch
        // (when no border tile existed for this bitmask, base terrain was used as fallback)
        // We want the generated diagonal to be the PRIMARY variant, not a fallback
        variantCoords[targetBitmask].Clear();

        // Extract base terrain image
        var baseImage = ExtractTileRegion(
            baseTerrain.SourceImage,
            baseTerrain.AtlasX,
            baseTerrain.AtlasY,
            baseTerrain.SourceTileWidth,
            baseTerrain.SourceTileHeight,
            targetTileSize);

        // Extract north corner tile (NE or NW - rendered first, below south)
        var northImage = ExtractTileRegion(
            wangSet.SourceImage,
            northTiles[0] % wangSet.Columns,
            northTiles[0] / wangSet.Columns,
            wangSet.SourceTileWidth,
            wangSet.SourceTileHeight,
            targetTileSize);

        // Extract south corner tile (SW or SE - rendered last, on top)
        var southImage = ExtractTileRegion(
            wangSet.SourceImage,
            southTiles[0] % wangSet.Columns,
            southTiles[0] / wangSet.Columns,
            wangSet.SourceTileWidth,
            wangSet.SourceTileHeight,
            targetTileSize);

        // Composite: base → north corner → south corner
        var intermediate = CompositeImages(baseImage, northImage);
        var compositeImage = CompositeImages(intermediate, southImage);

        // Pack into atlas
        (atlasImage, compositeCurrentX, compositeCurrentY) = PackCompositeToAtlas(
            atlasImage, compositeImage, targetTileSize,
            compositeCurrentX, compositeCurrentY, compositeRowHeight, compositeAtlasWidth);

        // Track variant position (in tile units)
        variantCoords[targetBitmask].Add(
            new Vector2I(
                compositeCurrentX / targetTileSize,
                compositeCurrentY / targetTileSize));

        compositeCurrentX += targetTileSize;
        compositesGenerated++;

        GD.Print(
            $"[TmxAtlasCompiler] Generated diagonal bitmask {targetBitmask} " +
            $"for {wangSet.Name} on {baseTerrain.Id}");
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
            // Convert paths to source IDs
            // Use "1" as the source ID to match firstgid in TMX files (tileset.tmx uses firstgid=1)
            // This ensures TranslateCoordinates can find tiles loaded via LoadFromTmx
            var sourcesMapping = new Dictionary<string, Dictionary<string, TileAtlasRect>>();
            foreach (var (path, coords) in mapping)
            {
                // All TSX tiles get sourceId=1 when loaded via TMX (from firstgid)
                const string sourceId = "1";
                if (!sourcesMapping.ContainsKey(sourceId))
                    sourcesMapping[sourceId] = new Dictionary<string, TileAtlasRect>();

                foreach (var (coordKey, rect) in coords)
                    sourcesMapping[sourceId][coordKey] = rect;
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
        public bool HasExplicitId { get; set; }
        public bool HasClassAttribute { get; set; } // True if tile has class/type attr in TSX
    }

    private class TsxWangSetData
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "corner";
        public string TsxPath { get; set; } = "";
        public int Columns { get; set; }
        public float SourceScale { get; set; }
        public Dictionary<string, string> Properties { get; set; } = new();
        public Dictionary<int, List<int>> WangTiles { get; set; } = new(); // bitmask -> list of tileIds (variants)
        // ALL tiles in wang set (for exclusion from base terrains)
        public HashSet<int> AllMemberTileIds { get; set; } = new();
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
        [JsonPropertyName("sources")]
        public Dictionary<string, Dictionary<string, TileAtlasRect>> Sources { get; set; } = new();
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
