using System.Text.Json.Serialization;
using CardCleaner.Scripts.Core.Data;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Worldgen;

[Tool]
[GlobalClass]
public partial class SocketData : Resource
{
    [JsonIgnore]
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

    [JsonPropertyName("north")] [Export] public Array<string> NorthName { get; set; } = new();
    [JsonIgnore] public Array<CompatibilityTag> North { get; set; } = new();
    [JsonPropertyName("east")] [Export] public Array<string> EastName { get; set; } = new();
    [JsonIgnore] public Array<CompatibilityTag> East { get; set; } = new();
    [JsonPropertyName("south")] [Export] public Array<string> SouthName { get; set; } = new();
    [JsonIgnore] public Array<CompatibilityTag> South { get; set; } = new();
    [JsonPropertyName("west")] [Export] public Array<string> WestName { get; set; } = new();
    [JsonIgnore] public Array<CompatibilityTag> West { get; set; } = new();

    [JsonPropertyName("northEast")]
    [Export]
    public Array<string> NorthEastName { get; set; } = new();

    [JsonIgnore] public Array<CompatibilityTag> NorthEast { get; set; } = new();

    [JsonPropertyName("southEast")]
    [Export]
    public Array<string> SouthEastName { get; set; } = new();

    [JsonIgnore] public Array<CompatibilityTag> SouthEast { get; set; } = new();

    [JsonPropertyName("southWest")]
    [Export]
    public Array<string> SouthWestName { get; set; } = new();

    [JsonIgnore] public Array<CompatibilityTag> SouthWest { get; set; } = new();

    [JsonPropertyName("northWest")]
    [Export]
    public Array<string> NorthWestName { get; set; } = new();

    [JsonIgnore] public Array<CompatibilityTag> NorthWest { get; set; } = new();
    [JsonPropertyName("up")] [Export] public Array<string> UpName { get; set; } = new();
    [JsonIgnore] public Array<CompatibilityTag> Up { get; set; } = new();
    [JsonPropertyName("down")] [Export] public Array<string> DownName { get; set; } = new();
    [JsonIgnore] public Array<CompatibilityTag> Down { get; set; } = new();
}