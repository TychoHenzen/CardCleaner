using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Where a view ray meets the shop floor (y 0), used to aim a piece of furniture. A ray that does not point
///     down, or that meets the floor farther than <see cref="MaxReach" /> away, meets nothing.
/// </summary>
public static class FurnitureAim
{
    /// <summary>Farthest floor point the aim reaches, in metres from the ray origin.</summary>
    public const float MaxReach = 8f;

    private const float MinimumDownward = 0.001f;

    /// <summary>The floor point the ray meets. <paramref name="direction" /> is assumed normalised.</summary>
    public static bool TryFloorPoint(Vector3 origin, Vector3 direction, out float x, out float z)
    {
        x = 0f;
        z = 0f;
        if (direction.Y > -MinimumDownward)
        {
            return false;
        }

        float t = -origin.Y / direction.Y;
        if (t < 0f || t > MaxReach)
        {
            return false;
        }

        x = origin.X + direction.X * t;
        z = origin.Z + direction.Z * t;
        return true;
    }
}
