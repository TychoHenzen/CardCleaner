using System.Text.Json.Serialization;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Enumeration;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

[Tool]
[GlobalClass]
public partial class LayerConstraint : Resource
{
    public enum Operation
    {
        Add,
        Remove
    }

    [Export] public TileLayer targetLayer { get; set; } = TileLayer.Terrain;
    [Export] public Direction AffectedSocket { get; set; }
    [Export] public Operation operation { get; set; } = Operation.Add;
    [JsonIgnore] public CompatibilityTag tag { get; set; } = null!;
    [Export] public string tagName { get; set; } = "";
}