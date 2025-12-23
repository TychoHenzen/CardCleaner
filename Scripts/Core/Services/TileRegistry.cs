using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

public class TileRegistry : ITileRegistry
{
    private readonly Dictionary<string, TileDefinition> _tiles = new();

    // Default tileset source ID - change this to match your TileSet in Godot
    public const int DefaultSourceId = 4;

    public TileRegistry()
    {
        RegisterDefaultTiles();
    }

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
            atlasCoords: new Vector2I(4, 0),
            sourceId: DefaultSourceId));

        RegisterTile(new TileDefinition(
            id: "floor_visited",
            name: "Visited Floor",
            passability: TilePassability.Passable,
            atlasCoords: new Vector2I(6, 0),
            sourceId: DefaultSourceId));

        RegisterTile(new TileDefinition(
            id: "grass",
            name: "Grass",
            passability: TilePassability.Passable,
            atlasCoords: new Vector2I(0, 0),
            sourceId: DefaultSourceId));

        RegisterTile(new TileDefinition(
            id: "dirt",
            name: "Dirt",
            passability: TilePassability.Passable,
            atlasCoords: new Vector2I(1, 0),
            sourceId: DefaultSourceId));

        // Blocked tiles (walls, obstacles)
        RegisterTile(new TileDefinition(
            id: "wall",
            name: "Wall",
            passability: TilePassability.Solid,
            atlasCoords: new Vector2I(2, 0),
            sourceId: DefaultSourceId,
            layer: TileLayer.Structure,
            elevation: 1f,
            isTransparent: false));

        RegisterTile(new TileDefinition(
            id: "stone",
            name: "Stone",
            passability: TilePassability.Solid,
            atlasCoords: new Vector2I(3, 0),
            sourceId: DefaultSourceId,
            layer: TileLayer.Structure,
            elevation: 0.5f,
            isTransparent: false));

        // Water (not passable but transparent for visibility)
        RegisterTile(new TileDefinition(
            id: "water",
            name: "Water",
            passability: TilePassability.Solid,
            atlasCoords: new Vector2I(5, 0),
            sourceId: DefaultSourceId,
            layer: TileLayer.Terrain,
            elevation: -0.5f,
            isTransparent: true));

        // Glass (not passable but transparent)
        RegisterTile(new TileDefinition(
            id: "glass",
            name: "Glass Wall",
            passability: TilePassability.Solid,
            atlasCoords: new Vector2I(7, 0),
            sourceId: DefaultSourceId,
            layer: TileLayer.Structure,
            elevation: 1f,
            isTransparent: true));
    }

    public void RegisterTile(TileDefinition tile)
    {
        _tiles[tile.Id] = tile;
    }

    public TileDefinition? GetTile(string id)
    {
        return _tiles.GetValueOrDefault(id);
    }

    public IEnumerable<TileDefinition> GetAllTiles()
    {
        return _tiles.Values;
    }

    public void Clear()
    {
        _tiles.Clear();
    }
}
