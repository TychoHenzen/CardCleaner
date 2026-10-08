using System.Threading.Tasks;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Geometry-level walkability of the shop: a player capsule swept along routes must fit and stay
/// supported inside the shop and be stopped by solid walls and the street boundary.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopSceneWalkTest
{
    private Node3D _shop = null!;
    private ShopSceneProbe _probe = null!;
    private Vector3 _spawn;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();

        // The sweeps move a capsule through the level, so the live body must not be in the way.
        var player = ShopScenePlayer.Sideline(_shop);
        _spawn = player.Position;
        var shape = (CapsuleShape3D)player.GetNode<CollisionShape3D>("CollisionShape3D").Shape;
        _probe = new ShopSceneProbe(_shop, shape);

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CapsuleCanSweepEveryRouteBetweenTheAreas()
    {
        AssertBool(_probe.CanWalk(ShopSceneProbe.StorefrontToStorage)).IsTrue();
        AssertBool(_probe.CanWalk(ShopSceneProbe.StorageToBackoffice)).IsTrue();
        AssertBool(_probe.CanWalk(ShopSceneProbe.BackofficeToStorefront)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void StorefrontEntranceIsOpen()
    {
        AssertBool(_probe.CanWalk([new Vector2(_spawn.X, _spawn.Z), new Vector2(6.05f, 11.5f)])).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void StreetOutsideTheEntranceIsFlooredAndBounded()
    {
        AssertBool(_probe.CanWalk([new Vector2(6.05f, 9f), new Vector2(6.05f, 15.2f)])).IsTrue();
        AssertBool(_probe.CanWalk([new Vector2(6.05f, 15.2f), new Vector2(6.05f, 17f)])).IsFalse();
        AssertBool(_probe.CanWalk([new Vector2(6.05f, 13f), new Vector2(-1f, 13f)])).IsFalse();
        AssertBool(_probe.CanWalk([new Vector2(6.05f, 13f), new Vector2(17f, 13f)])).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AreasAreNotReachableThroughSolidWalls()
    {
        AssertBool(_probe.CanWalk([new Vector2(6f, 4f), new Vector2(6f, -4f)])).IsFalse();
        AssertBool(_probe.CanWalk([new Vector2(4f, -4f), new Vector2(12f, -6f)])).IsFalse();
    }
}
