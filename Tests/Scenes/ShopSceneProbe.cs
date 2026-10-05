using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Physics queries against an instantiated shop scene: floor support and capsule clearance
/// along hand-listed routes between the three areas.
/// </summary>
public sealed class ShopSceneProbe(Node3D shop, CapsuleShape3D playerShape)
{
    public const string ScenePath = "res://Scenes/Gameplay/ShopScene.tscn";
    public const float FloorClearance = 0.01f;

    private const float WalkSampleStep = 0.1f;

    public static readonly Vector2[] StorefrontToStorage =
        [new(6f, 9f), new(2.05f, 2f), new(2.05f, -2f), new(4f, -4f)];

    public static readonly Vector2[] StorageToBackoffice =
        [new(4f, -4f), new(7f, -2.05f), new(9f, -2.05f), new(12f, -4f)];

    public static readonly Vector2[] BackofficeToStorefront =
        [new(12f, -4f), new(14.05f, -2f), new(14.05f, 2f), new(13f, 8f), new(6f, 9f)];

    public float CapsuleCenterHeight => playerShape.Height / 2f + FloorClearance;

    public bool CanWalk(Vector2[] route)
    {
        for (int leg = 1; leg < route.Length; leg++)
        {
            var from = route[leg - 1];
            var to = route[leg];
            int steps = Mathf.CeilToInt(from.DistanceTo(to) / WalkSampleStep);

            for (int step = 0; step <= steps; step++)
            {
                var point = from.Lerp(to, step / (float)steps);
                var feet = new Vector3(point.X, 0f, point.Y);
                if (!SupportedByFloor(feet) || !CapsuleFits(feet with { Y = CapsuleCenterHeight }))
                    return false;
            }
        }

        return true;
    }

    public bool SupportedByFloor(Vector3 feet)
    {
        var query = PhysicsRayQueryParameters3D.Create(feet + Vector3.Up * 0.5f, feet + Vector3.Down * 1f);
        return shop.GetWorld3D().DirectSpaceState.IntersectRay(query).Count > 0;
    }

    public bool CapsuleFits(Vector3 center)
    {
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = playerShape,
            Transform = new Transform3D(Basis.Identity, center)
        };
        return shop.GetWorld3D().DirectSpaceState.IntersectShape(query, 1).Count == 0;
    }
}
