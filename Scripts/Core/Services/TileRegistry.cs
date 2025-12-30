using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

public class TileRegistry : ITileRegistry
{
    public const int DefaultSourceId = 4;
    private const string DefaultTilesetPath = "res://Assets/Terrain/TileSets/ByPack/FantasyDreamland.tres";

    private readonly Dictionary<string, TileDefinition> _tiles = new();

    /// <summary>
    /// Path to the tileset resource. When using compiled atlas, this returns
    /// a special marker that indicates runtime tileset creation is needed.
    /// </summary>
    public string TilesetPath { get; private set; } = DefaultTilesetPath;

    /// <summary>
    /// Whether the registry is using a compiled atlas instead of source textures.
    /// </summary>
    public bool UsingCompiledAtlas { get; private set; }

    /// <summary>
    /// The compiled TileSet if using atlas mode, null otherwise.
    /// </summary>
    public TileSet? CompiledTileSet { get; private set; }

    /// <summary>
    /// Atlas coordinate mapping for translating original coords to compiled coords.
    /// </summary>
    private CompiledAtlasLoader.AtlasMappingData? _atlasMapping;

    public TileRegistry()
    {
        LoadFromData();
    }

    public void RegisterTile(TileDefinition tile) => _tiles[tile.Id] = tile;

    public TileDefinition? GetTile(string id) => _tiles.GetValueOrDefault(id);

    public IEnumerable<TileDefinition> GetAllTiles() => _tiles.Values;

    public IEnumerable<TileDefinition> GetTilesByBiome(BiomeType biome)
    {
        foreach (var tile in _tiles.Values)
            if (tile.IsAllowedInBiome(biome))
                yield return tile;
    }

    public void Clear() => _tiles.Clear();

    /// <summary>
    /// Loads tiles from Data/Tiles/tiles.json.
    /// Automatically uses compiled atlas if available for optimized runtime loading.
    /// </summary>
    public void LoadFromData(string? path = null)
    {
        var result = TileDataLoader.LoadTileRegistry(path);

        // Check if compiled atlas is available
        if (CompiledAtlasLoader.IsCompiledAtlasAvailable())
        {
            _atlasMapping = CompiledAtlasLoader.LoadMapping();
            CompiledTileSet = CompiledAtlasLoader.LoadCompiledTileSet();

            if (CompiledTileSet != null && _atlasMapping != null)
            {
                UsingCompiledAtlas = true;
                // Use a marker path that tells consumers to use CompiledTileSet instead
                TilesetPath = _atlasMapping.Atlas?.Path ?? result.TilesetPath;

                // Register tiles with translated coordinates
                foreach (var tile in result.Tiles)
                {
                    var translated = TranslateTileToCompiledAtlas(tile);
                    RegisterTile(translated);
                }

                ILog.Print($"[TileRegistry] Registered {_tiles.Count} tiles using compiled atlas");
                return;
            }
        }

        // Fallback to original tileset
        UsingCompiledAtlas = false;
        CompiledTileSet = null;
        TilesetPath = result.TilesetPath;

        foreach (var tile in result.Tiles)
            RegisterTile(tile);

        ILog.Print($"[TileRegistry] Registered {_tiles.Count} tiles from data using tileset {TilesetPath}");
    }

    /// <summary>
    /// Translates a tile's coordinates from original to compiled atlas coordinates.
    /// </summary>
    private TileDefinition TranslateTileToCompiledAtlas(TileDefinition original)
    {
        if (_atlasMapping == null)
            return original;

        // Translate base coordinates
        var (newSourceId, newCoords) = CompiledAtlasLoader.TranslateCoordinates(
            original.SourceId,
            original.AtlasCoords,
            _atlasMapping);

        // Translate auto-tile variants if present
        Vector2I?[]? translatedVariants = null;
        if (original.AutoTileVariants != null)
        {
            translatedVariants = new Vector2I?[original.AutoTileVariants.Length];
            for (var i = 0; i < original.AutoTileVariants.Length; i++)
            {
                if (original.AutoTileVariants[i].HasValue)
                {
                    var (_, variantCoords) = CompiledAtlasLoader.TranslateCoordinates(
                        original.SourceId,
                        original.AutoTileVariants[i]!.Value,
                        _atlasMapping);
                    translatedVariants[i] = variantCoords;
                }
            }
        }

        // Translate variations if present
        Vector2I[]? translatedVars = null;
        if (original.Variations != null)
        {
            translatedVars = new Vector2I[original.Variations.Length];
            for (var i = 0; i < original.Variations.Length; i++)
            {
                var (_, varCoords) = CompiledAtlasLoader.TranslateCoordinates(
                    original.SourceId,
                    original.Variations[i],
                    _atlasMapping);
                translatedVars[i] = varCoords;
            }
        }

        // Translate animation frames if present
        TileAnimation? translatedAnimation = null;
        if (original.Animation != null)
        {
            var translatedFrames = new Vector2I[original.Animation.Frames.Length];
            for (var i = 0; i < original.Animation.Frames.Length; i++)
            {
                var (_, frameCoords) = CompiledAtlasLoader.TranslateCoordinates(
                    original.SourceId,
                    original.Animation.Frames[i],
                    _atlasMapping);
                translatedFrames[i] = frameCoords;
            }
            translatedAnimation = new TileAnimation(translatedFrames, original.Animation.FrameDuration);
        }

        // Create new tile definition with translated coordinates
        return new TileDefinition(
            id: original.Id,
            name: original.Name,
            passability: original.Passability,
            atlasCoords: newCoords,
            sourceId: newSourceId,
            layer: original.Layer,
            elevation: original.Elevation,
            isTransparent: original.IsTransparent,
            allowedBiomes: original.AllowedBiomes,
            size: original.Size,
            decorationDensity: original.DecorationDensity,
            blobSettings: original.BlobSettings,
            autoTileVariants: translatedVariants,
            autoTileFormat: original.AutoTileFormat,
            variations: translatedVars,
            variationMode: original.VariationMode,
            animation: translatedAnimation,
            dominance: original.Dominance);
    }

    /// <summary>
    /// Reloads all tiles from the data file, clearing existing tiles first
    /// </summary>
    public void Reload(string? path = null)
    {
        Clear();
        LoadFromData(path);
    }
}
