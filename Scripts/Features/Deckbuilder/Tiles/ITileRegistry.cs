using System.Collections.Generic;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Deckbuilder.Tiles;

public interface ITileRegistry
{
    /// <summary>
    /// The visual side of the catalog: the tileset path, whether the compiled atlas is used, and the tileset config.
    /// </summary>
    TileVisualCatalog Visuals { get; }

    void RegisterTile(TileDefinition tile);
    TileDefinition? GetTile(string id);
    IEnumerable<TileDefinition> GetAllTiles();
    IEnumerable<TileDefinition> GetTilesByBiome(string biomeId);
    void Clear();

    /// <summary>
    /// Gets the variation group containing the specified tile, if any.
    /// </summary>
    VariationGroup? GetVariationGroup(string tileId);

    /// <summary>
    /// Gets the variation group by its base name (e.g., "grass").
    /// </summary>
    VariationGroup? GetVariationGroupByBaseName(string baseName);

    /// <summary>Determines whether two tile IDs represent the same terrain type.</summary>
    bool AreSameTerrainType(string? tileId1, string? tileId2);

    /// <summary>
    /// Selects a per-map variant for the given base name using weighted random selection.
    /// Returns the tile ID of the selected variant.
    /// </summary>
    string? SelectPerMapVariant(string baseName, IRandom rng);

    /// <summary>
    /// Gets all variants for a base name with their weights (for per-instance selection).
    /// </summary>
    IReadOnlyList<VariantWeight> GetInstanceVariants(string baseName);

    /// <summary>
    /// Returns all variation groups.
    /// </summary>
    IEnumerable<VariationGroup> GetAllVariationGroups();
}
