using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Tiles;

/// <summary>
/// An immutable snapshot of the tile definitions and variation groups of a registry. It answers the same
/// queries as the registry it was taken from, and is the <see cref="IWfcTileCatalog"/> the WFC code reads.
/// Tiles registered in the source after <see cref="From"/> are not visible.
/// </summary>
public sealed class TileCatalogData : ITileCatalog
{
    private const int SolidFillBitmask = 15;

    private readonly IReadOnlyList<TileDefinition> _tiles;
    private readonly Dictionary<string, TileDefinition> _index;
    private readonly VariationGroupCollection _variationGroups = new();
    private readonly TileCatalogQueries _queries;

    public TileCatalogData(IEnumerable<TileDefinition> tiles, IEnumerable<VariationGroup> groups)
    {
        // ASSUMPTION: tile ids are unique within the input, as a registry guarantees; a repeated id keeps its
        // last definition in the index, as the registry's own dictionary does.
        _tiles = tiles.ToList();
        _index = new Dictionary<string, TileDefinition>();
        foreach (var tile in _tiles)
            _index[tile.Id] = tile;

        foreach (var group in groups)
            _variationGroups.AddGroup(group);

        _queries = new TileCatalogQueries(_index);
    }

    /// <summary>
    /// Takes a snapshot of the registry's tiles and variation groups, in the order the registry enumerates them.
    /// </summary>
    public static TileCatalogData From(ITileRegistry registry) =>
        new(registry.GetAllTiles(), registry.GetAllVariationGroups());

    public TileDefinition? GetTile(string id) => _index.GetValueOrDefault(id);

    public IReadOnlyList<TileDefinition> GetAllTiles() => _tiles;

    public IReadOnlyList<TileDefinition> GetTilesByBiome(string biomeId) => _queries.GetTilesByBiome(biomeId);

    public IReadOnlyList<TileDefinition> GetSimpleTerrainTiles() => _queries.GetSimpleTerrainTiles();

    public IReadOnlyList<TileDefinition> GetAutoTiles() => _queries.GetAutoTiles();

    public IReadOnlyList<TileDefinition> GetGapTiles() => _queries.GetGapTiles();

    public IReadOnlyList<TileDefinition> GetPassableTerrainTiles() => _queries.GetPassableTerrainTiles();

    public IReadOnlyList<TileDefinition> GetSolidTerrainTiles() => _queries.GetSolidTerrainTiles();

    public IReadOnlyList<TileDefinition> GetDecorationTiles() => _queries.GetDecorationTiles();

    public IReadOnlyList<TileDefinition> GetTilesByLayer(TileLayer layer) => _queries.GetTilesByLayer(layer);

    public IReadOnlyList<TileDefinition> GetBackgroundTerrainTiles() => _queries.GetBackgroundTerrainTiles();

    public TileDefinition? GetDefaultPassableTile() => _queries.GetDefaultPassableTile();

    public TileDefinition? GetDefaultSolidTile() => _queries.GetDefaultSolidTile();

    public TileDefinition? GetDefaultGapTile() => _queries.GetDefaultGapTile();

    public string? GetDefaultPassableTileId() => GetDefaultPassableTile()?.Id;

    public string? GetDefaultSolidTileId() => GetDefaultSolidTile()?.Id;

    public string? GetDefaultGapTileId() => GetDefaultGapTile()?.Id;

    public bool IsAutoTile(string tileId) => _queries.IsAutoTile(tileId);

    public bool IsPassable(string tileId) => _queries.IsPassable(tileId);

    public bool IsSolid(string tileId) => _queries.IsSolid(tileId);

    public bool IsGapTile(string tileId) => _queries.IsGapTile(tileId);

    public bool IsTransparent(string tileId) => _queries.IsTransparent(tileId);

    public IReadOnlyList<string> GetWfcTileIds() => _queries.GetWfcTileIds();

    public VariationGroup? GetVariationGroup(string tileId) => _variationGroups.FindGroupContaining(tileId);

    public VariationGroup? GetVariationGroupByBaseName(string baseName) =>
        _variationGroups.GetGroupByBaseName(baseName);

    public IEnumerable<VariationGroup> GetAllVariationGroups() => _variationGroups.GetAllGroups();

    public IReadOnlyList<VariantWeight> GetInstanceVariants(string baseName) =>
        _variationGroups.GetVariantsFor(baseName);

    public string? SelectPerMapVariant(string baseName, IRandom rng) =>
        WeightedVariantSelector.Select(_variationGroups.GetGroupByBaseName(baseName), rng);

    #region IWfcTileCatalog

    public IEnumerable<string> TileIds => _tiles.Select(tile => tile.Id);

    public bool Contains(string tileId) => _index.ContainsKey(tileId);

    public bool HasSolidFillVariant(string tileId) =>
        _index.GetValueOrDefault(tileId)?.AutoTileVariants is { Count: > SolidFillBitmask } variants
        && variants[SolidFillBitmask].HasValue;

    public float GetProbability(string tileId) => _index.GetValueOrDefault(tileId)?.Probability ?? 1f;

    public bool HasBiomeRestriction(string tileId) => _index.GetValueOrDefault(tileId)?.AllowedBiomes != null;

    public bool IsAllowedInBiome(string tileId, string biomeId) =>
        _index.GetValueOrDefault(tileId)?.IsAllowedInBiome(biomeId) == true;

    public bool AreSameTerrainType(string? tileId1, string? tileId2) =>
        TerrainTypeComparer.AreSame(tileId1, tileId2, _variationGroups, GetTile);

    public WfcTileVariation? GetVariation(string tileId)
    {
        var group = _variationGroups.FindGroupContaining(tileId);
        if (group == null)
            return null;

        var perGeneration = group.Mode == VariationMode.PerGeneration;
        return new WfcTileVariation(
            group.BaseName,
            perGeneration,
            group.MaxWeight,
            perGeneration ? 0f : group.GetNormalizedWeight(tileId));
    }

    public bool? IsBitmaskAllowed(string tileId, int bitmask) =>
        _index.GetValueOrDefault(tileId)?.AllowedBitmasks?.Contains(bitmask);

    public (Vector2I Size, Vector2I Offset)? GetMultiCellBounds(string tileId) =>
        _index.GetValueOrDefault(tileId)?.MultiCellBounds;

    #endregion
}
