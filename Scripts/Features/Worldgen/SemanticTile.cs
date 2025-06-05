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

    // Pattern properties (every tile is now a pattern)
    [Export] public TileSet? TileSet { get; set; }
    [Export] public Vector2I Size { get; set; } = Vector2I.One;
    [Export] public TilePlacement[] Tiles { get; set; } = Array.Empty<TilePlacement>();
    
    // Layered spawning system
    [Export] public float GlobalSpawnChance { get; set; } = 0.0f;
    [Export] public TileLayer Layer { get; set; }
    [Export] public float BaseWeight { get; set; } = 1.0f;
    
    // WFC constraint sockets
    [Export] public SocketType North { get; set; } = SocketType.Any;
    [Export] public SocketType East { get; set; } = SocketType.Any;
    [Export] public SocketType South { get; set; } = SocketType.Any;
    [Export] public SocketType West { get; set; } = SocketType.Any;
    [Export] public SocketType Down { get; set; } = SocketType.Any;
    [Export] public SocketType Up { get; set; } = SocketType.Any;

    public SocketType[] Sockets
    {
        get { return new[] { North, East, South, West, Down, Up }; }
        set
        {
            North = value[0];
            East = value[1];
            South = value[2];
            West = value[3];
            Down = value[4];
            Up = value[5];
        }
    }

    [Export] public CardSignature Signature { get; set; }

    // Runtime properties for tileset integration
    public int AtlasId { get; set; } = -1;
    public Vector2I AtlasCoords { get; set; }
    public Vector2I[] AnimationFrames { get; set; } = Array.Empty<Vector2I>();

    // Pattern methods
    public TilePlacement? GetTileAt(Vector2I localPos)
    {
        int index = localPos.Y * Size.X + localPos.X;
        return index >= 0 && index < Tiles.Length ? Tiles[index] : null;
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

    // Helper method to initialize a 1x1 tile
    public void InitializeAsSingleTile(int sourceId, Vector2I atlasCoords, bool blocksTiles = false, bool blocksMovement = false)
    {
        Size = Vector2I.One;
        Tiles = new TilePlacement[]
        {
            new()
            {
                SourceId = sourceId,
                AtlasCoords = atlasCoords,
                BlocksTiles = blocksTiles,
                BlocksMovement = blocksMovement
            }
        };
    }
}