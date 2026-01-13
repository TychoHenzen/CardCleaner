using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services.TilesetLoading;
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
                var firstGid = ParseHelpers.ParseInt(tilesetRef.Attribute("firstgid")?.Value, 1);
                var source = tilesetRef.Attribute("source")?.Value;

                if (string.IsNullOrEmpty(source)) continue;

                // Resolve relative TSX path
                var tsxAbsolutePath = Path.GetFullPath(Path.Combine(tmxDir, source));

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

            var tileWidth = ParseHelpers.ParseInt(tileset.Attribute("tilewidth")?.Value, 16);
            var tileHeight = ParseHelpers.ParseInt(tileset.Attribute("tileheight")?.Value, 16);
            var columns = ParseHelpers.ParseInt(tileset.Attribute("columns")?.Value, 1);

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
            var wangData = WangSetParser.ParseWangSets(tileset);

            // Parse per-tile properties
            var tileProperties = TilePropertyParser.ParseAllTileProperties(tileset);

            // Build TileDefinitions
            var tiles = TileDefinitionBuilder.BuildTileDefinitions(tileProperties, wangData, columns, sourceId);

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

    /// <summary>
    /// Detects variation group info from a tile ID based on naming patterns.
    /// Numbered suffixes (grass1, grass2, grass3) = PerGeneration (one picked per map).
    /// Lettered suffixes (flower_a, flower_b) = PerInstance (randomly picked per placement).
    /// </summary>
    /// <param name="tileId">The tile ID to analyze.</param>
    /// <returns>Variation info or null if no pattern detected.</returns>
    public static VariationGroupInfo? DetectVariationPattern(string tileId)
        => VariationPatternDetector.DetectVariationPattern(tileId);

    /// <summary>
    /// Loads a TMX map file with full tile resolution support.
    /// </summary>
    public static TmxMapData? LoadTmxMap(string tmxPath)
        => TmxMapLoader.LoadTmxMap(tmxPath);
}
