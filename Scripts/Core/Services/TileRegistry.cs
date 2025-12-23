using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

public class TileRegistry : ITileRegistry
{
    private readonly Dictionary<string, TileDefinition> _tiles = new();

    public TileRegistry()
    {
        RegisterDefaultTiles();
    }

    private void RegisterDefaultTiles()
    {
        // Floor tiles (passable)
        RegisterTile(new TileDefinition(
            "floor",
            "Floor",
            TilePassability.Passable,
            new Vector2I(4, 0)));

        RegisterTile(new TileDefinition(
            "floor_visited",
            "Visited Floor",
            TilePassability.Passable,
            new Vector2I(6, 0)));

        RegisterTile(new TileDefinition(
            "grass",
            "Grass",
            TilePassability.Passable,
            new Vector2I(0, 0)));

        RegisterTile(new TileDefinition(
            "dirt",
            "Dirt",
            TilePassability.Passable,
            new Vector2I(1, 0)));

        // Blocked tiles (walls, obstacles)
        RegisterTile(new TileDefinition(
            "wall",
            "Wall",
            TilePassability.Solid,
            new Vector2I(2, 0),
            TileLayer.Structure,
            elevation: 1f,
            isTransparent: false));

        RegisterTile(new TileDefinition(
            "stone",
            "Stone",
            TilePassability.Solid,
            new Vector2I(3, 0),
            TileLayer.Structure,
            elevation: 0.5f,
            isTransparent: false));

        // Water (not passable but transparent for visibility)
        RegisterTile(new TileDefinition(
            "water",
            "Water",
            TilePassability.Solid,
            new Vector2I(5, 0),
            TileLayer.Terrain,
            elevation: -0.5f,
            isTransparent: true));

        // Glass (not passable but transparent)
        RegisterTile(new TileDefinition(
            "glass",
            "Glass Wall",
            TilePassability.Solid,
            new Vector2I(7, 0),
            TileLayer.Structure,
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
