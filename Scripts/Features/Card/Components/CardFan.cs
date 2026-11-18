using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

/// <summary>
///     A fan of cards held up in front of the camera, in the frame of the node the cards hang under.
/// </summary>
/// <param name="Center">Where the middle of the fan sits.</param>
/// <param name="Radius">
///     Distance from the point the fan turns about (straight below the middle card) to each card's centre.
/// </param>
/// <param name="SpreadDegrees">
///     Angle between the outermost cards once the fan holds <paramref name="FillCount" /> cards or more.
/// </param>
/// <param name="FillCount">How many cards it takes to fill the spread (see <see cref="HandFan" />).</param>
/// <param name="DepthStep">
///     How much nearer the camera each next card lies, so the cards never sink into each other.
/// </param>
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
