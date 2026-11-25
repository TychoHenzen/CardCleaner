using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Tiles.Loading.TilesetLoading.TmxMapping;

/// <summary>
/// Result of resolving a tile at a specific map position.
/// </summary>
internal sealed record TmxTileResolution(
    int GlobalTileId,
    int LocalTileId,
    Vector2I AtlasCoords,
    TmxTilesetReference Tileset,
    TileDefinition? TileDefinition);
