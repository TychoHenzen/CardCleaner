using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Loads tile definitions from JSON data files or Tiled TSX files.
/// </summary>
public static partial class TileDataLoader
{
    private const string DefaultTilesPath = "res://Data/Tiles/tiles.json";
    private const string DefaultTiledPath = "res://Data/Tiled/tileset.tmx";
    private const string DefaultTilesetPath = "res://Assets/Terrain/TileSets/ByPack/FantasyDreamland.tres";

    /// <summary>
    /// Creates fresh JsonSerializerOptions per call to avoid assembly unload issues.
    /// See: https://github.com/godotengine/godot/issues/78513
    /// </summary>
    private static JsonSerializerOptions CreateJsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Load tiles from TSX/TMX file, JSON file, or auto-detect based on available files.
    /// Prefers Tiled format (TMX/TSX) over JSON when both are available.
    /// Priority: 1) Explicit path argument, 2) TMX/TSX if exists, 3) JSON fallback.
    /// </summary>
    public static TileRegistryResult LoadTileRegistry(string? path = null)
    {
        // Auto-detect: prefer Tiled format if available and no explicit path given
        if (path == null)
        {
            var tiledAbsolutePath = ProjectSettings.GlobalizePath(DefaultTiledPath);
            if (File.Exists(tiledAbsolutePath))
            {
                ILog.Print($"[TileDataLoader] TSX/TMX PRIMARY: Loading from {DefaultTiledPath}");
                return DefaultTiledPath.EndsWith(".tmx", StringComparison.OrdinalIgnoreCase)
                    ? LoadFromTmx(DefaultTiledPath)
                    : LoadFromTsx(DefaultTiledPath);
            }

            ILog.Print(
                $"[TileDataLoader] JSON FALLBACK: No Tiled file at {DefaultTiledPath}, " +
                $"using {DefaultTilesPath}");
            path = DefaultTilesPath;
        }

        // Dispatch based on file extension
        if (path.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase))
            return LoadFromTsx(path);
        if (path.EndsWith(".tmx", StringComparison.OrdinalIgnoreCase))
            return LoadFromTmx(path);

        return LoadFromJson(path);
    }

    /// <summary>
    /// Load tiles from Tiled TSX tileset file.
    /// </summary>
    public static TileRegistryResult LoadFromTsx(string tsxPath, int sourceId = 0)
    {
        return TiledTilesetLoader.LoadFromTsx(tsxPath, sourceId);
    }

    /// <summary>
    /// Load tiles from Tiled TMX map file (loads all referenced tilesets).
    /// </summary>
    public static TileRegistryResult LoadFromTmx(string tmxPath)
    {
        return TiledTilesetLoader.LoadFromTmx(tmxPath);
    }

    /// <summary>
    /// Load tiles and tileset path from JSON (original format).
    /// </summary>
    public static TileRegistryResult LoadFromJson(string? path = null)
    {
        path ??= DefaultTilesPath;

        var absolutePath = ProjectSettings.GlobalizePath(path);
        if (!File.Exists(absolutePath))
        {
            ILog.Print($"[TileDataLoader] File not found: {absolutePath}");
            return new TileRegistryResult(DefaultTilesetPath, [], TilesetConfig.Default);
        }

        try
        {
            var json = File.ReadAllText(absolutePath);
            var data = JsonSerializer.Deserialize<TileRegistryData>(json, CreateJsonOptions());
            if (data?.Tiles == null)
            {
                ILog.Print("[TileDataLoader] Invalid JSON structure");
                return new TileRegistryResult(DefaultTilesetPath, [], TilesetConfig.Default);
            }

            // Parse tileset config (with defaults if not present)
            var tilesetConfig = ParseTilesetConfig(data.TilesetConfig);

            // Register custom auto-tile formats before processing tiles
            RegisterCustomAutoTileFormats(data.AutoTileFormats);

            var tilesetPath = data.Tileset ?? DefaultTilesetPath;
            var tiles = new List<TileDefinition>();
            var index = 0;
            foreach (var tileData in data.Tiles)
            {
                var tile = ConvertToTileDefinition(tileData, index);
                if (tile != null)
                    tiles.Add(tile);
                index++;
            }

            ILog.Print($"[TileDataLoader] Loaded {tiles.Count} tiles from {path} using tileset {tilesetPath}");
            return new TileRegistryResult(tilesetPath, tiles, tilesetConfig);
        }
        catch (Exception ex)
        {
            ILog.Print($"[TileDataLoader] Error loading tiles: {ex.Message}");
            return new TileRegistryResult(DefaultTilesetPath, [], TilesetConfig.Default);
        }
    }

    /// <summary>
    /// Parse tileset configuration from JSON data, with defaults for missing values.
    /// </summary>
    private static TilesetConfig ParseTilesetConfig(TilesetConfigData? data)
    {
        if (data == null)
            return TilesetConfig.Default;

        return new TilesetConfig
        {
            BaseTileSize = data.BaseTileSize != null
                ? new Vector2I(data.BaseTileSize.X, data.BaseTileSize.Y)
                : new Vector2I(16, 16),
            GridOffset = data.GridOffset != null
                ? new Vector2((float)data.GridOffset.X, (float)data.GridOffset.Y)
                : Vector2.Zero
        };
    }

    /// <summary>
    /// Register custom auto-tile formats from JSON data.
    /// </summary>
    private static void RegisterCustomAutoTileFormats(List<AutoTileFormatData>? formats)
    {
        // Ensure built-in formats are registered first
        AutoTileFormatRegistry.EnsureBuiltInsRegistered();

        if (formats == null || formats.Count == 0)
            return;

        foreach (var formatData in formats)
        {
            if (string.IsNullOrWhiteSpace(formatData.Name))
            {
                ILog.Print("[TileDataLoader] Skipping custom format with missing name");
                continue;
            }

            // Don't allow overriding built-in formats
            if (AutoTileFormatRegistry.Contains(formatData.Name))
            {
                ILog.Print($"[TileDataLoader] Skipping custom format '{formatData.Name}' - name already registered");
                continue;
            }

            var format = ParseCustomAutoTileFormat(formatData);
            if (format != null)
            {
                AutoTileFormatRegistry.Register(format);
                ILog.Print($"[TileDataLoader] Registered custom auto-tile format: {formatData.Name}");
            }
        }
    }

}
