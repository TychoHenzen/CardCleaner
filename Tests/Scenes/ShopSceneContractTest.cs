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
    private const string ScenePath = "res://Scenes/Gameplay/ShopScene.tscn";
    private const string PackFolder = "res://Assets/Synty/";
    private const float FloorClearance = 0.01f;
    private const float WalkSampleStep = 0.1f;

    private static readonly string[] AreaNames = ["Storefront", "Storage", "Backoffice"];
    private static readonly string[] MarkerNames = ["PcLocation", "SeamLocation", "DeliveryPoint", "ShelfSlotsArea"];

    private PackedScene _packed = null!;
    private Node3D _shop = null!;
    private CapsuleShape3D _playerShape = null!;
    private Vector3 _spawn;

    [BeforeTest]
    public async Task Setup()
    {
        _packed = GD.Load<PackedScene>(ScenePath);
        _shop = _packed.Instantiate<Node3D>();

        var player = _shop.GetNode<CharacterBody3D>("Player");
        _spawn = player.Position;
        _playerShape = (CapsuleShape3D)player.GetNode<CollisionShape3D>("CollisionShape3D").Shape;

        // The walk checks sweep the player capsule through the level, so the live body must not be in the way.
        _shop.RemoveChild(player);
        player.Free();

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [TestCase]
    public void SceneLoadsAndInstantiates()
    {
        AssertThat(_packed).IsNotNull();
        AssertThat(_shop).IsNotNull();
    }

    [TestCase]
    public void AreasAreSeparatelyNamedNodesWithFloorCollision()
    {
        foreach (var area in AreaNames)
        {
            AssertThat(_shop.GetNodeOrNull<Node3D>($"World/{area}")).IsNotNull();
            AssertThat(FloorShape(area)).IsNotNull();
        }
    }

    [TestCase]
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
    public void MarkersExistForLaterFeatures()
    {
        foreach (var marker in MarkerNames)
        {
            AssertThat(_shop.GetNodeOrNull<Marker3D>($"World/Markers/{marker}")).IsNotNull();
        }
    }

    [TestCase]
    public void MarkersSitInsideTheShopFootprint()
    {
        foreach (var marker in MarkerNames)
        {
            var position = _shop.GetNode<Marker3D>($"World/Markers/{marker}").GlobalPosition;
            AssertBool(IsInsideAnyArea(position)).IsTrue();
        }
    }

    [TestCase]
    public void PcAndSeamMarkersAreInTheBackoffice()
    {
        AssertBool(IsInsideArea("Backoffice", MarkerPosition("PcLocation"))).IsTrue();
        AssertBool(IsInsideArea("Backoffice", MarkerPosition("SeamLocation"))).IsTrue();
    }

    [TestCase]
    public void PlayerSpawnsInsideTheStorefrontAboveTheFloor()
    {
        AssertBool(IsInsideArea("Storefront", _spawn)).IsTrue();
        AssertBool(_spawn.Y > 0f).IsTrue();
        AssertBool(SupportedByFloor(_spawn with { Y = 0f })).IsTrue();
        AssertBool(CapsuleFits(_spawn with { Y = _playerShape.Height / 2f + FloorClearance })).IsTrue();
    }

    [TestCase]
    public void PlayerCanWalkEveryRouteBetweenTheAreas()
    {
        AssertBool(CanWalk(StorefrontToStorage)).IsTrue();
        AssertBool(CanWalk(StorageToBackoffice)).IsTrue();
        AssertBool(CanWalk(BackofficeToStorefront)).IsTrue();
    }

    [TestCase]
    public void StorefrontEntranceIsOpen()
    {
        AssertBool(CanWalk([new Vector2(_spawn.X, _spawn.Z), new Vector2(6.05f, 11.5f)])).IsTrue();
    }

    [TestCase]
    public void StreetOutsideTheEntranceIsFlooredAndBounded()
    {
        AssertBool(CanWalk([new Vector2(6.05f, 9f), new Vector2(6.05f, 15.2f)])).IsTrue();
        AssertBool(CanWalk([new Vector2(6.05f, 15.2f), new Vector2(6.05f, 17f)])).IsFalse();
        AssertBool(CanWalk([new Vector2(6.05f, 13f), new Vector2(-1f, 13f)])).IsFalse();
        AssertBool(CanWalk([new Vector2(6.05f, 13f), new Vector2(17f, 13f)])).IsFalse();
    }

    [TestCase]
    public void AreasAreNotReachableThroughSolidWalls()
    {
        AssertBool(CanWalk([new Vector2(6f, 4f), new Vector2(6f, -4f)])).IsFalse();
        AssertBool(CanWalk([new Vector2(4f, -4f), new Vector2(12f, -6f)])).IsFalse();
    }

    [TestCase]
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

    private static readonly Vector2[] StorefrontToStorage =
        [new(6f, 9f), new(2.05f, 2f), new(2.05f, -2f), new(4f, -4f)];

    private static readonly Vector2[] StorageToBackoffice =
        [new(4f, -4f), new(7f, -2.05f), new(9f, -2.05f), new(12f, -4f)];

    private static readonly Vector2[] BackofficeToStorefront =
        [new(12f, -4f), new(14.05f, -2f), new(14.05f, 2f), new(13f, 8f), new(6f, 9f)];

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

    private bool CanWalk(Vector2[] route)
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
                if (!SupportedByFloor(feet) || !CapsuleFits(feet with { Y = _playerShape.Height / 2f + FloorClearance }))
                    return false;
            }
        }

        return true;
    }

    private bool SupportedByFloor(Vector3 feet)
    {
        var query = PhysicsRayQueryParameters3D.Create(feet + Vector3.Up * 0.5f, feet + Vector3.Down * 1f);
        return _shop.GetWorld3D().DirectSpaceState.IntersectRay(query).Count > 0;
    }

    private bool CapsuleFits(Vector3 center)
    {
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = _playerShape,
            Transform = new Transform3D(Basis.Identity, center)
        };
        return _shop.GetWorld3D().DirectSpaceState.IntersectShape(query, 1).Count == 0;
    }
}
