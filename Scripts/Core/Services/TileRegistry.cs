using System.Collections.Generic;
using System.Linq;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

public class TileRegistry : ITileRegistry, ITileMetadataProvider
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
    /// Configuration for tileset-level spatial properties (tile size, grid offset).
    /// </summary>
    public TilesetConfig TilesetConfig { get; private set; } = TilesetConfig.Default;

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

    public IEnumerable<TileDefinition> GetTilesByBiome(string biomeId)
    {
        foreach (var tile in _tiles.Values)
            if (tile.IsAllowedInBiome(biomeId))
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
        var isAvailable = CompiledAtlasLoader.IsCompiledAtlasAvailable();
        ILog.Print($"[TileRegistry] Compiled atlas available: {isAvailable}");

        if (isAvailable)
        {
            _atlasMapping = CompiledAtlasLoader.LoadMapping();
            ILog.Print($"[TileRegistry] Atlas mapping loaded: {(_atlasMapping != null ? "YES" : "NULL")}");

            CompiledTileSet = CompiledAtlasLoader.LoadCompiledTileSet();
            ILog.Print($"[TileRegistry] Compiled TileSet loaded: {(CompiledTileSet != null ? "YES" : "NULL")}");

            if (CompiledTileSet != null && _atlasMapping != null)
            {
                // Use compiled atlas mode - transition_map.json has all auto-tile transitions
                // Even if some base tiles can't be translated, auto-tiles will work
                UsingCompiledAtlas = true;
                TilesetPath = _atlasMapping.Atlas?.Path ?? result.TilesetPath;
                TilesetConfig = result.TilesetConfig;

                // Register tiles WITH translation - tiles not in atlas_mapping keep original coords
                // but auto-tiles use transition_map.json which IS complete
                foreach (var tile in result.Tiles)
                {
                    var translated = TranslateTileToCompiledAtlas(tile);
                    RegisterTile(translated);
                }

                ILog.Print($"[TileRegistry] SUCCESS: Registered {_tiles.Count} tiles, using compiled atlas for auto-tiles");
                return;
            }
            else
            {
                ILog.Print("[TileRegistry] FAILED: CompiledTileSet or mapping is null, falling back");
            }
        }

        // Fallback to original tileset
        UsingCompiledAtlas = false;
        CompiledTileSet = null;
        TilesetPath = result.TilesetPath;
        TilesetConfig = result.TilesetConfig;

        foreach (var tile in result.Tiles)
            RegisterTile(tile);

        ILog.Print($"[TileRegistry] FALLBACK: Registered {_tiles.Count} tiles using tileset {TilesetPath}");
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
            autoTileVariants: translatedVariants,
            autoTileFormatName: original.AutoTileFormatName,
            variations: translatedVars,
            variationMode: original.VariationMode,
            animation: translatedAnimation,
            dominance: original.Dominance,
            innerTerrainId: original.InnerTerrainId,
            outerTerrainId: original.OuterTerrainId,
            isGapTile: original.IsGapTile);
    }

    /// <summary>
    /// Reloads all tiles from the data file, clearing existing tiles first
    /// </summary>
    public void Reload(string? path = null)
    {
        Clear();
        LoadFromData(path);
    }

    #region ITileMetadataProvider Implementation

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetSimpleTerrainTiles()
    {
        return _tiles.Values
            .Where(t => t.IsSimpleTerrain)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetAutoTiles()
    {
        return _tiles.Values
            .Where(t => t.IsAutoTile)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetGapTiles()
    {
        return _tiles.Values
            .Where(t => t.IsGapTile)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetPassableTerrainTiles()
    {
        return _tiles.Values
            .Where(t => t.Layer == TileLayer.Terrain && t.IsPassable)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetSolidTerrainTiles()
    {
        return _tiles.Values
            .Where(t => t.Layer == TileLayer.Terrain && t.IsSolid)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetDecorationTiles()
    {
        return _tiles.Values
            .Where(t => t.IsDecoration)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetTilesByLayer(TileLayer layer)
    {
        return _tiles.Values
            .Where(t => t.Layer == layer)
            .ToList();
    }

    /// <inheritdoc />
    IReadOnlyList<TileDefinition> ITileMetadataProvider.GetTilesByBiome(string biomeId)
    {
        return _tiles.Values
            .Where(t => t.IsAllowedInBiome(biomeId))
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetBackgroundTerrainTiles()
    {
        // Simple terrain tiles that can serve as backgrounds for auto-tiles
        return _tiles.Values
            .Where(t => t.IsSimpleTerrain && t.IsPassable)
            .ToList();
    }

    /// <inheritdoc />
    public TileDefinition? GetDefaultPassableTile()
    {
        // First try gap tiles (they're specifically meant to be passable fillers)
        var gapTile = _tiles.Values.FirstOrDefault(t => t.IsGapTile && t.IsPassable);
        if (gapTile != null) return gapTile;

        // Then try simple passable terrain
        return _tiles.Values.FirstOrDefault(t => t.IsSimpleTerrain && t.IsPassable)
            ?? _tiles.Values.FirstOrDefault(t => t.Layer == TileLayer.Terrain && t.IsPassable);
    }

    /// <inheritdoc />
    public TileDefinition? GetDefaultSolidTile()
    {
        return _tiles.Values.FirstOrDefault(t => t.Layer == TileLayer.Terrain && t.IsSolid);
    }

    /// <inheritdoc />
    public TileDefinition? GetDefaultGapTile()
    {
        // First try explicit gap tiles
        var gapTile = _tiles.Values.FirstOrDefault(t => t.IsGapTile);
        if (gapTile != null) return gapTile;

        // Fall back to any simple passable terrain tile
        return _tiles.Values.FirstOrDefault(t => t.IsSimpleTerrain && t.IsPassable);
    }

    /// <inheritdoc />
    public bool IsAutoTile(string tileId)
    {
        return _tiles.TryGetValue(tileId, out var tile) && tile.IsAutoTile;
    }

    /// <inheritdoc />
    public bool IsPassable(string tileId)
    {
        return _tiles.TryGetValue(tileId, out var tile) && tile.IsPassable;
    }

    /// <inheritdoc />
    public bool IsSolid(string tileId)
    {
        return _tiles.TryGetValue(tileId, out var tile) && tile.IsSolid;
    }

    /// <inheritdoc />
    public bool IsGapTile(string tileId)
    {
        return _tiles.TryGetValue(tileId, out var tile) && tile.IsGapTile;
    }

    /// <inheritdoc />
    public bool IsTransparent(string tileId)
    {
        return _tiles.TryGetValue(tileId, out var tile) && tile.IsTransparent;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetWfcTileIds()
    {
        return _tiles.Values
            .Where(t => t.Layer == TileLayer.Terrain)
            .Select(t => t.Id)
            .ToList();
    }

    /// <inheritdoc />
    public string? GetDefaultPassableTileId() => GetDefaultPassableTile()?.Id;

    /// <inheritdoc />
    public string? GetDefaultSolidTileId() => GetDefaultSolidTile()?.Id;

    /// <inheritdoc />
    public string? GetDefaultGapTileId() => GetDefaultGapTile()?.Id;

    #endregion
}
