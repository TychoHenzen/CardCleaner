using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Navigation-level reachability of the shop. The street doors are at z=12: the front door (centre x=6.05) and the
/// road door (centre x=18.05). The road door opens into RightStorage, which joins Backoffice and Storage, which reach
/// the storefront through the rear-divider doors at z=0 (centre x=2.05 and x=14.05). So blocking the front door only
/// reroutes the customers; both street doors must be blocked to cut the shop off from the street. A path to an
/// unreachable target ends at the closest reachable point, so "cut" means "ends far from the target".
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopNavigationSceneTest
{
    private const float PathTolerance = 0.5f;
    private const float DoorPassDistance = 1.0f;
    private const string RegionPath = "World/Navigation";
    private const float StreetDoorZ = 12f;
    private const float RearDividerZ = 0f;
    private const float FrontDoorX = 6.05f;
    private const float RoadDoorX = 18.05f;
    private const float RearDoorWestX = 2.05f;
    private const float RearDoorEastX = 14.05f;
    private const float AgentStep = 0.1f;
    private const int AgentMaxFrames = 600;
    private const int SyncMaxFrames = 60;

    private Node3D _shop = null!;
    private ShopNavigationRegion _region = null!;
    private Rid _map;
    private Vector3 _spawn;
    private Vector3 _shelf;
    private Vector3 _despawn;
    private Vector3 _delivery;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _region = _shop.GetNode<ShopNavigationRegion>(RegionPath);

        // The first bake runs inside _Ready during AddNode, so read the region's iteration id before adding the scene.
        uint regionBefore = NavigationServer3D.RegionGetIterationId(_region.GetRid());
        AddNode(_shop);
        _map = _region.GetNavigationMap();
        await WaitForBakeSync(regionBefore);

        _spawn = Marker("CustomerSpawn");
        _shelf = Marker("ShelfSlotsArea");
        _despawn = Marker("CustomerDespawn");
        _delivery = Marker("DeliveryPoint");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void BakesMeshWithPolygons()
    {
        AssertBool(_region.NavigationMesh!.GetPolygonCount() > 0).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void BakeCellSizeMatchesTheNavigationMap()
    {
        AssertThat(_region.NavigationMesh!.CellSize).IsEqual(NavigationServer3D.MapGetCellSize(_map));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CommittedSceneStoresNoBakedMesh()
    {
        // Never added to the tree, so no _Ready ran: this is the sub-resource exactly as the scene file stores it.
        var fresh = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        var stored = fresh.GetNode<NavigationRegion3D>(RegionPath).NavigationMesh!;

        AssertInt(stored.GetPolygonCount()).IsEqual(0);
        AssertInt(stored.Vertices.Length).IsEqual(0);
        fresh.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void StreetToStorefrontRoutesThroughTheFrontDoor()
    {
        AssertBool(_region.Reaches(_spawn, _shelf)).IsTrue();

        var route = Route(_spawn, _shelf);
        AssertBool(CrossesNear(route, StreetDoorZ, FrontDoorX)).IsTrue();
        AssertBool(CrossesNear(route, StreetDoorZ, RoadDoorX)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ShelfToDespawnHasAPath()
    {
        AssertBool(_region.Reaches(_shelf, _despawn)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task DoorBlockersRerouteThenCutThenRestoreTheRoute()
    {
        // (a) Baseline on this scene instance: the route to the shelf exists and uses the front door.
        AssertBool(_region.Reaches(_spawn, _shelf)).IsTrue();
        AssertBool(CrossesNear(Route(_spawn, _shelf), StreetDoorZ, FrontDoorX)).IsTrue();

        // (b) Front door blocked: the route moves to the road door and in through the backoffice.
        var frontBlocker = AddDoorBlocker("FrontDoorBlocker", FrontDoorX);
        await RebakeAndSync();
        var detour = Route(_spawn, _shelf);
        AssertBool(_region.Reaches(_spawn, _shelf)).IsTrue();
        AssertBool(CrossesNear(detour, StreetDoorZ, RoadDoorX)).IsTrue();
        AssertBool(CrossesNear(detour, StreetDoorZ, FrontDoorX)).IsFalse();
        AssertBool(CrossesNear(detour, RearDividerZ, RearDoorEastX)
                   || CrossesNear(detour, RearDividerZ, RearDoorWestX)).IsTrue();

        // (c) Road door blocked too: the shop is cut off from the street, but both sides stay baked.
        var roadBlocker = AddDoorBlocker("RoadDoorBlocker", RoadDoorX);
        uint regionBeforeCut = NavigationServer3D.RegionGetIterationId(_region.GetRid());
        _region.Rebake();
        // Stale until the server applies the new mesh: the old route still stands right after the call.
        AssertBool(_region.Reaches(_spawn, _shelf)).IsTrue();
        await WaitForBakeSync(regionBeforeCut);
        AssertBool(_region.Reaches(_spawn, _shelf)).IsFalse();
        AssertBool(_region.Reaches(_spawn, _delivery)).IsFalse();
        AssertBool(_region.Reaches(_spawn, _despawn)).IsTrue();
        AssertBool(_region.Reaches(_shelf, _delivery)).IsTrue();

        // (d) Both blockers removed and the same region rebaked: the front-door route is back.
        RemoveBlocker(frontBlocker);
        RemoveBlocker(roadBlocker);
        await RebakeAndSync();
        AssertThat(_shop.GetNode<ShopNavigationRegion>(RegionPath)).IsSame(_region);
        AssertBool(_region.Reaches(_spawn, _shelf)).IsTrue();
        AssertBool(CrossesNear(Route(_spawn, _shelf), StreetDoorZ, FrontDoorX)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AgentFromSpawnReachesTheShelf()
    {
        // NavigationAgent3D is a plain Node; it reads its parent's position, so the parent is the mover.
        var mover = new Node3D();
        var agent = new NavigationAgent3D
        {
            PathDesiredDistance = PathTolerance,
            TargetDesiredDistance = PathTolerance,
        };
        mover.AddChild(agent);
        _shop.AddChild(mover);
        agent.SetNavigationMap(_map);
        mover.GlobalPosition = _spawn;
        agent.TargetPosition = _shelf;

        for (int frame = 0; frame < AgentMaxFrames && !agent.IsNavigationFinished(); frame++)
        {
            await ISceneRunner.SyncPhysicsFrame;
            var next = agent.GetNextPathPosition();
            mover.GlobalPosition = mover.GlobalPosition.MoveToward(next, AgentStep);
        }

        AssertBool(HorizontalDistance(mover.GlobalPosition, _shelf) <= PathTolerance).IsTrue();
    }

    private Vector3 Marker(string name) => _shop.GetNode<Marker3D>($"World/Markers/{name}").GlobalPosition;

    private Vector3[] Route(Vector3 from, Vector3 to) => NavigationServer3D.MapGetPath(_map, from, to, true);

    /// <summary>XZ distance: the baked surface is at y 0.5 and the markers are at y 0.</summary>
    private static float HorizontalDistance(Vector3 a, Vector3 b) =>
        new Vector2(a.X, a.Z).DistanceTo(new Vector2(b.X, b.Z));

    private async Task RebakeAndSync()
    {
        uint regionBefore = NavigationServer3D.RegionGetIterationId(_region.GetRid());
        _region.Rebake();
        await WaitForBakeSync(regionBefore);
    }

    /// <summary>
    /// Waits for the server to apply a bake: first the region's iteration id leaves <paramref name="regionBefore" />
    /// (the map id seen on that frame is kept), then the map id passes the kept value on a later frame. The map id
    /// alone is not enough: it can move on the frame the region changes while the map still holds no polygons.
    /// </summary>
    private async Task WaitForBakeSync(uint regionBefore)
    {
        var region = _region.GetRid();
        bool regionChanged = false;
        uint mapAtRegion = 0;
        for (int frame = 0; frame < SyncMaxFrames; frame++)
        {
            uint mapNow = NavigationServer3D.MapGetIterationId(_map);
            if (regionChanged && mapNow > mapAtRegion)
            {
                return;
            }

            if (!regionChanged && NavigationServer3D.RegionGetIterationId(region) != regionBefore)
            {
                regionChanged = true;
                mapAtRegion = mapNow;
            }

            await ISceneRunner.SyncPhysicsFrame;
        }

        throw new InvalidOperationException(
            $"navigation did not sync after {SyncMaxFrames} physics frames: region id "
            + $"{NavigationServer3D.RegionGetIterationId(region)} (before {regionBefore}), map id "
            + $"{NavigationServer3D.MapGetIterationId(_map)} (at region change {mapAtRegion})");
    }

    /// <summary>
    /// X of every point where the path crosses the plane at the given z (a segment end on it counts).
    /// </summary>
    private static List<float> CrossingXs(Vector3[] path, float z)
    {
        var xs = new List<float>();
        for (int i = 1; i < path.Length; i++)
        {
            var a = path[i - 1];
            var b = path[i];
            if ((a.Z - z) * (b.Z - z) > 0f)
            {
                continue;
            }

            float span = b.Z - a.Z;
            float t = MathF.Abs(span) < 1e-4f ? 0f : (z - a.Z) / span;
            xs.Add(a.X + (b.X - a.X) * t);
        }

        return xs;
    }

    private static bool CrossesNear(Vector3[] path, float z, float x) =>
        CrossingXs(path, z).Any(crossing => MathF.Abs(crossing - x) <= DoorPassDistance);

    /// <summary>A solid box across a street-wall door opening (3 m wide, 2 m high, 0.5 m deep at z=12).</summary>
    private StaticBody3D AddDoorBlocker(string name, float doorX)
    {
        var blocker = new StaticBody3D { Name = name, Position = new Vector3(doorX, 1f, StreetDoorZ) };
        blocker.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(3f, 2f, 0.5f) } });
        _shop.GetNode("World/Walls").AddChild(blocker);
        return blocker;
    }

    /// <summary>Removes and frees at once (not QueueFree), so the next bake cannot still see the blocker.</summary>
    private static void RemoveBlocker(StaticBody3D blocker)
    {
        blocker.GetParent().RemoveChild(blocker);
        blocker.Free();
    }
}
