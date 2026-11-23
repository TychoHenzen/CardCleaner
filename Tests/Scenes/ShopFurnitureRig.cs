using Godot;
using CardCleaner.Scripts.Features.Shop.Furniture;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     The shelf rect and the route the furniture scene tests check against, shared by the placement, move and save
///     cases. The shelf is the unrotated pose at centre (4, 8), a spot clear of solid shop furniture such as the
///     checkout counter. Its size is read from the card shelf's furniture item, so the rect follows the shelf.
/// </summary>
internal static class ShopFurnitureRig
{
    public const int RouteSamplesPerSegment = 20;
    private const string ShelfItemPath = "res://Data/Shop/Furniture/ShelfFurniture.tres";
    private static readonly Vector2 ShelfCentre = new(4f, 8f);

    public static readonly Vector3 RouteA = new(4f, 0f, 6f);
    public static readonly Vector3 RouteB = new(4f, 0f, 10f);

    /// <summary>True when any sampled point of the route lies inside the shelf rect (XZ plane).</summary>
    public static bool CrossesShelf(Vector3[] route)
    {
        var shelf = ShelfRect();
        for (int i = 0; i + 1 < route.Length; i++)
        {
            for (int s = 0; s <= RouteSamplesPerSegment; s++)
            {
                var p = route[i].Lerp(route[i + 1], s / (float)RouteSamplesPerSegment);
                if (shelf.HasPoint(new Vector2(p.X, p.Z)))
                    return true;
            }
        }

        return false;
    }

    private static Rect2 ShelfRect()
    {
        var item = GD.Load<FurnitureItem>(ShelfItemPath);
        var size = new Vector2(item.Width, item.Depth);
        return new Rect2(ShelfCentre - size / 2f, size);
    }
}
