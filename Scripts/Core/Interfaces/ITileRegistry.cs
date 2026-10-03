using System.Collections.Generic;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface ITileRegistry
{
    /// <summary>
    /// Path to the TileSet resource used by these tiles.
    /// When using compiled atlas, this returns the atlas path.
    /// </summary>
    string TilesetPath { get; }

    /// <summary>
    /// Whether the registry is using a compiled atlas instead of source textures.
    /// </summary>
    bool UsingCompiledAtlas { get; }

    /// <summary>
    /// The compiled TileSet if using atlas mode. Use this instead of loading TilesetPath
    /// when UsingCompiledAtlas is true.
    /// </summary>
    TileSet? CompiledTileSet { get; }

    /// <summary>
    /// Configuration for tileset-level spatial properties (tile size, grid offset).
    /// </summary>
    TilesetConfig TilesetConfig { get; }

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
    string? SelectPerMapVariant(string baseName, RandomNumberGenerator rng);

    /// <summary>
    /// Gets all variants for a base name with their weights (for per-instance selection).
    /// </summary>
    IReadOnlyList<VariantWeight> GetInstanceVariants(string baseName);

    /// <summary>
    /// Returns all variation groups.
    /// </summary>
    IEnumerable<VariationGroup> GetAllVariationGroups();
}
