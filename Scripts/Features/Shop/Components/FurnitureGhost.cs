using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     The translucent box that previews a furniture placement. It is green where the piece may go and red where it
///     may not. It has no collision and joins no group, so it never blocks or is blocked.
/// </summary>
public partial class FurnitureGhost : Node3D
{
    private static readonly Color Valid = new(0.2f, 0.9f, 0.3f, 0.45f);
    private static readonly Color Invalid = new(0.9f, 0.2f, 0.2f, 0.45f);

    private MeshInstance3D? _body;
    private StandardMaterial3D? _material;

    /// <summary>Sizes the box to the footprint and height; calling it again resizes the same box.</summary>
    public void Configure(FurnitureFootprint footprint, float height)
    {
        if (_body == null)
        {
            _material = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
            _body = new MeshInstance3D { MaterialOverride = _material };
            AddChild(_body);
        }

        _body!.Mesh = new BoxMesh { Size = new Vector3(footprint.Width, height, footprint.Depth) };
        _body.Position = new Vector3(0f, height / 2f, 0f);
    }

    /// <summary>Moves the ghost to the pose and tints it by validity.</summary>
    public void Show(FurniturePose pose, bool valid)
    {
        Position = new Vector3(pose.X, 0f, pose.Z);
        Rotation = new Vector3(0f, pose.QuarterTurns * Mathf.Pi / 2f, 0f);
        Visible = true;
        if (_material != null)
        {
            _material.AlbedoColor = valid ? Valid : Invalid;
        }
    }

    /// <summary>The albedo colour the ghost is showing.</summary>
    public Color Tint => _material?.AlbedoColor ?? default;
}
