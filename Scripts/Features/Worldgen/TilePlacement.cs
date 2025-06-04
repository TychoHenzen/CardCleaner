using Godot;
using Godot.Collections;

[Tool]
[GlobalClass]
public partial class TilePlacement : Resource
{
    [Export] public int SourceId { get; set; } = 0;
    [Export] public Vector2I AtlasCoords { get; set; }
    [Export] public bool BlocksMovement { get; set; } = false;
    [Export] public bool BlocksTiles { get; set; } = false;
    
    // For animated tiles
    [Export] public Array<Vector2I> AnimationFrames { get; set; } = new();
    [Export] public float AnimationSpeed { get; set; } = 1.0f;
}