
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Enum;
using Godot;

[Tool]
[GlobalClass]
public partial class LayerConstraint : Resource
{
    public enum Operation
    {
        Add,
        Remove
    }
    [Export] public TileLayer targetLayer { get; set; }= TileLayer.Terrain;
    [Export] public Direction AffectedSocket { get; set; }
    [Export] public Operation operation { get; set; }= Operation.Add;
    [Export] public CompatibilityTag tag { get; set; }
}