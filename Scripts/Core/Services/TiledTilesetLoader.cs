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
    public static TileRegistryResult LoadFromTmx(IAssetPaths paths, string tmxPath)
    {
        var absolutePath = paths.Globalize(tmxPath);
        if (!File.Exists(absolutePath))
        {
            ILog.Print($"[TiledTilesetLoader] TMX file not found: {absolutePath}");
            return EmptyResult();
        }

        try
        {
            return ReadTmx(paths, absolutePath, tmxPath);
        }
        catch (Exception ex)
        {
            ILog.Print($"[TiledTilesetLoader] Error loading TMX: {ex.Message}");
            return EmptyResult();
        }
    }

    private static TileRegistryResult ReadTmx(IAssetPaths paths, string absolutePath, string tmxPath)
    {
        var doc = XDocument.Load(absolutePath);
        var map = doc.Root;
        if (map == null || map.Name != "map")
        {
            ILog.Print("[TiledTilesetLoader] Invalid TMX: missing map root element");
            return EmptyResult();
        }

        var tmxDir = Path.GetDirectoryName(absolutePath) ?? "";
        var allTiles = new List<TileDefinition>();
        var config = LoadReferencedTilesets(paths, map, tmxDir, allTiles);

        ILog.Print($"[TiledTilesetLoader] Loaded {allTiles.Count} tiles from TMX {tmxPath}");
        return new TileRegistryResult("", allTiles, config ?? TilesetConfig.Default);
    }

    /// <summary>
    /// Loads every tileset referenced by the map into <paramref name="allTiles"/> and returns the
    /// config of the first one.
    /// </summary>
    private static TilesetConfig? LoadReferencedTilesets(
        IAssetPaths paths, XElement map, string tmxDir, List<TileDefinition> allTiles)
    {
        TilesetConfig? config = null;

        foreach (var tilesetRef in map.Elements("tileset"))
        {
            var firstGid = ParseHelpers.ParseInt(tilesetRef.Attribute("firstgid")?.Value, 1);
            var source = tilesetRef.Attribute("source")?.Value;

            if (string.IsNullOrEmpty(source)) continue;

            // Resolve relative TSX path
            var tsxAbsolutePath = Path.GetFullPath(Path.Combine(tmxDir, source));

            ILog.Print($"[TiledTilesetLoader] Loading tileset from TMX: {source} (firstgid={firstGid})");

            var result = LoadFromTsx(paths, tsxAbsolutePath, firstGid);
            allTiles.AddRange(result.Tiles);

            // Use config from first tileset
            config ??= result.TilesetConfig;
        }

        return config;
    }

    /// <summary>
    /// Load tiles from a TSX tileset file.
    /// </summary>
    public static TileRegistryResult LoadFromTsx(IAssetPaths paths, string tsxPath, int sourceId = 0)
    {
        var absolutePath = paths.Globalize(tsxPath);
        if (!File.Exists(absolutePath))
        {
            ILog.Print($"[TiledTilesetLoader] TSX file not found: {absolutePath}");
            return EmptyResult();
        }

        try
        {
            return ReadTsx(absolutePath, tsxPath, sourceId);
        }
        catch (Exception ex)
        {
            ILog.Print($"[TiledTilesetLoader] Error loading TSX: {ex.Message}");
            return EmptyResult();
        }
    }

    private static TileRegistryResult ReadTsx(string absolutePath, string tsxPath, int sourceId)
    {
        var doc = XDocument.Load(absolutePath);
        var tileset = doc.Root;
        if (tileset == null || tileset.Name != "tileset")
        {
            ILog.Print("[TiledTilesetLoader] Invalid TSX: missing tileset root element");
            return EmptyResult();
        }

        var tileWidth = ParseHelpers.ParseInt(tileset.Attribute("tilewidth")?.Value, 16);
        var tileHeight = ParseHelpers.ParseInt(tileset.Attribute("tileheight")?.Value, 16);
        var columns = ParseHelpers.ParseInt(tileset.Attribute("columns")?.Value, 1);

        var imageSource = ResolveImageSource(tileset, absolutePath);

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

    /// <summary>
    /// Reads the image source and makes relative paths absolute from the TSX location
    /// (the caller can convert them back to project paths).
    /// </summary>
    private static string ResolveImageSource(XElement tileset, string tsxAbsolutePath)
    {
        var imageSource = tileset.Element("image")?.Attribute("source")?.Value ?? "";
        if (string.IsNullOrEmpty(imageSource) || imageSource.StartsWith("res://"))
            return imageSource;

        var tsxDir = Path.GetDirectoryName(tsxAbsolutePath) ?? "";
        return Path.GetFullPath(Path.Combine(tsxDir, imageSource));
    }

    private static TileRegistryResult EmptyResult() => new("", [], TilesetConfig.Default);

    /// <summary>
    /// Loads a TMX map file with full tile resolution support.
    /// </summary>
    internal static TmxMapData? LoadTmxMap(IAssetPaths paths, string tmxPath)
        => TmxMapLoader.LoadTmxMap(paths, tmxPath);
}
