using System.Collections.Generic;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Simple map data structure using tile IDs
/// </summary>
public class SimpleMapData
{
    private ITileRegistry? _tileRegistry;
    public string[,] TileIds { get; set; } = new string[0, 0];
    public BiomeType[,]? BiomeMap { get; set; }
    public Vector2I Size { get; set; }
    public Vector2I PlayerStart { get; set; }
    public List<Vector2I> EnemyPositions { get; set; } = new();
    public List<Vector2I> PassableTiles { get; set; } = new();

    public BiomeType GetBiomeAt(Vector2I pos)
    {
        if (BiomeMap == null || pos.X < 0 || pos.X >= Size.X || pos.Y < 0 || pos.Y >= Size.Y)
            return BiomeType.Plains;
        return BiomeMap[pos.Y, pos.X];
    }

    public bool IsPassable(Vector2I pos)
    {
        if (pos.X < 0 || pos.X >= Size.X || pos.Y < 0 || pos.Y >= Size.Y)
            return false;

        var tileId = TileIds[pos.Y, pos.X];

        _tileRegistry ??= ServiceLocator.Has<ITileRegistry>() ? ServiceLocator.Get<ITileRegistry>() : null;

        if (_tileRegistry == null)
        {
            // Fallback: if registry not available, assume "floor" and "grass" are passable
            return tileId is "floor" or "grass" or "dirt";
        }

        var tile = _tileRegistry.GetTile(tileId);
        return tile?.IsPassable ?? false;
    }

    public bool IsTransparent(Vector2I pos)
    {
        if (pos.X < 0 || pos.X >= Size.X || pos.Y < 0 || pos.Y >= Size.Y)
            return false;

        var tileId = TileIds[pos.Y, pos.X];

        _tileRegistry ??= ServiceLocator.Has<ITileRegistry>() ? ServiceLocator.Get<ITileRegistry>() : null;

        if (_tileRegistry == null)
        {
            // Fallback: passable tiles are transparent
            return tileId is "floor" or "grass" or "dirt" or "water" or "glass";
        }

        var tile = _tileRegistry.GetTile(tileId);
        return tile?.IsTransparent ?? false;
    }

    public string GetTileId(Vector2I pos)
    {
        if (pos.X < 0 || pos.X >= Size.X || pos.Y < 0 || pos.Y >= Size.Y)
            return "wall";
        return TileIds[pos.Y, pos.X];
    }
}
