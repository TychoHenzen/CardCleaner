using System.Collections.Generic;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;

namespace CardCleaner.Features.Deckbuilder.Tiles;

/// <summary>
/// The visual side of the tile catalog: the tileset the tiles render from, whether that is the compiled atlas,
/// and the tile definitions with their atlas coordinates translated when the atlas is used.
/// </summary>
public sealed record TileVisualCatalog(
    string TilesetPath,
    bool UsingCompiledAtlas,
    TilesetConfig TilesetConfig,
    IReadOnlyList<TileDefinition> Tiles);
