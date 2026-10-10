using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Tiles;

/// <summary>
/// Presents an <see cref="ITileRegistry"/> as the <see cref="IWfcTileCatalog"/> the WFC code reads.
/// Every member asks the registry on each call, so tiles registered after construction are visible.
/// </summary>
public sealed class TileRegistryWfcCatalog : IWfcTileCatalog
{
    private const int SolidFillBitmask = 15;

    private readonly ITileRegistry _registry;

    public TileRegistryWfcCatalog(ITileRegistry registry)
    {
        _registry = registry;
    }

    public IEnumerable<string> TileIds => _registry.GetAllTiles().Select(tile => tile.Id);

    public bool Contains(string tileId) => _registry.GetTile(tileId) != null;

    public bool IsAutoTile(string tileId) => _registry.GetTile(tileId)?.HasAutoTileVariants == true;

    public bool HasSolidFillVariant(string tileId) =>
        _registry.GetTile(tileId)?.AutoTileVariants is { Length: > SolidFillBitmask } variants
        && variants[SolidFillBitmask].HasValue;

    public bool IsPassable(string tileId) => _registry.GetTile(tileId)?.IsPassable ?? false;

    public float GetProbability(string tileId) => _registry.GetTile(tileId)?.Probability ?? 1f;

    public bool HasBiomeRestriction(string tileId) => _registry.GetTile(tileId)?.AllowedBiomes != null;

    public bool IsAllowedInBiome(string tileId, string biomeId) =>
        _registry.GetTile(tileId)?.IsAllowedInBiome(biomeId) == true;

    public bool AreSameTerrainType(string? tileId1, string? tileId2) =>
        _registry.AreSameTerrainType(tileId1, tileId2);

    public WfcTileVariation? GetVariation(string tileId)
    {
        var group = _registry.GetVariationGroup(tileId);
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
        _registry.GetTile(tileId)?.GetAutoTileFormat()?.IsBitmaskAllowed(bitmask);

    public (Vector2I Size, Vector2I Offset)? GetMultiCellBounds(string tileId) =>
        _registry.GetTile(tileId)?.GetAutoTileFormat()?.GetMaxMultiCellBounds();
}
