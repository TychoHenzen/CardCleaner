using System.Collections.Generic;
using System.Linq;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services.TilesetLoading;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

public class TileRegistry : ITileRegistry, ITileMetadataProvider
{
    public const int DefaultSourceId = 4;
    private const string DefaultTilesetPath = "res://Assets/Terrain/TileSets/ByPack/FantasyDreamland.tres";

    private readonly Dictionary<string, TileDefinition> _tiles = new();
    private readonly VariationGroupCollection _variationGroups = new();

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

    private readonly TileCatalogQueries _queries;

    public TileRegistry()
    {
        _queries = new TileCatalogQueries(_tiles);
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

    public void Clear()
    {
        _tiles.Clear();
        _variationGroups.Clear();
    }

    /// <summary>
    /// Gets the variation group containing the specified tile, if any.
    /// </summary>
    public VariationGroup? GetVariationGroup(string tileId)
    {
        return _variationGroups.FindGroupContaining(tileId);
    }

    /// <summary>
    /// Gets the variation group by its base name (e.g., "grass").
    /// </summary>
    public VariationGroup? GetVariationGroupByBaseName(string baseName)
    {
        return _variationGroups.GetGroupByBaseName(baseName);
    }

    /// <inheritdoc />
    public bool AreSameTerrainType(string? tileId1, string? tileId2)
        => TerrainTypeComparer.AreSame(tileId1, tileId2, _variationGroups, GetTile);

    /// <summary>
    /// Selects a per-map variant for the given base name using weighted random selection.
    /// Returns the tile ID of the selected variant.
    /// </summary>
    public string? SelectPerMapVariant(string baseName, RandomNumberGenerator rng)
    {
        return WeightedVariantSelector.Select(_variationGroups.GetGroupByBaseName(baseName), rng);
    }

    /// <summary>
    /// Gets all variants for a base name with their weights (for per-instance selection).
    /// </summary>
    public IReadOnlyList<VariantWeight> GetInstanceVariants(string baseName)
    {
        return _variationGroups.GetVariantsFor(baseName);
    }

    /// <summary>
    /// Returns all variation groups.
    /// </summary>
    public IEnumerable<VariationGroup> GetAllVariationGroups()
    {
        return _variationGroups.GetAllGroups();
    }

    /// <summary>
    /// Loads tiles from Data/Tiles/tiles.json.
    /// Automatically uses compiled atlas if available for optimized runtime loading.
    /// </summary>
    public void LoadFromData(string? path = null)
    {
        var result = TileDataLoader.LoadTileRegistry(path);

        if (!TryUseCompiledAtlas(result))
            UseOriginalTileset(result);

        VariationGroupBuilder.Rebuild(_tiles.Values, _variationGroups);
    }

    private bool TryUseCompiledAtlas(TileRegistryResult result)
    {
        // Check if compiled atlas is available
        var isAvailable = CompiledAtlasLoader.IsCompiledAtlasAvailable();
        ILog.Print($"[TileRegistry] Compiled atlas available: {isAvailable}");

        if (!isAvailable)
            return false;

        _atlasMapping = CompiledAtlasLoader.LoadMapping();
        ILog.Print($"[TileRegistry] Atlas mapping loaded: {(_atlasMapping != null ? "YES" : "NULL")}");

        CompiledTileSet = CompiledAtlasLoader.LoadCompiledTileSet();
        ILog.Print($"[TileRegistry] Compiled TileSet loaded: {(CompiledTileSet != null ? "YES" : "NULL")}");

        if (CompiledTileSet == null || _atlasMapping == null)
        {
            ILog.Print("[TileRegistry] FAILED: CompiledTileSet or mapping is null, falling back");
            return false;
        }

        UseCompiledAtlas(result, _atlasMapping);
        return true;
    }

    // Use compiled atlas mode - transition_map.json has all auto-tile transitions
    // Even if some base tiles can't be translated, auto-tiles will work
    private void UseCompiledAtlas(TileRegistryResult result, CompiledAtlasLoader.AtlasMappingData atlasMapping)
    {
        UsingCompiledAtlas = true;
        TilesetPath = atlasMapping.Atlas?.Path ?? result.TilesetPath;

        // CRITICAL: Use compiled atlas tile size, NOT the TSX source tile size
        // TSX may have smaller source tiles (e.g., 8x8) that get composited to larger tiles (e.g., 16x16)
        var atlasTileSize = atlasMapping.Atlas?.TileSize ?? 16;
        TilesetConfig = new TilesetConfig
        {
            BaseTileSize = new Vector2I(atlasTileSize, atlasTileSize)
        };

        // Register tiles WITH translation - tiles not in atlas_mapping keep original coords
        // but auto-tiles use transition_map.json which IS complete
        var translator = new CompiledAtlasTileTranslator(atlasMapping);
        foreach (var tile in result.Tiles)
            RegisterTile(translator.Translate(tile));

        ILog.Print(
            $"[TileRegistry] SUCCESS: Registered {_tiles.Count} tiles, " +
            "using compiled atlas for auto-tiles");
    }

    // Fallback to original tileset
    private void UseOriginalTileset(TileRegistryResult result)
    {
        UsingCompiledAtlas = false;
        CompiledTileSet = null;
        TilesetPath = result.TilesetPath;
        TilesetConfig = result.TilesetConfig;

        foreach (var tile in result.Tiles)
            RegisterTile(tile);

        ILog.Print($"[TileRegistry] FALLBACK: Registered {_tiles.Count} tiles using tileset {TilesetPath}");
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
    public IReadOnlyList<TileDefinition> GetSimpleTerrainTiles() => _queries.GetSimpleTerrainTiles();

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetAutoTiles() => _queries.GetAutoTiles();

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetGapTiles() => _queries.GetGapTiles();

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetPassableTerrainTiles() => _queries.GetPassableTerrainTiles();

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetSolidTerrainTiles() => _queries.GetSolidTerrainTiles();

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetDecorationTiles() => _queries.GetDecorationTiles();

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetTilesByLayer(TileLayer layer) => _queries.GetTilesByLayer(layer);

    /// <inheritdoc />
    IReadOnlyList<TileDefinition> ITileMetadataProvider.GetTilesByBiome(string biomeId)
        => _queries.GetTilesByBiome(biomeId);

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetBackgroundTerrainTiles() => _queries.GetBackgroundTerrainTiles();

    /// <inheritdoc />
    public TileDefinition? GetDefaultPassableTile() => _queries.GetDefaultPassableTile();

    /// <inheritdoc />
    public TileDefinition? GetDefaultSolidTile() => _queries.GetDefaultSolidTile();

    /// <inheritdoc />
    public TileDefinition? GetDefaultGapTile() => _queries.GetDefaultGapTile();

    /// <inheritdoc />
    public bool IsAutoTile(string tileId) => _queries.IsAutoTile(tileId);

    /// <inheritdoc />
    public bool IsPassable(string tileId) => _queries.IsPassable(tileId);

    /// <inheritdoc />
    public bool IsSolid(string tileId) => _queries.IsSolid(tileId);

    /// <inheritdoc />
    public bool IsGapTile(string tileId) => _queries.IsGapTile(tileId);

    /// <inheritdoc />
    public bool IsTransparent(string tileId) => _queries.IsTransparent(tileId);

    /// <inheritdoc />
    public IReadOnlyList<string> GetWfcTileIds() => _queries.GetWfcTileIds();

    /// <inheritdoc />
    public string? GetDefaultPassableTileId() => GetDefaultPassableTile()?.Id;

    /// <inheritdoc />
    public string? GetDefaultSolidTileId() => GetDefaultSolidTile()?.Id;

    /// <inheritdoc />
    public string? GetDefaultGapTileId() => GetDefaultGapTile()?.Id;

    #endregion
}
