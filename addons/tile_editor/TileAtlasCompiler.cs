#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Compiles tiles from a TileSet into a single atlas texture with coordinate mapping.
/// The compiled atlas and mapping file are used at runtime instead of loading
/// hundreds of individual source textures.
/// </summary>
public class TileAtlasCompiler
{
    private const int MaxAtlasSize = 16384;
    private const int TilePadding = 0; // No padding - tiles are packed tightly
    private const string CompiledAtlasDir = "res://Data/CompiledAtlas";
    private const string AtlasFileName = "terrain_atlas.png";
    private const string MappingFileName = "atlas_mapping.json";

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Compiles all tiles from the TileEditorService into a single atlas.
    /// For compositable auto-tiles (OuterTerrainId="*"), generates N×M composite variants
    /// by compositing each border onto each base terrain.
    /// Returns (success, message).
    /// </summary>
    public (bool success, string message) CompileAtlas(TileEditorService service)
    {
        if (service.TileSet == null)
            return (false, "No TileSet loaded");

        // Validate transition references before compilation
        var validationWarnings = ValidateTransitionReferences(service);
        foreach (var warning in validationWarnings)
        {
            GD.PrintErr($"[TileAtlasCompiler] {warning}");
        }

        var tileSet = service.TileSet;
        var tileSize = tileSet.TileSize;

        // Create output directory
        var absoluteDir = ProjectSettings.GlobalizePath(CompiledAtlasDir);
        if (!Directory.Exists(absoluteDir))
            Directory.CreateDirectory(absoluteDir);

        // Identify base terrain tiles (simple tiles that can serve as backgrounds)
        var simpleBaseTerrains = service.AllTiles
            .Where(t => !t.HasAutoTileVariants && t.Layer == "terrain")
            .ToList();

        // Identify compositable auto-tiles and fixed auto-tiles
        var compositableAutoTiles = service.AllTiles
            .Where(t => t.HasAutoTileVariants && t.IsCompositable)
            .ToList();
        var fixedAutoTiles = service.AllTiles
            .Where(t => t.HasAutoTileVariants && !t.IsCompositable)
            .ToList();

        // Auto-tiles that use themselves as inner terrain (InnerTerrainId is null)
        // can also serve as base terrains using their variant 15 (solid fill)
        var autoTileSolidFills = compositableAutoTiles
            .Where(t => string.IsNullOrEmpty(t.InnerTerrainId))
            .ToList();

        // Combined list of all tiles that can serve as base terrains
        var baseTerrains = simpleBaseTerrains.Concat(autoTileSolidFills).ToList();

        GD.Print($"[TileAtlasCompiler] Found {simpleBaseTerrains.Count} simple base terrains, " +
                 $"{autoTileSolidFills.Count} auto-tile solid fills, " +
                 $"{compositableAutoTiles.Count} compositable auto-tiles, " +
                 $"{fixedAutoTiles.Count} fixed auto-tiles");

        // Collect all unique tile regions we need to pack (non-composite tiles)
        var tilesToPack = CollectTilesToPack(service, tileSet, tileSize);
        if (tilesToPack.Count == 0)
            return (false, "No tiles to compile");

        GD.Print($"[TileAtlasCompiler] Packing {tilesToPack.Count} base tile regions");

        // Pack tiles into atlas(es) using simple row-based packing
        var packResult = PackTiles(tilesToPack, tileSize, MaxAtlasSize);
        if (!packResult.success)
            return (false, packResult.message);

        // Generate atlas image with base tiles
        var atlasPath = $"{CompiledAtlasDir}/{AtlasFileName}";
        var atlasImage = CreateAtlasImage(packResult.atlas, packResult.atlasSize, tileSet, tileSize);
        if (atlasImage == null)
            return (false, "Failed to create atlas image");

        // Initialize transition map
        var transitionMap = new CompiledTransitionMap();

        // Track current atlas position for composite tiles
        var compositeCurrentX = 0;
        var compositeCurrentY = packResult.atlasSize.Y; // Start below packed tiles
        var compositeRowHeight = tileSize.Y;
        var compositeAtlasWidth = packResult.atlasSize.X;
        var maxAtlasHeight = MaxAtlasSize;

        // Generate composite tiles for compositable auto-tiles
        var compositesGenerated = 0;
        foreach (var borderTile in compositableAutoTiles)
        {
            var borderSource = tileSet.GetSource(borderTile.SourceId) as TileSetAtlasSource;
            if (borderSource?.Texture == null)
            {
                GD.PrintErr($"[TileAtlasCompiler] Missing source {borderTile.SourceId} for {borderTile.Id}");
                continue;
            }

            var borderSourceImage = borderSource.Texture.GetImage();
            var variantCount = borderTile.ExpectedVariantCount;
            var format = ParseAutoTileFormat(borderTile.AutoTileFormat);

            foreach (var baseTerrain in baseTerrains)
            {
                // Skip self-transition (auto-tile compositing onto its own solid fill)
                if (baseTerrain.Id == borderTile.Id)
                    continue;

                var baseSource = tileSet.GetSource(baseTerrain.SourceId) as TileSetAtlasSource;
                if (baseSource?.Texture == null)
                    continue;

                var baseSourceImage = baseSource.Texture.GetImage();

                // For auto-tiles used as base, use variant 15 (solid fill) instead of base coords
                int baseAtlasX, baseAtlasY;
                if (baseTerrain.HasAutoTileVariants &&
                    baseTerrain.AutoTileVariants != null &&
                    baseTerrain.AutoTileVariants.Length > 15 &&
                    baseTerrain.AutoTileVariants[15].HasValue)
                {
                    var solidFillCoords = baseTerrain.AutoTileVariants[15]!.Value;
                    baseAtlasX = solidFillCoords.X;
                    baseAtlasY = solidFillCoords.Y;
                }
                else
                {
                    baseAtlasX = baseTerrain.AtlasX;
                    baseAtlasY = baseTerrain.AtlasY;
                }

                // Extract base terrain image
                var baseImage = ExtractTileRegion(baseSourceImage, baseAtlasX, baseAtlasY,
                    tileSize.X, baseTerrain.SourceScale);

                // Generate composites for all variants
                var variantCoords = new Vector2I[variantCount];
                for (var i = 0; i < variantCount; i++)
                {
                    // Get border variant coordinates
                    Vector2I borderCoords;
                    if (borderTile.AutoTileVariants != null && i < borderTile.AutoTileVariants.Length &&
                        borderTile.AutoTileVariants[i].HasValue)
                    {
                        borderCoords = borderTile.AutoTileVariants[i]!.Value;
                    }
                    else
                    {
                        borderCoords = new Vector2I(borderTile.AtlasX, borderTile.AtlasY);
                    }

                    // Extract border variant image
                    var borderImage = ExtractTileRegion(borderSourceImage, borderCoords.X, borderCoords.Y,
                        tileSize.X, borderTile.SourceScale);

                    // Composite border onto base
                    var compositeImage = CompositeImages(baseImage, borderImage);

                    // Check if we need to expand atlas height
                    if (compositeCurrentX + tileSize.X > compositeAtlasWidth)
                    {
                        compositeCurrentX = 0;
                        compositeCurrentY += compositeRowHeight;
                    }

                    if (compositeCurrentY + tileSize.Y > maxAtlasHeight)
                    {
                        GD.PrintErr($"[TileAtlasCompiler] Atlas size exceeded during composite generation");
                        break;
                    }

                    // Expand atlas if needed
                    if (compositeCurrentY + tileSize.Y > atlasImage.GetHeight())
                    {
                        var newHeight = Math.Min(atlasImage.GetHeight() * 2, maxAtlasHeight);
                        var expandedAtlas = Image.CreateEmpty(atlasImage.GetWidth(), newHeight, false, Image.Format.Rgba8);
                        expandedAtlas.Fill(new Color(0, 0, 0, 0));
                        expandedAtlas.BlitRect(atlasImage, new Rect2I(0, 0, atlasImage.GetWidth(), atlasImage.GetHeight()), Vector2I.Zero);
                        atlasImage = expandedAtlas;
                    }

                    // Write composite to atlas
                    atlasImage.BlitRect(compositeImage, new Rect2I(0, 0, tileSize.X, tileSize.Y),
                        new Vector2I(compositeCurrentX, compositeCurrentY));

                    // Track variant position (in tile units)
                    variantCoords[i] = new Vector2I(compositeCurrentX / tileSize.X, compositeCurrentY / tileSize.Y);

                    compositeCurrentX += tileSize.X;
                    compositesGenerated++;
                }

                // Add to transition map
                transitionMap.AddTransition(borderTile.Id, baseTerrain.Id, format, variantCoords);
            }
        }

        // Add fixed auto-tiles to transition map (they reference their specific outer terrain)
        foreach (var fixedTile in fixedAutoTiles)
        {
            if (string.IsNullOrEmpty(fixedTile.OuterTerrainId))
                continue; // No outer terrain specified, skip

            var format = ParseAutoTileFormat(fixedTile.AutoTileFormat);
            var variantCount = fixedTile.ExpectedVariantCount;
            var variantCoords = new Vector2I[variantCount];

            for (var i = 0; i < variantCount; i++)
            {
                if (fixedTile.AutoTileVariants != null && i < fixedTile.AutoTileVariants.Length &&
                    fixedTile.AutoTileVariants[i].HasValue)
                {
                    // Look up the compiled atlas position for this variant
                    var origCoords = fixedTile.AutoTileVariants[i]!.Value;
                    var sourceKey = fixedTile.SourceId.ToString();
                    var coordKey = $"{origCoords.X},{origCoords.Y}";

                    if (packResult.mapping.TryGetValue(sourceKey, out var sourceMapping) &&
                        sourceMapping.TryGetValue(coordKey, out var rect))
                    {
                        variantCoords[i] = new Vector2I(rect.X, rect.Y);
                    }
                    else
                    {
                        variantCoords[i] = new Vector2I(fixedTile.AtlasX, fixedTile.AtlasY);
                    }
                }
                else
                {
                    variantCoords[i] = new Vector2I(fixedTile.AtlasX, fixedTile.AtlasY);
                }
            }

            transitionMap.AddTransition(fixedTile.Id, fixedTile.OuterTerrainId, format, variantCoords);
        }

        GD.Print($"[TileAtlasCompiler] Generated {compositesGenerated} composite tiles");

        // Trim atlas to actual used height
        var finalHeight = compositeCurrentY + (compositeCurrentX > 0 ? compositeRowHeight : 0);
        finalHeight = NextPowerOf2(Math.Max(finalHeight, packResult.atlasSize.Y));
        if (finalHeight < atlasImage.GetHeight())
        {
            var trimmedAtlas = Image.CreateEmpty(atlasImage.GetWidth(), finalHeight, false, Image.Format.Rgba8);
            trimmedAtlas.BlitRect(atlasImage, new Rect2I(0, 0, atlasImage.GetWidth(), finalHeight), Vector2I.Zero);
            atlasImage = trimmedAtlas;
        }

        // Save atlas image
        var absolutePath = ProjectSettings.GlobalizePath(atlasPath);
        var error = atlasImage.SavePng(absolutePath);
        if (error != Error.Ok)
            return (false, $"Failed to save atlas: {error}");

        GD.Print($"[TileAtlasCompiler] Saved atlas {atlasImage.GetWidth()}x{atlasImage.GetHeight()} to {atlasPath}");

        // Save mapping file
        var mappingPath = $"{CompiledAtlasDir}/{MappingFileName}";
        var mappingResult = SaveAtlasMapping(packResult.mapping, new Vector2I(atlasImage.GetWidth(), atlasImage.GetHeight()),
            tileSize, atlasPath, mappingPath);
        if (!mappingResult.success)
            return (false, mappingResult.message);

        // Save transition map
        var transitionMapPath = $"{CompiledAtlasDir}/transition_map.json";
        var transitionResult = SaveTransitionMap(transitionMap, transitionMapPath);
        if (!transitionResult.success)
            return (false, transitionResult.message);

        var totalTiles = tilesToPack.Count + compositesGenerated;
        return (true, $"Compiled {totalTiles} tiles ({compositesGenerated} composites) to {atlasPath}");
    }

    /// <summary>
    /// Creates the atlas image by rendering all packed tiles.
    /// </summary>
    private Image? CreateAtlasImage(List<PackedTile> packedTiles, Vector2I atlasSize, TileSet tileSet, Vector2I tileSize)
    {
        try
        {
            var atlasImage = Image.CreateEmpty(atlasSize.X, atlasSize.Y, false, Image.Format.Rgba8);
            atlasImage.Fill(new Color(0, 0, 0, 0)); // Transparent background

            var paddedTileSize = tileSize.X + TilePadding * 2;

            foreach (var packed in packedTiles)
            {
                var source = tileSet.GetSource(packed.Region.SourceId) as TileSetAtlasSource;
                if (source?.Texture == null)
                {
                    GD.PrintErr($"[TileAtlasCompiler] Missing source {packed.Region.SourceId}");
                    continue;
                }

                var sourceImage = source.Texture.GetImage();
                if (sourceImage == null)
                {
                    GD.PrintErr($"[TileAtlasCompiler] Cannot get image from source {packed.Region.SourceId}");
                    continue;
                }

                var sourceScale = packed.Region.SourceScale;
                var sourcePixelSize = (int)(tileSize.X / sourceScale);

                var srcRect = new Rect2I(
                    packed.Region.AtlasX * sourcePixelSize,
                    packed.Region.AtlasY * sourcePixelSize,
                    packed.Region.Width * sourcePixelSize,
                    packed.Region.Height * sourcePixelSize);

                var targetWidth = packed.Region.Width * tileSize.X;
                var targetHeight = packed.Region.Height * tileSize.Y;
                var dstPos = new Vector2I(packed.AtlasX + TilePadding, packed.AtlasY + TilePadding);

                if (Math.Abs(sourceScale - 1.0f) > 0.001f)
                {
                    var extractedTile = Image.CreateEmpty(srcRect.Size.X, srcRect.Size.Y, false, Image.Format.Rgba8);
                    extractedTile.BlitRect(sourceImage, srcRect, Vector2I.Zero);
                    extractedTile.Resize(targetWidth, targetHeight, Image.Interpolation.Nearest);
                    atlasImage.BlitRect(extractedTile, new Rect2I(0, 0, targetWidth, targetHeight), dstPos);
                }
                else
                {
                    atlasImage.BlitRect(sourceImage, srcRect, dstPos);
                }

                ExtendEdgesToPadding(atlasImage, dstPos, new Vector2I(targetWidth, targetHeight), TilePadding);
            }

            return atlasImage;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileAtlasCompiler] Error creating atlas: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Saves the transition map to a JSON file.
    /// </summary>
    private (bool success, string message) SaveTransitionMap(CompiledTransitionMap transitionMap, string path)
    {
        try
        {
            var json = JsonSerializer.Serialize(transitionMap, JsonWriteOptions);
            var absolutePath = ProjectSettings.GlobalizePath(path);
            File.WriteAllText(absolutePath, json);

            GD.Print($"[TileAtlasCompiler] Saved transition map with {transitionMap.Transitions.Count} entries to {path}");
            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, $"Error saving transition map: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses auto-tile format string to enum.
    /// </summary>
    private static AutoTileFormat ParseAutoTileFormat(string? format)
    {
        return format?.ToLowerInvariant() switch
        {
            "blob47" => AutoTileFormat.Blob47,
            "edge16" => AutoTileFormat.Edge16,
            _ => AutoTileFormat.Corner16
        };
    }

    /// <summary>
    /// Validates that auto-tile transition references point to valid tiles.
    /// Returns a list of warning messages for invalid references.
    /// </summary>
    private List<string> ValidateTransitionReferences(TileEditorService service)
    {
        var warnings = new List<string>();
        var allTileIds = new HashSet<string>(service.AllTiles.Select(t => t.Id));
        var simpleTileIds = new HashSet<string>(
            service.AllTiles
                .Where(t => !t.HasAutoTileVariants) // Simple tiles don't have auto-tile variants
                .Select(t => t.Id));

        foreach (var tile in service.AllTiles)
        {
            // Only validate auto-tiles (tiles with auto-tile variants)
            if (!tile.HasAutoTileVariants)
                continue;

            // Validate InnerTerrainId if set
            if (!string.IsNullOrEmpty(tile.InnerTerrainId))
            {
                if (!allTileIds.Contains(tile.InnerTerrainId))
                {
                    warnings.Add($"Tile '{tile.Id}': InnerTerrainId '{tile.InnerTerrainId}' references non-existent tile");
                }
                else if (!simpleTileIds.Contains(tile.InnerTerrainId))
                {
                    // It's valid to reference any tile, but warn if it's another auto-tile
                    // as this could cause confusion
                    var referencedTile = service.AllTiles.FirstOrDefault(t => t.Id == tile.InnerTerrainId);
                    if (referencedTile?.HasAutoTileVariants == true)
                    {
                        warnings.Add($"Tile '{tile.Id}': InnerTerrainId '{tile.InnerTerrainId}' references another auto-tile (expected simple tile)");
                    }
                }
            }

            // Validate OuterTerrainId if set and not the special compositable marker
            if (!string.IsNullOrEmpty(tile.OuterTerrainId) && tile.OuterTerrainId != "*")
            {
                if (!allTileIds.Contains(tile.OuterTerrainId))
                {
                    warnings.Add($"Tile '{tile.Id}': OuterTerrainId '{tile.OuterTerrainId}' references non-existent tile");
                }
                else if (!simpleTileIds.Contains(tile.OuterTerrainId))
                {
                    var referencedTile = service.AllTiles.FirstOrDefault(t => t.Id == tile.OuterTerrainId);
                    if (referencedTile?.HasAutoTileVariants == true)
                    {
                        warnings.Add($"Tile '{tile.Id}': OuterTerrainId '{tile.OuterTerrainId}' references another auto-tile (expected simple tile)");
                    }
                }
            }

            // Log info about compositable tiles
            if (tile.IsCompositable)
            {
                GD.Print($"[TileAtlasCompiler] Compositable auto-tile: {tile.Id} (will generate N×M combinations)");
            }
        }

        return warnings;
    }

    /// <summary>
    /// Collects all tile regions that need to be packed, including variants.
    /// Excludes auto-tile variants for compositable tiles (they're replaced by composites).
    /// </summary>
    private List<TileRegion> CollectTilesToPack(TileEditorService service, TileSet tileSet, Vector2I tileSize)
    {
        var regions = new List<TileRegion>();
        var seenRegions = new HashSet<string>(); // Track unique (sourceId, x, y) combinations

        foreach (var tile in service.AllTiles)
        {
            var sourceScale = tile.SourceScale;

            // Skip the base tile coords for compositable auto-tiles
            // (they're transparent and only used as source for compositing)
            if (!tile.IsCompositable)
            {
                // Add base tile
                AddRegionIfNew(regions, seenRegions, tile.SourceId, tile.AtlasX, tile.AtlasY, tile.SizeX, tile.SizeY, sourceScale);
            }

            // Add auto-tile variants only for NON-compositable tiles
            // Compositable variants are replaced by generated composites
            if (tile.AutoTileVariants != null && !tile.IsCompositable)
            {
                foreach (var variant in tile.AutoTileVariants)
                {
                    if (variant.HasValue)
                        AddRegionIfNew(regions, seenRegions, tile.SourceId, variant.Value.X, variant.Value.Y, 1, 1, sourceScale);
                }
            }

            // Add visual variations (still include for all tiles)
            if (tile.Variations != null)
            {
                foreach (var variation in tile.Variations)
                {
                    AddRegionIfNew(regions, seenRegions, tile.SourceId, variation.X, variation.Y, tile.SizeX, tile.SizeY, sourceScale);
                }
            }

            // Add animation frames (still include for all tiles)
            if (tile.AnimationFrames != null)
            {
                foreach (var frame in tile.AnimationFrames)
                {
                    AddRegionIfNew(regions, seenRegions, tile.SourceId, frame.X, frame.Y, tile.SizeX, tile.SizeY, sourceScale);
                }
            }
        }

        return regions;
    }

    private void AddRegionIfNew(List<TileRegion> regions, HashSet<string> seen, int sourceId, int x, int y, int w, int h, float sourceScale)
    {
        var key = $"{sourceId}:{x},{y}";
        if (seen.Contains(key))
            return;

        seen.Add(key);
        regions.Add(new TileRegion(sourceId, x, y, w, h, sourceScale));
    }

    /// <summary>
    /// Packs tiles into an atlas using simple row-based bin packing.
    /// Returns atlas image, size, and coordinate mapping.
    /// </summary>
    private (bool success, string message, List<PackedTile> atlas, Vector2I atlasSize, Dictionary<string, AtlasTileMapping> mapping)
        PackTiles(List<TileRegion> tiles, Vector2I tileSize, int maxSize)
    {
        var atlas = new List<PackedTile>();
        var mapping = new Dictionary<string, AtlasTileMapping>();

        // Sort tiles by height descending for better packing
        var sorted = tiles.OrderByDescending(t => t.Height).ThenByDescending(t => t.Width).ToList();

        // Simple row-based packing
        var currentX = 0;
        var currentY = 0;
        var rowHeight = 0;
        var maxRowWidth = 0;
        var paddedTileSize = tileSize.X + TilePadding * 2;

        foreach (var tile in sorted)
        {
            var tilePixelWidth = tile.Width * paddedTileSize;
            var tilePixelHeight = tile.Height * paddedTileSize;

            // Check if we need to start a new row
            if (currentX + tilePixelWidth > maxSize)
            {
                currentX = 0;
                currentY += rowHeight;
                rowHeight = 0;
            }

            // Check if atlas is full
            if (currentY + tilePixelHeight > maxSize)
            {
                return (false, $"Atlas exceeds {maxSize}x{maxSize} - need multi-atlas support", atlas, Vector2I.Zero, mapping);
            }

            // Place tile
            var packedTile = new PackedTile(tile, currentX, currentY);
            atlas.Add(packedTile);

            // Create mapping entry: sourceId -> "x,y" -> atlas position
            var sourceKey = tile.SourceId.ToString();
            var coordKey = $"{tile.AtlasX},{tile.AtlasY}";

            if (!mapping.ContainsKey(sourceKey))
                mapping[sourceKey] = new AtlasTileMapping();

            // Store atlas position (in tile units, not pixels)
            var atlasTileX = (currentX + TilePadding) / tileSize.X;
            var atlasTileY = (currentY + TilePadding) / tileSize.Y;

            mapping[sourceKey][coordKey] = new TileAtlasRect
            {
                X = atlasTileX,
                Y = atlasTileY,
                W = tile.Width,
                H = tile.Height
            };

            // Update position trackers
            currentX += tilePixelWidth;
            rowHeight = Math.Max(rowHeight, tilePixelHeight);
            maxRowWidth = Math.Max(maxRowWidth, currentX);
        }

        // Calculate final atlas size (round up to power of 2)
        var finalHeight = currentY + rowHeight;
        var atlasWidth = NextPowerOf2(maxRowWidth);
        var atlasHeight = NextPowerOf2(finalHeight);

        return (true, "", atlas, new Vector2I(atlasWidth, atlasHeight), mapping);
    }

    private int NextPowerOf2(int value)
    {
        var power = 1;
        while (power < value)
            power *= 2;
        return Math.Min(power, MaxAtlasSize);
    }

    /// <summary>
    /// Creates and saves the atlas image by copying tile regions from source textures.
    /// </summary>
    private (bool success, string message) SaveAtlasImage(
        List<PackedTile> packedTiles,
        Vector2I atlasSize,
        TileSet tileSet,
        Vector2I tileSize,
        string atlasPath)
    {
        try
        {
            // Create atlas image
            var atlasImage = Image.CreateEmpty(atlasSize.X, atlasSize.Y, false, Image.Format.Rgba8);
            atlasImage.Fill(new Color(0, 0, 0, 0)); // Transparent background

            var paddedTileSize = tileSize.X + TilePadding * 2;

            foreach (var packed in packedTiles)
            {
                var source = tileSet.GetSource(packed.Region.SourceId) as TileSetAtlasSource;
                if (source?.Texture == null)
                {
                    GD.PrintErr($"[TileAtlasCompiler] Missing source {packed.Region.SourceId}");
                    continue;
                }

                // Get source image
                var sourceImage = source.Texture.GetImage();
                if (sourceImage == null)
                {
                    GD.PrintErr($"[TileAtlasCompiler] Cannot get image from source {packed.Region.SourceId}");
                    continue;
                }

                // Calculate source pixel size based on SourceScale
                // SourceScale 0.5 = 32px source (32/16=2, so divide tileSize by 0.5 = multiply by 2)
                // SourceScale 1.0 = 16px source (standard)
                // SourceScale 2.0 = 8px source (8/16=0.5, so divide tileSize by 2.0)
                var sourceScale = packed.Region.SourceScale;
                var sourcePixelSize = (int)(tileSize.X / sourceScale);

                // Calculate source rectangle (in source texture pixels)
                var srcRect = new Rect2I(
                    packed.Region.AtlasX * sourcePixelSize,
                    packed.Region.AtlasY * sourcePixelSize,
                    packed.Region.Width * sourcePixelSize,
                    packed.Region.Height * sourcePixelSize);

                // Target size in output atlas (always 16px base * cell count)
                var targetWidth = packed.Region.Width * tileSize.X;
                var targetHeight = packed.Region.Height * tileSize.Y;

                // Destination position (with padding offset)
                var dstPos = new Vector2I(packed.AtlasX + TilePadding, packed.AtlasY + TilePadding);

                // Handle scaling if needed
                if (Math.Abs(sourceScale - 1.0f) > 0.001f)
                {
                    // Extract tile region to temp image
                    var extractedTile = Image.CreateEmpty(srcRect.Size.X, srcRect.Size.Y, false, Image.Format.Rgba8);
                    extractedTile.BlitRect(sourceImage, srcRect, Vector2I.Zero);

                    // Resize to target output size using nearest neighbor for pixel art
                    extractedTile.Resize(targetWidth, targetHeight, Image.Interpolation.Nearest);

                    // Blit the resized tile to the atlas
                    atlasImage.BlitRect(extractedTile, new Rect2I(0, 0, targetWidth, targetHeight), dstPos);
                }
                else
                {
                    // No scaling needed - direct blit
                    atlasImage.BlitRect(sourceImage, srcRect, dstPos);
                }

                // Optional: Extend edges into padding to reduce bleeding
                ExtendEdgesToPadding(atlasImage, dstPos, new Vector2I(targetWidth, targetHeight), TilePadding);
            }

            // Save as PNG
            var absolutePath = ProjectSettings.GlobalizePath(atlasPath);
            var error = atlasImage.SavePng(absolutePath);
            if (error != Error.Ok)
                return (false, $"Failed to save atlas: {error}");

            GD.Print($"[TileAtlasCompiler] Saved atlas {atlasSize.X}x{atlasSize.Y} to {atlasPath}");
            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, $"Error creating atlas: {ex.Message}");
        }
    }

    /// <summary>
    /// Extends tile edges into padding area to reduce texture bleeding.
    /// </summary>
    private void ExtendEdgesToPadding(Image atlas, Vector2I tilePos, Vector2I tileSize, int padding)
    {
        // Extend top edge
        for (var x = tilePos.X; x < tilePos.X + tileSize.X; x++)
        {
            var edgeColor = atlas.GetPixel(x, tilePos.Y);
            for (var p = 1; p <= padding; p++)
            {
                if (tilePos.Y - p >= 0)
                    atlas.SetPixel(x, tilePos.Y - p, edgeColor);
            }
        }

        // Extend bottom edge
        for (var x = tilePos.X; x < tilePos.X + tileSize.X; x++)
        {
            var edgeColor = atlas.GetPixel(x, tilePos.Y + tileSize.Y - 1);
            for (var p = 1; p <= padding; p++)
            {
                if (tilePos.Y + tileSize.Y - 1 + p < atlas.GetHeight())
                    atlas.SetPixel(x, tilePos.Y + tileSize.Y - 1 + p, edgeColor);
            }
        }

        // Extend left edge
        for (var y = tilePos.Y; y < tilePos.Y + tileSize.Y; y++)
        {
            var edgeColor = atlas.GetPixel(tilePos.X, y);
            for (var p = 1; p <= padding; p++)
            {
                if (tilePos.X - p >= 0)
                    atlas.SetPixel(tilePos.X - p, y, edgeColor);
            }
        }

        // Extend right edge
        for (var y = tilePos.Y; y < tilePos.Y + tileSize.Y; y++)
        {
            var edgeColor = atlas.GetPixel(tilePos.X + tileSize.X - 1, y);
            for (var p = 1; p <= padding; p++)
            {
                if (tilePos.X + tileSize.X - 1 + p < atlas.GetWidth())
                    atlas.SetPixel(tilePos.X + tileSize.X - 1 + p, y, edgeColor);
            }
        }
    }

    /// <summary>
    /// Composites a transparent border tile onto a base terrain tile using alpha blending.
    /// Creates a new image with the border rendered on top of the base.
    /// </summary>
    /// <param name="baseImage">The opaque base terrain image</param>
    /// <param name="borderImage">The transparent border overlay image</param>
    /// <returns>A new image with the border composited onto the base</returns>
    private static Image CompositeImages(Image baseImage, Image borderImage)
    {
        var width = baseImage.GetWidth();
        var height = baseImage.GetHeight();

        // Ensure border matches base dimensions
        if (borderImage.GetWidth() != width || borderImage.GetHeight() != height)
        {
            // Resize border to match base (shouldn't normally happen, but handle gracefully)
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
                        Math.Max(baseColor.A, alpha) // Result is opaque if either input is opaque
                    );
                    result.SetPixel(x, y, blended);
                }
                // If alpha is 0, keep base color (already there)
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts a tile region from a source image with optional scaling.
    /// </summary>
    /// <param name="sourceImage">The source texture image</param>
    /// <param name="atlasX">Tile X coordinate in source</param>
    /// <param name="atlasY">Tile Y coordinate in source</param>
    /// <param name="tileSize">Target tile size in pixels</param>
    /// <param name="sourceScale">Source scale factor (0.5=32px source, 1.0=16px, 2.0=8px)</param>
    /// <returns>Extracted and scaled tile image</returns>
    private static Image ExtractTileRegion(Image sourceImage, int atlasX, int atlasY, int tileSize, float sourceScale)
    {
        // Calculate source pixel size based on scale
        var sourcePixelSize = (int)(tileSize / sourceScale);

        // Source rectangle
        var srcRect = new Rect2I(
            atlasX * sourcePixelSize,
            atlasY * sourcePixelSize,
            sourcePixelSize,
            sourcePixelSize);

        // Extract tile region
        var extracted = Image.CreateEmpty(sourcePixelSize, sourcePixelSize, false, Image.Format.Rgba8);
        extracted.BlitRect(sourceImage, srcRect, Vector2I.Zero);

        // Scale if needed
        if (Math.Abs(sourceScale - 1.0f) > 0.001f)
        {
            extracted.Resize(tileSize, tileSize, Image.Interpolation.Nearest);
        }

        return extracted;
    }

    /// <summary>
    /// Saves the atlas mapping JSON file.
    /// </summary>
    private (bool success, string message) SaveAtlasMapping(
        Dictionary<string, AtlasTileMapping> mapping,
        Vector2I atlasSize,
        Vector2I tileSize,
        string atlasPath,
        string mappingPath)
    {
        try
        {
            var data = new AtlasMappingData
            {
                Version = "1.0",
                Atlas = new AtlasInfo
                {
                    Path = atlasPath,
                    Width = atlasSize.X,
                    Height = atlasSize.Y,
                    TileSize = tileSize.X
                },
                Sources = mapping
            };

            var json = JsonSerializer.Serialize(data, JsonWriteOptions);
            var absolutePath = ProjectSettings.GlobalizePath(mappingPath);
            File.WriteAllText(absolutePath, json);

            GD.Print($"[TileAtlasCompiler] Saved mapping with {mapping.Count} sources to {mappingPath}");
            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, $"Error saving mapping: {ex.Message}");
        }
    }

    // Data structures for packing
    private record TileRegion(int SourceId, int AtlasX, int AtlasY, int Width, int Height, float SourceScale);
    private record PackedTile(TileRegion Region, int AtlasX, int AtlasY);

    // Type alias for mapping structure: sourceId -> coordKey -> rect
    private class AtlasTileMapping : Dictionary<string, TileAtlasRect> { }

    // JSON serialization classes
    private class AtlasMappingData
    {
        [JsonPropertyName("version")] public string Version { get; set; } = "1.0";
        [JsonPropertyName("atlas")] public AtlasInfo Atlas { get; set; } = new();
        [JsonPropertyName("sources")] public Dictionary<string, AtlasTileMapping> Sources { get; set; } = new();
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
