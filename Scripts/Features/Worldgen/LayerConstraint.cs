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

    // Default values as constants
    private const TileLayer DefaultTargetLayer = TileLayer.Terrain;
    private const Direction DefaultAffectedSocket = Direction.North;
    private const Operation DefaultOperation = Operation.Add;
    private const string DefaultTagName = "";

    [Export] public TileLayer targetLayer { get; set; } = DefaultTargetLayer;
    [Export] public Direction AffectedSocket { get; set; } = DefaultAffectedSocket;
    [Export] public Operation operation { get; set; } = DefaultOperation;
    [JsonIgnore] public CompatibilityTag tag { get; set; } = null!;
    [Export] public string tagName { get; set; } = DefaultTagName;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(targetLayer) => true,
            nameof(AffectedSocket) => true,
            nameof(operation) => true,
            nameof(tagName) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(targetLayer) => (int)DefaultTargetLayer,
            nameof(AffectedSocket) => (int)DefaultAffectedSocket,
            nameof(operation) => (int)DefaultOperation,
            nameof(tagName) => DefaultTagName,
            _ => base._PropertyGetRevert(property)
        };
    }
}