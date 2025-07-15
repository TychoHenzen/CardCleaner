using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Simple map data structure
/// </summary>
public class SimpleMapData
{
    public bool[,] Grid { get; set; }
    public Vector2I Size { get; set; }
    public Vector2I PlayerStart { get; set; }
    public List<Vector2I> EnemyPositions { get; set; } = new();
    public List<Vector2I> PassableTiles { get; set; } = new();

    public bool IsPassable(Vector2I pos)
    {
        if (pos.X < 0 || pos.X >= Size.X || pos.Y < 0 || pos.Y >= Size.Y)
            return false;
        return Grid[pos.Y, pos.X];
    }
}