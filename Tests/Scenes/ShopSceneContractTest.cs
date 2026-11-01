using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Portal.Components;
using CardCleaner.Scripts.Features.Shop.Components;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The graybox shop scene keeps its three named areas, collision, markers, spawn and pack-mesh
/// slots. The licensed pack files live in the private Assets submodule and may be absent, so nothing
/// here requires them to exist.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopSceneContractTest
{
    private const string PackFolder = "res://Assets/Synty/";

    private static readonly string[] AreaNames =
        ["Storefront", "Storage", "Backoffice", "RightStorage", "ExpansionLot"];
    private static readonly string[] ShopMarkerNames =
        ["PcLocation", "SeamLocation", "DeliveryPoint", "ShelfSlotsArea", "ExpansionLot1"];
    private static readonly string[] StreetMarkerNames = ["CustomerSpawn", "CustomerDespawn"];
    private const float RoadDoorX = 18.05f;

    private PackedScene _packed = null!;
    private Node3D _shop = null!;
    private ShopSceneProbe _probe = null!;
    private Vector3 _spawn;

    [BeforeTest]
    public async Task Setup()
    {
        _packed = GD.Load<PackedScene>(ShopSceneProbe.ScenePath);
        _shop = _packed.Instantiate<Node3D>();

        // The walk checks sweep the player capsule through the level, so the live body must not be in the way.
        var player = ShopScenePlayer.Sideline(_shop);
        _spawn = player.Position;
        _probe = new ShopSceneProbe(_shop, (CapsuleShape3D)player.GetNode<CollisionShape3D>("CollisionShape3D").Shape);

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
        foreach (var marker in ShopMarkerNames.Concat(StreetMarkerNames))
        {
            AssertThat(_shop.GetNodeOrNull<Marker3D>($"World/Markers/{marker}")).IsNotNull();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MarkersSitInsideTheShopFootprint()
    {
        foreach (var marker in ShopMarkerNames)
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
    public void StorageAndBackofficeAreOneRoom()
    {
        AssertThat(_shop.GetNodeOrNull("World/Walls/BackRoomDivider")).IsNull();
        foreach (var z in new[] { -5f, -3f, -1f })
        {
            AssertBool(_probe.ProbeSweep(new Vector3(4f, 1f, z), new Vector3(12f, 1f, z))).IsTrue();
        }

        AssertThat(_shop.GetNode<Node3D>("World/BackofficeZone").Position).IsEqual(new Vector3(12f, 2f, -4f));
        AssertThat(MarkerPosition("PcLocation")).IsEqual(new Vector3(10f, 0f, -6.9f));
        AssertThat(MarkerPosition("SeamLocation")).IsEqual(new Vector3(12f, 1.5f, -7.8f));
        AssertThat(_shop.GetNode<Node3D>("World/Backoffice/Floor").GlobalPosition)
            .IsEqual(new Vector3(12f, -0.1f, -4f));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RightArmWrapsTheStorefront()
    {
        AssertBool(IsInsideArea("RightStorage", new Vector3(23f, 0f, -7f))).IsTrue();
        AssertBool(IsInsideArea("RightStorage", new Vector3(17f, 0f, 11f))).IsTrue();
        AssertThat(FloorShape("RightStorage")!.GlobalPosition).IsEqual(new Vector3(20f, -0.1f, 2f));

        // Opening in the old wall at x = 16, z -8..0.
        AssertBool(_probe.ProbeSweep(new Vector3(12f, 1f, -4f), new Vector3(20f, 1f, -4f))).IsTrue();
        // The room continues along the storefront's right side.
        AssertBool(_probe.ProbeSweep(new Vector3(20f, 1f, -4f), new Vector3(20f, 1f, 6f))).IsTrue();
        // The wall at x = 16 for z 0..12 stays.
        AssertBool(_probe.ProbeSweep(new Vector3(14f, 1f, 6f), new Vector3(15f, 1f, 6f))).IsTrue();
        AssertBool(_probe.ProbeSweep(new Vector3(14f, 1f, 6f), new Vector3(18f, 1f, 6f))).IsFalse();
        // The outer right wall at x = 24.
        AssertBool(_probe.ProbeSweep(new Vector3(22f, 1f, 6f), new Vector3(23.6f, 1f, 6f))).IsFalse();
        // The outer back wall at z = -8.
        AssertBool(_probe.ProbeSweep(new Vector3(20f, 1f, -6f), new Vector3(20f, 1f, -7.6f))).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RoadDoorJoinsStreetAndRightArm()
    {
        var street = _shop.GetNode<CollisionShape3D>("World/Street/Floor/CollisionShape3D");
        AssertThat(((BoxShape3D)street.Shape).Size).IsEqual(new Vector3(40f, 0.2f, 4f));
        AssertThat(street.GlobalPosition).IsEqual(new Vector3(4f, -0.1f, 14f));

        // Street through the road door, then to the delivery point.
        AssertBool(_probe.ProbeSweep(new Vector3(RoadDoorX, 1f, 14f), new Vector3(RoadDoorX, 1f, 9f))).IsTrue();
        AssertBool(_probe.ProbeSweep(new Vector3(RoadDoorX, 1f, 9f), new Vector3(20f, 1f, 9f))).IsTrue();
        // Solid wall beside the door.
        AssertBool(_probe.ProbeSweep(new Vector3(21f, 1f, 14f), new Vector3(21f, 1f, 9f))).IsFalse();
        // The front door still joins the street and the storefront.
        AssertBool(_probe.ProbeSweep(new Vector3(6.05f, 1f, 14f), new Vector3(6.05f, 1f, 9f))).IsTrue();
        // The street runs along the whole front.
        AssertBool(_probe.ProbeSweep(new Vector3(-15f, 1f, 14f), new Vector3(23f, 1f, 14f))).IsTrue();
        // The street is walled on its building side where no building stands.
        AssertBool(_probe.ProbeSweep(new Vector3(-12f, 1f, 14f), new Vector3(-12f, 1f, 12.4f))).IsFalse();

        var delivery = MarkerPosition("DeliveryPoint");
        AssertThat(delivery).IsEqual(new Vector3(20f, 0f, 9f));
        AssertBool(IsInsideArea("RightStorage", delivery)).IsTrue();
        AssertBool(delivery.Z is > 8f and < 12f).IsTrue();
        AssertBool(Mathf.Abs(delivery.X - RoadDoorX) < 2f).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ExpansionLotIsLockedLeftOfTheStorefront()
    {
        // Storefront side, stopped by LeftWall at x = 0.
        AssertBool(_probe.ProbeSweep(new Vector3(2f, 1f, 6f), new Vector3(-4f, 1f, 6f))).IsFalse();
        // Storage side, stopped by LeftWall.
        AssertBool(_probe.ProbeSweep(new Vector3(2f, 1f, -4f), new Vector3(-4f, 1f, -4f))).IsFalse();
        // Lot to street, stopped by the lot's street-side wall.
        AssertBool(_probe.ProbeSweep(new Vector3(-4f, 1f, 6f), new Vector3(-4f, 1f, 14f))).IsFalse();
        // Storefront part to storage part, stopped by the wall at z = 0.
        AssertBool(_probe.ProbeSweep(new Vector3(-4f, 1f, 6f), new Vector3(-4f, 1f, -4f))).IsFalse();
        // Lot back wall at z = -8.
        AssertBool(_probe.ProbeSweep(new Vector3(-4f, 1f, -4f), new Vector3(-4f, 1f, -7.6f))).IsFalse();
        // Lot outer left wall at x = -8.
        AssertBool(_probe.ProbeSweep(new Vector3(-4f, 1f, 6f), new Vector3(-7.6f, 1f, 6f))).IsFalse();

        AssertThat(_shop.GetNodeOrNull<StaticBody3D>("World/Walls/LeftWall")).IsNotNull();
        AssertThat(MarkerPosition("ExpansionLot1")).IsEqual(new Vector3(-4f, 0f, 6f));
        AssertBool(IsInsideArea("ExpansionLot", MarkerPosition("ExpansionLot1"))).IsTrue();
        AssertThat(FloorShape("ExpansionLot")!.GlobalPosition).IsEqual(new Vector3(-4f, -0.1f, 2f));
        AssertBool(FloorShape("ExpansionLot")!.GlobalPosition.X < FloorShape("Storefront")!.GlobalPosition.X).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NavigationRegionExists()
    {
        var region = _shop.GetNodeOrNull<NavigationRegion3D>("World/Navigation");
        AssertThat(region).IsNotNull();
        var mesh = region!.NavigationMesh;
        AssertThat(mesh).IsNotNull();
        // Static colliders, groups with children.
        AssertThat((int)mesh!.GeometryParsedGeometryType).IsEqual(1);
        AssertThat((int)mesh.GeometrySourceGeometryMode).IsEqual(1);
        AssertThat(mesh.GeometrySourceGroupName.ToString()).IsEqual("shop_navigation");
        // x -16..24, z -8..16.
        AssertThat(mesh.FilterBakingAabb).IsEqual(new Aabb(-16f, -1f, -8f, 40f, 5f, 24f));
        foreach (var name in new[]
                     { "Storefront", "Storage", "Backoffice", "RightStorage", "ExpansionLot", "Street", "Walls" })
        {
            AssertBool(_shop.GetNode($"World/{name}").IsInGroup("shop_navigation")).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SpawnAndDespawnMarkersAreOnTheStreet()
    {
        foreach (var marker in StreetMarkerNames)
        {
            var position = MarkerPosition(marker);
            AssertBool(IsOnStreet(position)).IsTrue();
            // Outside the road-door approach.
            AssertBool(Mathf.Abs(position.X - RoadDoorX) > 3f).IsTrue();
        }

        AssertThat(MarkerPosition("CustomerSpawn")).IsEqual(new Vector3(-14f, 0f, 14f));
        AssertThat(MarkerPosition("CustomerDespawn")).IsEqual(new Vector3(22f, 0f, 14f));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SidelinedPlayerStaysTheLiveTargetOfBothSeams()
    {
        var player = _shop.GetNode<PlayerController>("Player");

        AssertBool(player.IsInsideTree()).IsTrue();
        AssertThat(_shop.GetNode<WallSeam>("World/Markers/SeamLocation/Seam").Player).IsSame(player);
        AssertThat(_shop.GetNode<WallSeam>("World/Workshop/ReturnSeam").Player).IsSame(player);
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

    private bool IsOnStreet(Vector3 position)
    {
        var floor = _shop.GetNode<CollisionShape3D>("World/Street/Floor/CollisionShape3D");
        var half = ((BoxShape3D)floor.Shape).Size / 2f;
        var center = floor.GlobalPosition;
        return Mathf.Abs(position.X - center.X) <= half.X && Mathf.Abs(position.Z - center.Z) <= half.Z;
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
