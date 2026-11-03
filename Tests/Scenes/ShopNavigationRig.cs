using System;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
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

    private const int SyncMaxFrames = 60;

    /// <summary>
    /// Waits for the server to apply a bake: first the region's iteration id leaves <paramref name="regionBefore" />
    /// (the map id seen on that frame is kept), then the map id passes the kept value on a later frame. The map id
    /// alone is not enough: it can move on the frame the region changes while the map still holds no polygons.
    /// </summary>
    public static async Task WaitForBakeSync(ShopNavigationRegion region, uint regionBefore)
    {
        var map = region.GetNavigationMap();
        var rid = region.GetRid();
        bool regionChanged = false;
        uint mapAtRegion = 0;
        for (int frame = 0; frame < SyncMaxFrames; frame++)
        {
            uint mapNow = NavigationServer3D.MapGetIterationId(map);
            if (regionChanged && mapNow > mapAtRegion)
            {
                return;
            }

            if (!regionChanged && NavigationServer3D.RegionGetIterationId(rid) != regionBefore)
            {
                regionChanged = true;
                mapAtRegion = mapNow;
            }

            await ISceneRunner.SyncPhysicsFrame;
        }

        throw new InvalidOperationException(
            $"navigation did not sync after {SyncMaxFrames} physics frames: region id "
            + $"{NavigationServer3D.RegionGetIterationId(rid)} (before {regionBefore}), map id "
            + $"{NavigationServer3D.MapGetIterationId(map)} (at region change {mapAtRegion})");
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
