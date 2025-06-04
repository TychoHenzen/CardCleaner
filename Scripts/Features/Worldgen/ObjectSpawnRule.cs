using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

[Tool] 
[GlobalClass]
public partial class ObjectSpawnRule : Resource
{
    [Export] public PackedScene ObjectScene { get; set; }
    [Export] public float SpawnChance { get; set; } = 0.3f;
    [Export] public Vector2I FootprintSize { get; set; } = Vector2I.One; // Size in tiles
    [Export] public Vector2 PositionOffset { get; set; } = Vector2.Zero;
    [Export] public bool BlocksMovement { get; set; } = false;
    [Export] public ObjectLayer Layer { get; set; } = ObjectLayer.Decoration;
}