using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     A moving part of a container's wrapper: a lid, a flap or a booster's tear strip. While the container opens,
///     the part turns by <see cref="OpenRotationDegrees" /> about its own origin (its hinge) and moves by
///     <see cref="OpenOffset" /> in its own frame, over the share of the unseal time from <see cref="StartAt" /> to
///     <see cref="EndAt" />. A part that <see cref="VanishWhenOpen" /> shrinks away over the end of its motion.
/// </summary>
public partial class OpeningPart : Node3D
{
    private const float SmallestScale = 0.01f;
    private const float VanishFrom = 0.6f;

    private Transform3D _rest = Transform3D.Identity;

    [Export]
    public Vector3 OpenRotationDegrees { get; set; }

    [Export]
    public Vector3 OpenOffset { get; set; }

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float StartAt { get; set; }

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float EndAt { get; set; } = 1f;

    [Export]
    public bool VanishWhenOpen { get; set; }

    public override void _Ready() => _rest = Transform;

    /// <summary>Poses the part at <paramref name="unseal" />, the container's unseal progress from 0 to 1.</summary>
    public void Pose(float unseal)
    {
        var span = Mathf.Max(EndAt - StartAt, 1e-4f);
        var own = Mathf.Clamp((unseal - StartAt) / span, 0f, 1f);
        var eased = ContainerOpeningPlan.Ease(own);
        var turn = Basis.FromEuler(OpenRotationDegrees * (Mathf.Pi / 180f) * eased);
        var basis = _rest.Basis * turn;
        if (VanishWhenOpen)
            basis = basis.Scaled(Vector3.One * Mathf.Max(SmallestScale, 1f - ContainerOpeningPlan.Ease(
                (own - VanishFrom) / (1f - VanishFrom))));

        Transform = new Transform3D(basis, _rest.Origin + _rest.Basis * (OpenOffset * eased));
    }
}
