using System;
using System.Linq;
using System.Text.Json.Serialization;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Worldgen;

[Tool]
[GlobalClass]
public partial class SemanticTile : Resource
{
    // Default values as constants
    private const string DefaultTileName = "";
    private const TilePassability DefaultPassability = TilePassability.Passable;
    private const string DefaultTileSetPath = "";
    private const float DefaultGlobalSpawnChance = 0.0f;
    private const TileLayer DefaultLayer = TileLayer.Terrain;
    private const float DefaultBaseWeight = 1.0f;
    private static readonly Vector2I DefaultSize = Vector2I.One;
    private static readonly CardSignature DefaultSignature = new();

    [Export] public string TileName { get; set; } = DefaultTileName;
    [Export] public TilePassability Passability { get; set; } = DefaultPassability;

    // Pattern properties (every tile is now a pattern)

    [JsonPropertyName("tileSetPath")] public string TileSetPath { get; set; } = DefaultTileSetPath;
    [Export] public TileSet? TileSet { get; set; }
    [Export] public Vector2I Size { get; set; } = DefaultSize;
    [Export] public TilePlacement Tile { get; set; } = new();

    // Layered spawning system
    [Export] public float GlobalSpawnChance { get; set; } = DefaultGlobalSpawnChance;
    [Export] public TileLayer Layer { get; set; } = DefaultLayer;
    [Export] public float BaseWeight { get; set; } = DefaultBaseWeight;
    [Export] public CardSignature Signature { get; set; } = new();

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(TileName) => true,
            nameof(Passability) => true,
            nameof(TileSetPath) => true,
            nameof(Size) => true,
            nameof(GlobalSpawnChance) => true,
            nameof(Layer) => true,
            nameof(BaseWeight) => true,
            nameof(Signature) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(TileName) => DefaultTileName,
            nameof(Passability) => (int)DefaultPassability,
            nameof(TileSetPath) => DefaultTileSetPath,
            nameof(Size) => Variant.From(DefaultSize),
            nameof(GlobalSpawnChance) => DefaultGlobalSpawnChance,
            nameof(Layer) => (int)DefaultLayer,
            nameof(BaseWeight) => DefaultBaseWeight,
            nameof(Signature) => Variant.From(DefaultSignature),
            _ => base._PropertyGetRevert(property)
        };
    }

    /// <summary>
    /// Constraint modifications this tile applies to other layers when placed
    /// </summary>
    [Export]
    public Array<LayerConstraint> LayerConstraints { get; set; } = new();


    [JsonPropertyName("sockets")] [Export] public SocketData SocketData { get; set; } = new();

    /// <summary>
    /// Check if this tile modifies constraints on other layers
    /// </summary>
    public bool HasLayerConstraints => LayerConstraints.Count > 0;

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

        var ourSocket = SocketData.Sockets[(int)direction];
        var oppositeDirection = GetOppositeDirection(direction);
        var theirSocket = other.SocketData.Sockets[(int)oppositeDirection];

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
            Direction.Up => Direction.Down,
            Direction.Down => Direction.Up,
            _ => throw new ArgumentException("Invalid direction")
        };
    }
}
