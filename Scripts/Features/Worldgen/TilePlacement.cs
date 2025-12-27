using System.Linq;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Worldgen;

[Tool]
[GlobalClass]
public partial class TilePlacement : Resource
{
    // Default values as constants
    private const bool DefaultBlocksMovement = false;
    private const bool DefaultBlocksTiles = false;
    private const float DefaultAnimationSpeed = 1.0f;

    [Export] public bool BlocksMovement { get; set; } = DefaultBlocksMovement;
    [Export] public bool BlocksTiles { get; set; } = DefaultBlocksTiles;

    // For animated tiles
    [Export] public Array<Vector3I> AnimationFrames { get; set; } = new();
    [Export] public float AnimationSpeed { get; set; } = DefaultAnimationSpeed;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(BlocksMovement) => true,
            nameof(BlocksTiles) => true,
            nameof(AnimationSpeed) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(BlocksMovement) => DefaultBlocksMovement,
            nameof(BlocksTiles) => DefaultBlocksTiles,
            nameof(AnimationSpeed) => DefaultAnimationSpeed,
            _ => base._PropertyGetRevert(property)
        };
    }

    public TilePlacement WithOffset(Vector2I offset)
    {
        return new TilePlacement
        {
            BlocksMovement = BlocksMovement,
            BlocksTiles = BlocksTiles,
            AnimationFrames =
                new Array<Vector3I>(AnimationFrames.Select(i => new Vector3I(i.X, i.Y + offset.X, i.Z + offset.Y))
                    .ToArray()),
            AnimationSpeed = AnimationSpeed
        };
    }
}