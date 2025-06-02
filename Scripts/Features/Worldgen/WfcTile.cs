using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enum;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

// WFC Tile as a Godot Resource
[Tool]
[GlobalClass]
public partial class WfcTile : Resource
{
    [Export] public string TileName { get; set; } = "";
    [Export] public string Description { get; set; } = "";

    // Graphics
    [Export] public SpriteRegion[] SpriteRegion { get; set; }
    [Export] public float FrameDuration { get; set; } = 0.0f;


    // Signature for gameplay influence
    [Export] public CardSignature Signature { get; set; }

    // Sockets for WFC constraints (North, East, South, West)
    [ExportCategory("Sockets")] [Export] public SocketType North { get; set; }
    [Export] public SocketType East { get; set; }
    [Export] public SocketType South { get; set; }
    [Export] public SocketType West { get; set; }
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

    // Probability weighting
    [Export] public float BaseWeight { get; set; } = 1.0f;

    // Optional: specific placement rules
    [Export] public bool CanBeAtEdge { get; set; } = true;
    [Export] public bool CanBeAtCorner { get; set; } = true;
    [Export] public int MinDistanceFromSameType { get; set; } = 0;

    // Runtime properties (set during atlas generation)
    public int AtlasId { get; set; } = -1;
    public Vector2I AtlasCoords { get; set; }
    
    public Vector2I[] AnimationFrames { get; set; } = System.Array.Empty<Vector2I>();
    public bool IsAnimated => FrameDuration > 0.0f && SpriteRegion.Length > 1;


    // Check if this tile can connect to another in a given direction
    public bool CanConnectTo(WfcTile other, Direction direction)
    {
        if (other == null) return false;

        var ourSocket = Sockets[(int)direction];
        var oppositeDirection = GetOppositeDirection(direction);
        var theirSocket = other.Sockets[(int)oppositeDirection];

        return SocketsCompatible(ourSocket, theirSocket);
    }

    private static bool SocketsCompatible(SocketType socket1, SocketType socket2)
    {
        // Same sockets always connect
        if (socket1 == socket2) return true;

        // Mixed connects to everything
        if (socket1 == SocketType.Mixed || socket2 == SocketType.Mixed) return true;

        // Add specific compatibility rules here
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