using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading.TmxMapping;

/// <summary>
/// Result of resolving a tile at a specific map position.
/// </summary>
internal sealed record TmxTileResolution(
    int GlobalTileId,
    int LocalTileId,
    Vector2I AtlasCoords,
    TmxTilesetReference Tileset,
    TileDefinition? TileDefinition);
