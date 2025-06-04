using System;
using CardCleaner.Scripts.Core.Enum;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

[Tool]
[GlobalClass]
public partial class SemanticTile : Resource
{
    [Export] public string TileName { get; set; } = "";
    [Export] public TilePassability Passability { get; set; } = TilePassability.Passable;
    
    // Base visual representation
    [Export] public Texture2D BaseTexture { get; set; }
    [Export] public Vector2I BaseAtlasCoords { get; set; }
    [Export] public int SourceId { get; set; } = 0;
    
    // Tile pattern spawning rules (instead of scenes)
    [Export] public TilePattern[] SpawnPatterns { get; set; } = Array.Empty<TilePattern>();
    
    // WFC constraint sockets
    [Export] public SocketType North { get; set; } = SocketType.Any;
    [Export] public SocketType East { get; set; } = SocketType.Any; 
    [Export] public SocketType South { get; set; } = SocketType.Any;
    [Export] public SocketType West { get; set; } = SocketType.Any;
    
    public SocketType[] Sockets
    {
        get { return new[] { North, East, South, West }; }
        set
        {
            North = value[0];
            East = value[1];
            South = value[2];
            West = value[3];
        }
    }
    
    [Export] public CardSignature Signature { get; set; }
    [Export] public float BaseWeight { get; set; } = 1.0f;
    
    // WFC compatibility methods
    public bool CanConnectTo(SemanticTile other, Direction direction)
    {
        if (other == null) return false;

        var ourSocket = Sockets[(int)direction];
        var oppositeDirection = GetOppositeDirection(direction);
        var theirSocket = other.Sockets[(int)oppositeDirection];

        return SocketsCompatible(ourSocket, theirSocket);
    }

    private static bool SocketsCompatible(SocketType socket1, SocketType socket2)
    {
        if (socket1 == socket2) return true;
        if (socket1 == SocketType.Any || socket2 == SocketType.Any) return true;
        return false;
    }

    private static Direction GetOppositeDirection(Direction dir)
    {
        return dir switch
        {
            Direction.North => Direction.South,
            Direction.East => Direction.West,
            Direction.South => Direction.North,
            Direction.West => Direction.East,
            _ => throw new ArgumentException("Invalid direction")
        };
    }
}
