using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Tests.Scenes;

[TestSuite]
[RequireGodotRuntime]
public class ShopGrayboxSceneTest
{
    private const string ScenePath = "res://Scenes/Graybox/ShopGraybox.tscn";

    private static readonly string[] AreaNames = ["Storefront", "Storage", "Backoffice"];
    private static readonly string[] MarkerNames = ["PlayerSpawn", "PcLocation", "SeamLocation", "DeliveryPoint", "ShelfSlotsArea"];

    private Node3D _shop = null!;
    private CharacterBody3D _player = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ScenePath).Instantiate<Node3D>();
        _player = _shop.GetNode<CharacterBody3D>("Player");

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
    public void SceneHasNamedAreasAndMarkers()
    {
        foreach (var area in AreaNames)
        {
            AssertThat(_shop.GetNodeOrNull<Node3D>($"World/{area}")).IsNotNull();
            AssertThat(_shop.GetNode<Label3D>($"World/{area}/AreaLabel").Text).IsEqual(area.ToUpperInvariant());
            AssertThat(_shop.GetNode<CollisionShape3D>($"World/{area}/Floor/CollisionShape3D").Shape)
                .IsInstanceOf<BoxShape3D>();
        }

        foreach (var marker in MarkerNames)
            AssertThat(_shop.GetNodeOrNull<Marker3D>($"World/Markers/{marker}")).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PlayerWalksStorefrontStorageBackofficeAndBack()
    {
        var route = new[]
        {
            new Vector2(0, 8),
            new Vector2(8, 8),
            new Vector2(0, 8),
            new Vector2(0, 0)
        };

        foreach (var waypoint in route)
        {
            var walk = await PlayerWalker.WalkTo(_player, waypoint);

            AssertBool(walk.Arrived).IsTrue();
            AssertBool(_player.GlobalPosition.Y > -1f).IsTrue();
        }

        AssertBool(_player.IsOnFloor()).IsTrue();
    }
}
