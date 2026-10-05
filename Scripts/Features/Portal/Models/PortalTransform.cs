using Godot;

namespace CardCleaner.Scripts.Features.Portal.Models;

/// <summary>
///     Teleport math for a doorway whose two sides are a pair of markers. Only the offset from the entry
///     marker and the yaw difference between the markers are carried over, so the player keeps their
///     position relative to the doorway and walks on in the same direction relative to it. Pitch is not
///     touched: it lives on the player's head, not on the body.
/// </summary>
public static class PortalTransform
{
    /// <summary>The player's body transform on the far side of the doorway.</summary>
    public static Transform3D Map(Transform3D player, Transform3D entry, Transform3D exit)
    {
        var turn = YawBetween(entry, exit);
        var offset = turn * (player.Origin - entry.Origin);
        return new Transform3D(turn * player.Basis, exit.Origin + offset);
    }

    /// <summary>Velocity after the teleport: rotated by the same yaw as the body.</summary>
    public static Vector3 MapVelocity(Vector3 velocity, Transform3D entry, Transform3D exit)
    {
        return YawBetween(entry, exit) * velocity;
    }

    /// <summary>
    ///     True once the player has stepped into the doorway: inside its half width, no further from the
    ///     wall than <paramref name="depth" /> and not behind it, and between <paramref name="bottom" /> and
    ///     <paramref name="top" /> (local Y of the opening). The doorway faces its local +Z.
    /// </summary>
    public static bool HasCrossed(
        Transform3D doorway, Vector3 playerPosition, float halfWidth, float depth, float bottom, float top)
    {
        var local = doorway.AffineInverse() * playerPosition;
        return Mathf.Abs(local.X) <= halfWidth
               && local.Z <= depth && local.Z >= -depth
               && local.Y >= bottom && local.Y <= top;
    }

    /// <summary>
    ///     Landing spots to try, in order: the mapped spot, then the same spot raised in
    ///     <paramref name="step" /> increments. The caller keeps the first one that is clear of geometry.
    /// </summary>
    public static Vector3 Candidate(Vector3 landing, int attempt, float step)
    {
        return landing + Vector3.Up * (attempt * step);
    }

    private static Basis YawBetween(Transform3D entry, Transform3D exit)
    {
        return Basis.FromEuler(new Vector3(0f, YawOf(exit) - YawOf(entry), 0f));
    }

    private static float YawOf(Transform3D transform)
    {
        var forward = -transform.Basis.Z;
        return Mathf.Atan2(-forward.X, -forward.Z);
    }
}
