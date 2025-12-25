using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

public class TileRegistry : ITileRegistry
{
    // Default tileset source ID - change this to match your TileSet in Godot
    public const int DefaultSourceId = 4;
    private readonly Dictionary<string, TileDefinition> _tiles = new();

    public TileRegistry()
    {
        RegisterDefaultTiles();
    }

    public void RegisterTile(TileDefinition tile) => _tiles[tile.Id] = tile;

    public TileDefinition? GetTile(string id) => _tiles.GetValueOrDefault(id);

    public IEnumerable<TileDefinition> GetAllTiles() => _tiles.Values;

    public IEnumerable<TileDefinition> GetTilesByBiome(BiomeType biome)
    {
        foreach (var tile in _tiles.Values)
            if (tile.IsAllowedInBiome(biome))
                yield return tile;
    }

    public void Clear() => _tiles.Clear();

    private void RegisterDefaultTiles()
    {
        // ============================================================
        // CONFIGURE YOUR TILES HERE
        // AtlasCoords = Vector2I(x, y) position in tileset atlas
        // SourceId = TileSet source ID from Godot editor
        // Open your .tres tileset in Godot to find correct values
        // ============================================================

        // Floor tiles (passable)
        RegisterTile(new TileDefinition(
            id: "floor",
            name: "Floor",
            passability: TilePassability.Passable,
            atlasCoords: new Vector2I(14, 0),
            allowedBiomes: [BiomeType.Desert, BiomeType.Tundra]));

        RegisterTile(new TileDefinition(
            id: "floor_visited",
            name: "Visited Floor",
            passability: TilePassability.Passable,
            atlasCoords: new Vector2I(11, 6),
            sourceId: 49));

        // Debug overlay tiles for path visualization
        RegisterTile(new TileDefinition(
            id: "debug_path",
            name: "Debug Path",
            passability: TilePassability.Passable,
            atlasCoords: new Vector2I(12, 6),
            sourceId: 49));

        RegisterTile(new TileDefinition(
            id: "debug_target",
            name: "Debug Target",
            passability: TilePassability.Passable,
            atlasCoords: new Vector2I(13, 6),
            sourceId: 49));

        RegisterTile(new TileDefinition(
            id: "grass",
            name: "Grass",
            passability: TilePassability.Passable,
            atlasCoords: new Vector2I(4, 0),
            allowedBiomes: [BiomeType.Plains, BiomeType.Forest]));

        RegisterTile(new TileDefinition(
            id: "dirt",
            name: "Dirt",
            passability: TilePassability.Passable,
            atlasCoords: new Vector2I(6, 0),
            sourceId: DefaultSourceId));

        // Blocked tiles (walls, obstacles)
        RegisterTile(new TileDefinition(
            id: "wall",
            name: "Wall",
            passability: TilePassability.Solid,
            atlasCoords: new Vector2I(22, 5),
            sourceId: DefaultSourceId,
            layer: TileLayer.Structure,
            elevation: 1f,
            isTransparent: false));

        RegisterTile(new TileDefinition(
            id: "stone",
            name: "Stone",
            passability: TilePassability.Solid,
            atlasCoords: new Vector2I(22, 7),
            sourceId: DefaultSourceId,
            layer: TileLayer.Structure,
            elevation: 0.5f,
            isTransparent: false));

        // Water (not passable but transparent for visibility)
        RegisterTile(new TileDefinition(
            id: "water",
            name: "Water",
            passability: TilePassability.Solid,
            atlasCoords: new Vector2I(7, 27),
            sourceId: 17,
            layer: TileLayer.Terrain,
            elevation: -0.5f,
            true,
            [BiomeType.Tundra]));

        // Glass (not passable but transparent)
        RegisterTile(new TileDefinition(
            id: "glass",
            name: "Glass Wall",
            passability: TilePassability.Solid,
            atlasCoords: new Vector2I(29, 16),
            sourceId: DefaultSourceId,
            layer: TileLayer.Structure,
            elevation: 1f,
            isTransparent: true));
    }
}
