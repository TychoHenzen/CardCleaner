using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Drives the production player of the shop scene through PlayerController's own movement loop
/// (input actions, MoveAndSlide, gravity) from the spawn through all three areas and back.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopScenePlayerWalkTest
{
    private const float FallLimitY = -1f;

    private Node3D _shop = null!;
    private CharacterBody3D _player = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _player = _shop.GetNode<CharacterBody3D>("Player");

        // The test scene is not the current scene, so hand the scene-owned settings to the player directly.
        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton<IGameSettings>(_shop.GetNode<GameSettings>("Services/GameSettings"));

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public static void ReleaseInput()
    {
        Input.ActionRelease("ui_up");
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PlayerSettlesOnTheFloorAtSpawn()
    {
        for (int frame = 0; frame < 30; frame++)
            await ISceneRunner.SyncPhysicsFrame;

        AssertBool(_player.IsOnFloor()).IsTrue();
        AssertBool(_player.GlobalPosition.Y > FallLimitY).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PlayerWalksStorefrontStorageBackofficeAndBack()
    {
        var route = ShopSceneProbe.StorefrontToStorage
            .Skip(1)
            .Concat(ShopSceneProbe.StorageToBackoffice.Skip(1))
            .Concat(ShopSceneProbe.BackofficeToStorefront.Skip(1));

        foreach (var waypoint in route)
        {
            var walk = await PlayerWalker.WalkTo(_player, waypoint);

            AssertBool(walk.Arrived).IsTrue();
            AssertBool(_player.GlobalPosition.Y > FallLimitY).IsTrue();
        }

        AssertBool(_player.IsOnFloor()).IsTrue();
    }
}
