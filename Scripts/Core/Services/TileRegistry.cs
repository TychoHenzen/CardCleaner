using System.Collections.Generic;
using System.Linq;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services.TilesetLoading;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;

namespace CardCleaner.Scripts.Core.Services;

public class TileRegistry : ITileRegistry, ITileMetadataProvider
{
    public const int DefaultSourceId = 4;
    private const string DefaultTilesetPath = "res://Assets/Terrain/TileSets/ByPack/FantasyDreamland.tres";

    private readonly Dictionary<string, TileDefinition> _tiles = new();
    private readonly VariationGroupCollection _variationGroups = new();

    /// <summary>
    /// The visual side of the catalog (tileset path, compiled atlas mode, tileset config), set by Install.
    /// </summary>
    public TileVisualCatalog Visuals { get; private set; } =
        new(DefaultTilesetPath, false, TilesetConfig.Default, []);

    private readonly TileCatalogQueries _queries;

    internal TileRegistry()
    {
        _queries = new TileCatalogQueries(_tiles);
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
    public string? SelectPerMapVariant(string baseName, IRandom rng)
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
    /// Registers the tiles of a loaded visual catalog on top of the tiles already registered and rebuilds the
    /// variation groups. <c>TileCatalogLoad</c> builds the catalog from the tile data files.
    /// </summary>
    internal void Install(TileVisualCatalog visuals)
    {
        Visuals = visuals;
        foreach (var tile in Visuals.Tiles)
            RegisterTile(tile);

        VariationGroupBuilder.Rebuild(_tiles.Values, _variationGroups);
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
