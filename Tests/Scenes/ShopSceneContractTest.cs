using System.Collections.Generic;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The graybox shop scene keeps its three named areas, collision, markers, spawn and pack-mesh
/// slots. The licensed pack files are gitignored, so nothing here requires them to exist.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopSceneContractTest
{
    private const string PackFolder = "res://Assets/Synty/";

    private static readonly string[] AreaNames = ["Storefront", "Storage", "Backoffice"];
    private static readonly string[] MarkerNames = ["PcLocation", "SeamLocation", "DeliveryPoint", "ShelfSlotsArea"];

    private PackedScene _packed = null!;
    private Node3D _shop = null!;
    private ShopSceneProbe _probe = null!;
    private Vector3 _spawn;

    [BeforeTest]
    public async Task Setup()
    {
        _packed = GD.Load<PackedScene>(ShopSceneProbe.ScenePath);
        _shop = _packed.Instantiate<Node3D>();

        var player = _shop.GetNode<CharacterBody3D>("Player");
        _spawn = player.Position;
        _probe = new ShopSceneProbe(_shop, (CapsuleShape3D)player.GetNode<CollisionShape3D>("CollisionShape3D").Shape);

        // The walk checks sweep the player capsule through the level, so the live body must not be in the way.
        _shop.RemoveChild(player);
        player.Free();

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SceneLoadsAndInstantiates()
    {
        AssertThat(_packed).IsNotNull();
        AssertThat(_shop).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AreasAreSeparatelyNamedNodesWithFloorCollision()
    {
        foreach (var area in AreaNames)
        {
            AssertThat(_shop.GetNodeOrNull<Node3D>($"World/{area}")).IsNotNull();
            AssertThat(FloorShape(area)).IsNotNull();
            AssertThat(FloorShape(area)!.Shape).IsInstanceOf<BoxShape3D>();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WallsHaveCollision()
    {
        var walls = _shop.GetNode("World/Walls");

        AssertThat(walls.GetChildCount()).IsGreater(0);
        foreach (var wall in walls.GetChildren())
        {
            AssertThat(wall).IsInstanceOf<StaticBody3D>();
            AssertThat(CollisionShapeCount(wall)).IsGreater(0);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MarkersExistForLaterFeatures()
    {
        foreach (var marker in MarkerNames)
        {
            AssertThat(_shop.GetNodeOrNull<Marker3D>($"World/Markers/{marker}")).IsNotNull();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MarkersSitInsideTheShopFootprint()
    {
        foreach (var marker in MarkerNames)
        {
            var position = _shop.GetNode<Marker3D>($"World/Markers/{marker}").GlobalPosition;
            AssertBool(IsInsideAnyArea(position)).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PcAndSeamMarkersAreInTheBackoffice()
    {
        AssertBool(IsInsideArea("Backoffice", MarkerPosition("PcLocation"))).IsTrue();
        AssertBool(IsInsideArea("Backoffice", MarkerPosition("SeamLocation"))).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PlayerSpawnsInsideTheStorefrontAboveTheFloor()
    {
        AssertBool(IsInsideArea("Storefront", _spawn)).IsTrue();
        AssertBool(_spawn.Y > 0f).IsTrue();
        AssertBool(_probe.SupportedByFloor(_spawn with { Y = 0f })).IsTrue();
        AssertBool(_probe.CapsuleFits(_spawn with { Y = _probe.CapsuleCenterHeight })).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ArtSlotsPointAtPackMeshesForWallsFloorDoorsAndCounter()
    {
        var slots = new List<ShopArtSlot>();
        CollectArtSlots(_shop, slots);

        foreach (var kind in new[] { "SI_Env_Wall_0", "SI_Env_Floor", "SI_Env_Wall_Door", "SI_Prop_CheckoutCounter" })
        {
            AssertBool(slots.Exists(s => s.ArtPath.Contains(kind))).IsTrue();
        }

        foreach (var slot in slots)
        {
            AssertBool(slot.ArtPath.StartsWith(PackFolder)).IsTrue();
            AssertThat(slot.Placeholder).IsNotNull();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ArtSlotKeepsPlaceholderWhenPackFileIsMissing()
    {
        var placeholder = new Node3D();
        var slot = new ShopArtSlot { ArtPath = PackFolder + "DoesNotExist/Missing.fbx", Placeholder = placeholder };
        slot.AddChild(placeholder);

        AddNode(slot);

        AssertBool(slot.ArtLoaded).IsFalse();
        AssertBool(placeholder.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SceneStillBuildsWhenPackFilesAreMissing()
    {
        var slots = new List<ShopArtSlot>();
        CollectArtSlots(_shop, slots);

        foreach (var slot in slots)
        {
            AssertBool(slot.ArtLoaded || slot.Placeholder!.Visible).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SceneDoesNotSerializeRuntimeOnlyFields()
    {
        var state = _packed.GetState();

        for (int node = 0; node < state.GetNodeCount(); node++)
        {
            for (int i = 0; i < state.GetNodePropertyCount(node); i++)
            {
                var property = state.GetNodePropertyName(node, i).ToString();
                AssertBool(property.StartsWith('_')).IsFalse();
                AssertThat(property).IsNotEqual(nameof(ShopArtSlot.ArtLoaded));
            }
        }
    }

    private Vector3 MarkerPosition(string marker) =>
        _shop.GetNode<Marker3D>($"World/Markers/{marker}").GlobalPosition;

    private CollisionShape3D? FloorShape(string area) =>
        _shop.GetNodeOrNull<CollisionShape3D>($"World/{area}/Floor/CollisionShape3D");

    private bool IsInsideAnyArea(Vector3 position) =>
        System.Array.Exists(AreaNames, area => IsInsideArea(area, position));

    private bool IsInsideArea(string area, Vector3 position)
    {
        var floor = FloorShape(area)!;
        var half = ((BoxShape3D)floor.Shape).Size / 2f;
        var center = floor.GlobalPosition;
        return System.Math.Abs(position.X - center.X) <= half.X && System.Math.Abs(position.Z - center.Z) <= half.Z;
    }

    private static int CollisionShapeCount(Node body)
    {
        int count = 0;
        foreach (var child in body.GetChildren())
        {
            if (child is CollisionShape3D { Shape: not null })
                count++;
        }

        return count;
    }

    private static void CollectArtSlots(Node node, List<ShopArtSlot> slots)
    {
        if (node is ShopArtSlot slot)
            slots.Add(slot);

        foreach (var child in node.GetChildren())
            CollectArtSlots(child, slots);
    }
}
