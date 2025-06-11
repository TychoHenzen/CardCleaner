using System;
using System.Linq;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Enum;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;
using Godot.Collections;
using Array = System.Array;

namespace CardCleaner.Scripts.Features.Worldgen;

[Tool]
[GlobalClass]
public partial class SemanticTile : Resource
{
    [Export] public string TileName { get; set; } = "";
    [Export] public TilePassability Passability { get; set; } = TilePassability.Passable;

    // Pattern properties (every tile is now a pattern)
    [Export] public TileSet? TileSet { get; set; }
    [Export] public Vector2I Size { get; set; } = Vector2I.One;
    [Export] public TilePlacement Tile { get; set; } = new();
    
    // Layered spawning system
    [Export] public float GlobalSpawnChance { get; set; } = 0.0f;
    [Export] public TileLayer Layer { get; set; }
    [Export] public float BaseWeight { get; set; } = 1.0f;
    [Export] public CardSignature Signature { get; set; }
    /// <summary>
    /// Constraint modifications this tile applies to other layers when placed
    /// </summary>
    [Export] public Array<LayerConstraint> LayerConstraints { get; set; } = new();
    [ExportSubgroup("Tags")]
    [Export] public Array<CompatibilityTag> North { get; set; } = new();
    [Export] public Array<CompatibilityTag>  East { get; set; } = new();
    [Export] public Array<CompatibilityTag>  South { get; set; } = new();
    [Export] public Array<CompatibilityTag>  West { get; set; } = new();
    [Export] public Array<CompatibilityTag>  NorthEast { get; set; } = new();
    [Export] public Array<CompatibilityTag>  SouthEast { get; set; } = new();
    [Export] public Array<CompatibilityTag>  SouthWest { get; set; } = new();
    [Export] public Array<CompatibilityTag>  NorthWest { get; set; } = new();
    [Export] public Array<CompatibilityTag>  Down { get; set; } = new();
    [Export] public Array<CompatibilityTag>  Up { get; set; } = new();
    /// <summary>
    /// Check if this tile modifies constraints on other layers
    /// </summary>
    public bool HasLayerConstraints => LayerConstraints.Count > 0;
    
    public Array<CompatibilityTag>[] Sockets
    {
        get { return new[] { North, East, South, West, NorthEast, SouthEast, SouthWest, NorthWest, Down, Up }; }
        set
        {
            North = value[0];
            East = value[1];
            South = value[2];
            West = value[3];
            NorthEast = value[4];
            SouthEast = value[5];
            SouthWest = value[6];
            NorthWest = value[7];
            Down = value[8];
            Up = value[9];
        }
    }

    // Pattern methods
    public TilePlacement GetTileAt(Vector2I localPos)
    {
        return Tile.WithOffset(localPos);
    }
    
    public bool ShouldBlockAt(Vector2I localPos)
    {
        var tile = GetTileAt(localPos);
        return tile?.BlocksTiles ?? false;
    }

    // WFC compatibility methods
    public bool CanConnectTo(SemanticTile other, Direction direction)
    {
        if (other == null) return false;

        var ourSocket = Sockets[(int)direction];
        var oppositeDirection = GetOppositeDirection(direction);
        var theirSocket = other.Sockets[(int)oppositeDirection];

        return ourSocket.Any(ours => theirSocket.Any(ours.IsCompatibleWith));
    }


    private static Direction GetOppositeDirection(Direction dir)
    {
        return dir switch
        {
            Direction.North => Direction.South,
            Direction.East => Direction.West,
            Direction.South => Direction.North,
            Direction.West => Direction.East,
            Direction.NorthEast => Direction.SouthWest,
            Direction.SouthEast => Direction.NorthWest,
            Direction.SouthWest => Direction.NorthEast,
            Direction.NorthWest => Direction.SouthEast,
            _ => throw new ArgumentException("Invalid direction")
        };
    }
}