using System.Threading.Tasks;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     The shop scene loaded for the layout suites: the packed scene, the instance with the player sidelined, the
///     sweep probe and the lookups of named areas, floors and markers.
/// </summary>
[RequireGodotRuntime]
public abstract class ShopSceneContractTestBase
{
    private protected const float RoadDoorX = 18.05f;

    private protected static readonly string[] AreaNames =
        ["Storefront", "Storage", "Backoffice", "RightStorage", "ExpansionLot"];

    private protected PackedScene _packed = null!;
    private protected Node3D _shop = null!;
    private protected ShopSceneProbe _probe = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _packed = GD.Load<PackedScene>(ShopSceneProbe.ScenePath);
        _shop = _packed.Instantiate<Node3D>();

        // The walk checks sweep the player capsule through the level, so the live body must not be in the way.
        var player = ShopScenePlayer.Sideline(_shop);
        _probe = new ShopSceneProbe(_shop, (CapsuleShape3D)player.GetNode<CollisionShape3D>("CollisionShape3D").Shape);

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    private protected Vector3 MarkerPosition(string marker) =>
        _shop.GetNode<Marker3D>($"World/Markers/{marker}").GlobalPosition;

    private protected CollisionShape3D? FloorShape(string area) =>
        _shop.GetNodeOrNull<CollisionShape3D>($"World/{area}/Floor/CollisionShape3D");

    // Whether the point lies over the floor box of the named area (an area, or "Street").
    private protected bool IsInsideArea(string area, Vector3 position)
    {
        var floor = _shop.GetNode<CollisionShape3D>($"World/{area}/Floor/CollisionShape3D");
        var half = ((BoxShape3D)floor.Shape).Size / 2f;
        var center = floor.GlobalPosition;
        return System.Math.Abs(position.X - center.X) <= half.X && System.Math.Abs(position.Z - center.Z) <= half.Z;
    }
}
