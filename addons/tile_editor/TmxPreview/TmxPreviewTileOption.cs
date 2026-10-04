#if TOOLS
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Core.Services.TilesetLoading;

namespace CardCleaner.Addons.TileEditor;

internal sealed record TmxPreviewTileOption(
    TileDefinition Tile,
    TmxTilesetReference Tileset);
#endif
