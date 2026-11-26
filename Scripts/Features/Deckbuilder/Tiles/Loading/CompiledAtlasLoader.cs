using System;
using CardCleaner.Scripts.Core.Services;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles.Loading.CompiledAtlas;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Tiles.Loading;

/// <summary>
/// Loads compiled tile atlas and mapping at runtime.
/// Builds a TileSet from the compiled atlas PNG for efficient rendering.
/// </summary>
public static class CompiledAtlasLoader
{
    private const string DefaultMappingPath = "res://Data/CompiledAtlas/atlas_mapping.json";

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

    // One TileSet per mapping file, built on its first load, so a TileSet built for another mapping (a test's) never
    // replaces the game's. Its transition tiles are created as cells show them (CompiledAtlasTiles), so a replaced
    // TileSet would lose the ones already created. A failed build caches nothing.
    private static readonly Dictionary<string, CompiledAtlasTiles> CachedTileSets = new();

    static CompiledAtlasLoader()
    {
        AssemblyUnloadCleanup.Register(ClearCache);
    }

    /// <summary>
    /// Checks if a compiled atlas is available.
    /// </summary>
    public static bool IsCompiledAtlasAvailable(string? mappingPath = null)
    {
        mappingPath ??= DefaultMappingPath;
        var absolutePath = ProjectSettings.GlobalizePath(mappingPath);
        var exists = File.Exists(absolutePath);
        ILog.Print($"[CompiledAtlasLoader] IsCompiledAtlasAvailable: path={absolutePath}, exists={exists}");
        return exists;
    }

    /// <summary>
    /// Whether the compiled atlas for the mapping can be built (the loader builds and caches it).
    /// </summary>
    public static bool CanBuildAtlas(string? mappingPath = null) => LoadCompiledTileSet(mappingPath) != null;

    /// <summary>
    /// Loads the atlas mapping from JSON (a <c>res://</c> path is resolved here).
    /// Returns null if file doesn't exist or is invalid.
    /// </summary>
    public static AtlasMapping.AtlasMappingData? LoadMapping(string? mappingPath = null)
    {
        mappingPath ??= DefaultMappingPath;
        return AtlasMapping.Load(ProjectSettings.GlobalizePath(mappingPath));
    }

    /// <summary>
    /// Creates a TileSet from the compiled atlas.
    /// Returns null if atlas isn't available.
    /// </summary>
    public static TileSet? LoadCompiledTileSet(string? mappingPath = null)
    {
        ILog.Print("[CompiledAtlasLoader] LoadCompiledTileSet called");

        var cacheKey = mappingPath ?? DefaultMappingPath;
        if (CachedTileSets.TryGetValue(cacheKey, out var cached))
        {
            ILog.Print("[CompiledAtlasLoader] Returning cached TileSet");
            return cached.TileSet;
        }

        var mapping = LoadMapping(mappingPath);
        if (mapping?.Atlas == null)
        {
            ILog.Print($"[CompiledAtlasLoader] FAILED: mapping={mapping != null}, atlas={mapping?.Atlas != null}");
            return null;
        }

        ILog.Print(
            $"[CompiledAtlasLoader] Atlas info: path={mapping.Atlas.Path}, " +
            $"size={mapping.Atlas.Width}x{mapping.Atlas.Height}, " +
            $"tileSize={mapping.Atlas.TileSize}");

        try
        {
            var atlasTexture = LoadAtlasTexture(mapping.Atlas.Path);
            if (atlasTexture == null)
            {
                ILog.Print(
                    $"[CompiledAtlasLoader] FAILED: All load methods failed for: " +
                    mapping.Atlas.Path);
                return null;
            }

            var tiles = CompiledAtlasTileSetBuilder.Build(mapping, atlasTexture, CreateJsonOptions());

            CachedTileSets[cacheKey] = tiles;
            LogTileSetDetails(tiles.TileSet);

            return tiles.TileSet;
        }
        catch (Exception ex)
        {
            ILog.Print($"[CompiledAtlasLoader] Error creating TileSet: {ex.Message}");
            return null;
        }
    }

    private static Texture2D? LoadAtlasTexture(string atlasPath)
    {
        var atlasTexture = TryLoadAtlasResource(atlasPath);
        return atlasTexture ?? LoadAtlasTextureFromFile(atlasPath);
    }

    private static Texture2D? TryLoadAtlasResource(string atlasPath)
    {
        var resourceExists = ResourceLoader.Exists(atlasPath);
        ILog.Print(
            $"[CompiledAtlasLoader] ResourceLoader.Exists({atlasPath})={resourceExists}");

        if (!resourceExists)
            return null;

        var atlasTexture = ResourceLoader.Load<Texture2D>(atlasPath);
        if (atlasTexture != null)
        {
            ILog.Print($"[CompiledAtlasLoader] Loaded atlas via ResourceLoader: {atlasPath}");
        }
        else
        {
            ILog.Print("[CompiledAtlasLoader] ResourceLoader.Load returned null");
        }

        return atlasTexture;
    }

    private static Texture2D? LoadAtlasTextureFromFile(string atlasPath)
    {
        var absolutePath = ProjectSettings.GlobalizePath(atlasPath);
        ILog.Print($"[CompiledAtlasLoader] Trying direct file read: {absolutePath}");

        if (!File.Exists(absolutePath))
        {
            ILog.Print(
                $"[CompiledAtlasLoader] FAILED: Atlas PNG not found at: {absolutePath}");
            return null;
        }

        var image = Image.LoadFromFile(absolutePath);
        if (image == null)
        {
            ILog.Print("[CompiledAtlasLoader] FAILED: Image.LoadFromFile returned null");
            return null;
        }

        ILog.Print($"[CompiledAtlasLoader] Loaded image: {image.GetWidth()}x{image.GetHeight()}");
        var atlasTexture = ImageTexture.CreateFromImage(image);
        ILog.Print($"[CompiledAtlasLoader] Loaded atlas via direct file read: {atlasPath}");
        return atlasTexture;
    }

    private static void LogTileSetDetails(TileSet tileSet)
    {
        var sourceCount = tileSet.GetSourceCount();
        var source0 = tileSet.GetSource(0);
        var atlasSourceCheck = source0 as TileSetAtlasSource;
        var textureDims = atlasSourceCheck?.Texture?.GetSize() ?? Vector2.Zero;
        ILog.Print(
            $"[CompiledAtlasLoader] Created TileSet: {sourceCount} source(s), " +
            $"source 0 = {(source0 != null ? "present" : "MISSING")}, " +
            $"texture = {textureDims.X}x{textureDims.Y}");
    }

    /// <summary>
    /// The compiled atlas whose tile set this is, or null for any other tile set (the original tileset, a test's).
    /// </summary>
    internal static CompiledAtlasTiles? TilesFor(TileSet tileSet)
    {
        var id = tileSet.GetInstanceId();
        foreach (var tiles in CachedTileSets.Values)
        {
            if (tiles.TileSetId == id)
                return tiles;
        }

        return null;
    }

    // Clears every cached tileset on assembly unload (see AssemblyUnloadCleanup) and only then. A TileSet still on a
    // layer after a clear is no longer found by TilesFor, so its deferred transition tiles would never be created and
    // the cells that paint one would show nothing.
    private static void ClearCache() => CachedTileSets.Clear();
}
