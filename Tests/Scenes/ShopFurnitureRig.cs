using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     The shelf rect and the route the furniture scene tests check against, shared by the placement, move and save
///     cases. The shelf is the unrotated pose at centre (4, 8), a spot clear of solid shop furniture such as the
///     checkout counter.
/// </summary>
internal static class ShopFurnitureRig
{
    public const float ShelfMinX = 2.935f;
    public const float ShelfMaxX = 5.065f;
    public const float ShelfMinZ = 7.5f;
    public const float ShelfMaxZ = 8.5f;
    public const int RouteSamplesPerSegment = 20;

    public static readonly Vector3 RouteA = new(4f, 0f, 6f);
    public static readonly Vector3 RouteB = new(4f, 0f, 10f);

    /// <summary>True when any sampled point of the route lies inside the shelf rect (XZ plane).</summary>
    public static bool CrossesShelf(Vector3[] route)
    {
        for (int i = 0; i + 1 < route.Length; i++)
        {
            for (int s = 0; s <= RouteSamplesPerSegment; s++)
            {
                var p = route[i].Lerp(route[i + 1], s / (float)RouteSamplesPerSegment);
                if (p.X >= ShelfMinX && p.X <= ShelfMaxX && p.Z >= ShelfMinZ && p.Z <= ShelfMaxZ)
                    return true;
            }
        }

        return false;
    }
}
