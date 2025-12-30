#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Compiles tiles from a TileSet into a single atlas texture with coordinate mapping.
/// The compiled atlas and mapping file are used at runtime instead of loading
/// hundreds of individual source textures.
/// </summary>
public class TileAtlasCompiler
{
    private const int MaxAtlasSize = 4096;
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
    /// Returns (success, message).
    /// </summary>
    public (bool success, string message) CompileAtlas(TileEditorService service)
    {
        if (service.TileSet == null)
            return (false, "No TileSet loaded");

        var tileSet = service.TileSet;
        var tileSize = tileSet.TileSize;

        // Collect all unique tile regions we need to pack
        var tilesToPack = CollectTilesToPack(service, tileSet, tileSize);
        if (tilesToPack.Count == 0)
            return (false, "No tiles to compile");

        GD.Print($"[TileAtlasCompiler] Packing {tilesToPack.Count} tile regions");

        // Pack tiles into atlas(es) using simple row-based packing
        var packResult = PackTiles(tilesToPack, tileSize, MaxAtlasSize);
        if (!packResult.success)
            return (false, packResult.message);

        // Create output directory
        var absoluteDir = ProjectSettings.GlobalizePath(CompiledAtlasDir);
        if (!Directory.Exists(absoluteDir))
            Directory.CreateDirectory(absoluteDir);

        // Generate atlas image and save
        var atlasPath = $"{CompiledAtlasDir}/{AtlasFileName}";
        var saveResult = SaveAtlasImage(packResult.atlas, packResult.atlasSize, tileSet, tileSize, atlasPath);
        if (!saveResult.success)
            return (false, saveResult.message);

        // Generate and save mapping file
        var mappingPath = $"{CompiledAtlasDir}/{MappingFileName}";
        var mappingResult = SaveAtlasMapping(packResult.mapping, packResult.atlasSize, tileSize, atlasPath, mappingPath);
        if (!mappingResult.success)
            return (false, mappingResult.message);

        return (true, $"Compiled {tilesToPack.Count} tiles to {atlasPath}");
    }

    /// <summary>
    /// Collects all tile regions that need to be packed, including variants.
    /// </summary>
    private List<TileRegion> CollectTilesToPack(TileEditorService service, TileSet tileSet, Vector2I tileSize)
    {
        var regions = new List<TileRegion>();
        var seenRegions = new HashSet<string>(); // Track unique (sourceId, x, y) combinations

        foreach (var tile in service.AllTiles)
        {
            var sourceScale = tile.SourceScale;

            // Add base tile
            AddRegionIfNew(regions, seenRegions, tile.SourceId, tile.AtlasX, tile.AtlasY, tile.SizeX, tile.SizeY, sourceScale);

            // Add auto-tile variants (always 1x1 cell size, but inherit source scale)
            if (tile.AutoTileVariants != null)
            {
                foreach (var variant in tile.AutoTileVariants)
                {
                    if (variant.HasValue)
                        AddRegionIfNew(regions, seenRegions, tile.SourceId, variant.Value.X, variant.Value.Y, 1, 1, sourceScale);
                }
            }

            // Add visual variations
            if (tile.Variations != null)
            {
                foreach (var variation in tile.Variations)
                {
                    AddRegionIfNew(regions, seenRegions, tile.SourceId, variation.X, variation.Y, tile.SizeX, tile.SizeY, sourceScale);
                }
            }

            // Add animation frames
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
