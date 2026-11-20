using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Components;
using Godot;

namespace CardCleaner.Scripts.Features.Hand.Components;

/// <summary>
///     The default layout of the hand (the reset values of <see cref="Hand" />'s exports) and the poses it gives its
///     items, in the hand anchor's frame.
/// </summary>
internal static class HandLayout
{
    /// <summary>The layer a card gets back when it leaves the hand.</summary>
    public const uint CardCollisionLayer = 2;

    /// <summary>How far in front of the hand anchor the cards lie while a drop is being aimed.</summary>
    public const float HoldDistance = 1f;

    /// <summary>Distance from the point the fan turns about (straight below the middle item) to each item.</summary>
    public const float FanRadius = 0.8f;

    /// <summary>Angle between the outermost items once the hand holds <see cref="FanFillCount" /> or more.</summary>
    public const float FanSpreadDegrees = 50f;

    /// <summary>How many items it takes to fill the spread (see <see cref="Card.Models.HandFan" />).</summary>
    public const int FanFillCount = 10;

    /// <summary>How much nearer the camera each next item lies, so they never sink into each other.</summary>
    public const float FanDepthStep = 0.006f;

    /// <summary>How far the front item stands out of the fan, along its own lean.</summary>
    public const float FrontRaise = 0.1f;

    /// <summary>How far in front of the camera Q sets the front item down.</summary>
    public const float PutDownDistance = 1.5f;

    /// <summary>Where the middle of the held fan sits, in the hand anchor's frame.</summary>
    public static readonly Vector3 FanCenter = new(0.5f, -0.55f, -1.4f);

    /// <summary>Where a booster box or a cardboard box is carried, level, in the hand anchor's frame.</summary>
    public static readonly Vector3 BoxHoldOffset = new(0.5f, -0.4f, -1.2f);

    private static readonly Dictionary<string, Variant> Defaults = new()
    {
        [nameof(Hand.CardCollisionLayerExport)] = CardCollisionLayer,
        [nameof(Hand.HoldDistance)] = HoldDistance,
        [nameof(Hand.FanCenter)] = FanCenter,
        [nameof(Hand.FanRadius)] = FanRadius,
        [nameof(Hand.FanSpreadDegrees)] = FanSpreadDegrees,
        [nameof(Hand.FanFillCount)] = FanFillCount,
        [nameof(Hand.FanDepthStep)] = FanDepthStep,
        [nameof(Hand.FrontRaise)] = FrontRaise,
        [nameof(Hand.PutDownDistance)] = PutDownDistance,
        [nameof(Hand.BoxHoldOffset)] = BoxHoldOffset,
    };

    /// <summary>The reset value of one of <see cref="Hand" />'s layout exports; false for any other property.</summary>
    public static bool TryDefault(StringName property, out Variant value) =>
        Defaults.TryGetValue(property.ToString(), out value);

    /// <summary>
    ///     The pose of item <paramref name="index" /> of <paramref name="count" /> in the fan. The front item stands
    ///     out along its own lean (the fan's radius through it) and a little nearer the camera, so it shows over the
    ///     others.
    /// </summary>
    public static Transform3D FanPose(CardFan fan, int index, int count, bool front, float raise, float depthStep)
    {
        var pose = fan.Pose(index, count);
        if (!front)
            return pose;

        var lean = (pose.Basis * CardFan.FacingCamera.Inverse()) * Vector3.Up;
        return new Transform3D(pose.Basis, pose.Origin + lean * raise + Vector3.Back * depthStep);
    }

    /// <summary>
    ///     Where card <paramref name="index" /> lies while a drop is aimed: <paramref name="holdDistance" /> ahead of
    ///     the eye, face turned back along the view, each card one <paramref name="thickness" /> above the one before.
    /// </summary>
    public static Transform3D DropPose(Transform3D eye, float holdDistance, int index, float thickness)
    {
        var forward = -eye.Basis.Z;
        var yaw = Mathf.Atan2(forward.X, forward.Z);
        var faceYaw = Basis.Identity.Rotated(Vector3.Up, yaw + Mathf.DegToRad(180));
        return new Transform3D(faceYaw, eye.Origin + forward * holdDistance + Vector3.Up * (index * thickness));
    }
}
