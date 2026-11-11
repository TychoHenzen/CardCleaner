using System;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Shared navigation test helpers for the shop scene: waiting for a bake to reach the server, and adding or removing
/// a solid box across a street-wall door. The street doors are at z=12: the front door (centre x=6.05) and the road
/// door (centre x=18.05).
/// </summary>
public static class ShopNavigationRig
{
    public const float StreetDoorZ = 12f;
    public const float FrontDoorX = 6.05f;
    public const float RoadDoorX = 18.05f;

    /// <summary>
    /// Waits for the server to apply a bake: the region's iteration id leaves <paramref name="regionBefore" />, and
    /// the map id has moved past its value on the last frame the region was unchanged. The map builds synchronously
    /// (<see cref="NavigationMapSyncSettingTest" />), so a map build in the sync that applies the region includes it;
    /// with async builds a stale build finishing in that sync looked the same and the wait could not end.
    /// The wait is bounded by wall-clock time through BoundedWait; a timeout throws InvalidOperationException whose
    /// message starts with 'navigation did not sync'.
    /// </summary>
    public static async Task WaitForBakeSync(
        ShopNavigationRegion region,
        uint regionBefore,
        double maxSeconds = BoundedWait.DefaultMaxSeconds)
    {
        var map = region.GetNavigationMap();
        var rid = region.GetRid();
        if (NavigationServer3D.MapGetUseAsyncIterations(map))
        {
            throw new InvalidOperationException(
                "the shop navigation map builds asynchronously, so a bake cannot be awaited "
                + "(navigation/world/map_use_async_iterations must be false)");
        }

        // Null until a tick shows the region unchanged: a region already changed on entry was applied by an earlier
        // sync, whose synchronous map build already holds it. Both ids are read on the main thread between syncs, so
        // several physics frames per tick still leave the map id different from the value kept here.
        uint? mapBeforeRegion = null;
        var timeout = await BoundedWait.Until(
            () =>
            {
                uint mapNow = NavigationServer3D.MapGetIterationId(map);
                if (NavigationServer3D.RegionGetIterationId(rid) == regionBefore)
                {
                    mapBeforeRegion = mapNow;
                    return false;
                }

                return mapBeforeRegion is null || mapNow != mapBeforeRegion;
            },
            "the navigation map to apply a bake",
            maxSeconds);
        if (timeout is not null)
        {
            throw new InvalidOperationException(
                $"navigation did not sync: {timeout} Region id now {NavigationServer3D.RegionGetIterationId(rid)} "
                + $"(before {regionBefore}), map id now {NavigationServer3D.MapGetIterationId(map)} "
                + $"(before the region changed {mapBeforeRegion?.ToString() ?? "unset"}).");
        }
    }

    /// <summary>A solid box across a street-wall door opening (3 m wide, 2 m high, 0.5 m deep at z=12).</summary>
    public static StaticBody3D AddDoorBlocker(Node3D shop, string name, float doorX)
    {
        var blocker = new StaticBody3D { Name = name, Position = new Vector3(doorX, 1f, StreetDoorZ) };
        blocker.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(3f, 2f, 0.5f) } });
        shop.GetNode("World/Walls").AddChild(blocker);
        return blocker;
    }

    /// <summary>Removes and frees at once (not QueueFree), so the next bake cannot still see the blocker.</summary>
    public static void RemoveBlocker(StaticBody3D blocker)
    {
        blocker.GetParent().RemoveChild(blocker);
        blocker.Free();
    }
}
