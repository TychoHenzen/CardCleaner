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
    [Export] public CardSignature Signature { get; set; }
    // WFC constraint sockets - now includes diagonals
    [ExportSubgroup("North socket")]
    [Export]
    public SocketDescriptor North { get; set; } = new();
    [ExportSubgroup("East socket")]
    [Export] public SocketDescriptor East { get; set; } = new();
    [ExportSubgroup("South socket")]
    [Export] public SocketDescriptor South { get; set; } = new();
    [ExportSubgroup("West socket")]
    [Export] public SocketDescriptor West { get; set; } = new();
    [ExportSubgroup("North-East socket")]
    [Export] public SocketDescriptor NorthEast { get; set; } = new();
    [ExportSubgroup("South-East socket")]
    [Export] public SocketDescriptor SouthEast { get; set; } = new();
    [ExportSubgroup("South-West socket")]
    [Export] public SocketDescriptor SouthWest { get; set; } = new();
    [ExportSubgroup("North-West socket")]
    [Export] public SocketDescriptor NorthWest { get; set; } = new();
    [ExportSubgroup("Down socket")]
    [Export] public SocketDescriptor Down { get; set; } = new();
    /// <summary>
    /// Constraint modifications this tile applies to other layers when placed
    /// </summary>
    [Export] public Godot.Collections.Dictionary<Direction, SocketType> LayerConstraints { get; set; } = new();
    /// <summary>
    /// Check if this tile modifies constraints on other layers
    /// </summary>
    public bool HasLayerConstraints => LayerConstraints.Count > 0;
    

    [ExportSubgroup("Up socket")] [Export] public SocketDescriptor Up { get; set; } = new();
    /// <summary>
    /// Get constraint modifications for a specific layer and direction
    /// </summary>
    public SocketType? GetLayerConstraint(Direction direction)
    {
        return LayerConstraints.TryGetValue(direction, out var constraint) ? constraint : null;
    }

    /// <summary>
    /// Set a constraint modification for a specific layer and direction
    /// </summary>
    public void SetLayerConstraint(Direction direction, SocketType constraint)
    {
        LayerConstraints[direction] = constraint;
    }
    public SocketDescriptor[] Sockets
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

        return ourSocket.IsCompatibleWith(theirSocket);
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