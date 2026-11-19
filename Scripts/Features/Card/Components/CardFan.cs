using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

/// <summary>
///     A fan of cards held up in front of the camera, in the frame of the node the cards hang under: its middle at
///     <paramref name="Center" />, each card <paramref name="Radius" /> from the point the fan turns about (straight
///     below the middle card), the outermost cards <paramref name="SpreadDegrees" /> apart once it holds
///     <paramref name="FillCount" /> cards (see <see cref="HandFan" />), and each next card
///     <paramref name="DepthStep" /> nearer the camera, so the cards never sink into each other.
/// </summary>
public readonly record struct CardFan(Vector3 Center, float Radius, float SpreadDegrees, int FillCount, float DepthStep)
{
    /// <summary>
    ///     The pose of the card at <paramref name="index" /> (0 is the leftmost) in a fan of <paramref name="count" />
    ///     cards. Each card faces the camera, turned about the point <see cref="Radius" /> below
    ///     <see cref="Center" /> by its <see cref="HandFan" /> angle, so its top leans out; each next card lies
    ///     <see cref="DepthStep" /> nearer the camera, on top of the one before.
    /// </summary>
    public Transform3D Pose(int index, int count)
    {
        var angle = Mathf.DegToRad(HandFan.AngleDegrees(index, count, SpreadDegrees, FillCount));
        var pivot = Center + Vector3.Down * Radius;
        var position = pivot + new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f) * Radius
                       + Vector3.Back * (index * DepthStep);

        // Standing the card up (90 degrees about X) shows its face to the camera with its top up; the roll about the
        // view axis then leans a card right of the middle clockwise.
        var basis = new Basis(Vector3.Back, -angle) * FacingCamera;
        return new Transform3D(basis, position);
    }

    /// <summary>
    ///     The turn that stands a card (or a booster, whose front is also its +Y face) up facing the camera.
    /// </summary>
    public static readonly Basis FacingCamera = new(Vector3.Right, Mathf.Pi / 2f);
}
