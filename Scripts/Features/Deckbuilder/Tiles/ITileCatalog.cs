using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.Wfc;

namespace CardCleaner.Scripts.Features.Deckbuilder.Tiles;

/// <summary>
/// The tile queries the map generators need, without the concrete snapshot that answers them. Generators and
/// their helpers take this interface; <see cref="TileCatalogData"/> is built only by the composition root.
/// </summary>
public interface ITileCatalog : IWfcTileCatalog
{
    string? GetDefaultPassableTileId();

    string? GetDefaultSolidTileId();

    TileDefinition? GetTile(string id);

    IReadOnlyList<TileDefinition> GetAllTiles();

    IEnumerable<VariationGroup> GetAllVariationGroups();

    string? SelectPerMapVariant(string baseName, IRandom rng);
}
