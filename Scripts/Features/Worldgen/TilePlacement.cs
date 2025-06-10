using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;
using Godot.Collections;

[Tool]
[GlobalClass]
public partial class TilePlacement : Resource
{
    [Export] public bool BlocksMovement { get; set; }
    [Export] public bool BlocksTiles { get; set; }

    // For animated tiles
    [Export] public Array<Vector3I> AnimationFrames { get; set; } = new();
    [Export] public float AnimationSpeed { get; set; } = 1.0f;

    public TilePlacement WithOffset(Vector2I offset)
    {
        return new TilePlacement
        {
            BlocksMovement = BlocksMovement,
            BlocksTiles = BlocksTiles,
            AnimationFrames =
                new Array<Vector3I>(AnimationFrames.Select(i => new Vector3I(i.X , i.Y + offset.X, i.Z+ offset.Y))
                    .ToArray()),
            AnimationSpeed = AnimationSpeed,
        };
    }
}