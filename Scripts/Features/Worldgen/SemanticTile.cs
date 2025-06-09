using System;
using System.Linq;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Enum;
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
    [Export] public TilePlacement[] Tiles { get; set; } = Array.Empty<TilePlacement>();
    
    // Layered spawning system
    [Export] public float GlobalSpawnChance { get; set; } = 0.0f;
    [Export] public TileLayer Layer { get; set; }
    [Export] public float BaseWeight { get; set; } = 1.0f;
    [Export] public CardSignature Signature { get; set; }
    [Export]
    public Array<CompatibilityTag> North { get; set; } = new();
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
    /// Constraint modifications this tile applies to other layers when placed
    /// </summary>
    [Export] public Dictionary<Direction, SocketType> LayerConstraints { get; set; } = new();
    /// <summary>
    /// Check if this tile modifies constraints on other layers
    /// </summary>
    public bool HasLayerConstraints => LayerConstraints.Count > 0;
    

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
    public Array<CompatibilityTag>[] Sockets
    {
        get { return new Array<CompatibilityTag>[] { North, East, South, West, NorthEast, SouthEast, SouthWest, NorthWest, Down, Up }; }
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