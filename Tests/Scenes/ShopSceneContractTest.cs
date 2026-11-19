using System.Linq;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The graybox shop scene keeps its named areas, collision, markers and spawn. The pack-mesh slots
/// are covered by <see cref="ShopSceneArtSlotTest"/>, and where the player can walk by
/// <see cref="ShopSceneLayoutWalkTest"/>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopSceneContractTest : ShopSceneContractTestBase
{
    private static readonly string[] ShopMarkerNames =
        [
            "PcLocation", "SeamLocation", "DeliveryPoint", "StockDeliveryPoint", "ShelfSlotsArea", "ExpansionLot1",
            "CustomerCounter",
        ];
    private static readonly string[] StreetMarkerNames = ["CustomerSpawn", "CustomerDespawn"];

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
            AssertThat(FloorShape(area)!.Shape).IsInstanceOf<BoxShape3D>();
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
    public void ExactlyOneHiddenRestockerStandsInTheRightStorageRoom()
    {
        var restockers = _shop.FindChildren("*", "CharacterBody3D", true, false)
            .Where(node => node is Restocker).ToList();

        AssertInt(restockers.Count).IsEqual(1);
        var restocker = _shop.GetNode<Restocker>("World/RightStorage/Restocker");
        AssertBool(restocker.Visible).IsFalse();
        AssertBool(IsInsideArea("RightStorage", restocker.GlobalPosition)).IsTrue();
    }

    [TestCase]
    public void MarkersExistForLaterFeatures()
    {
        foreach (var marker in ShopMarkerNames.Concat(StreetMarkerNames))
        {
            AssertThat(_shop.GetNodeOrNull<Marker3D>($"World/Markers/{marker}")).IsNotNull();
        }
    }

    [TestCase]
    public void MarkersSitInsideTheShopFootprint()
    {
        foreach (var marker in ShopMarkerNames)
        {
            var position = _shop.GetNode<Marker3D>($"World/Markers/{marker}").GlobalPosition;
            AssertBool(IsInsideAnyArea(position)).IsTrue();
        }
    }

    [TestCase]
    public void PcAndSeamMarkersAreInTheBackoffice()
    {
        foreach (var marker in new[] { "PcLocation", "SeamLocation" })
        {
            AssertBool(this.IsInsideArea("Backoffice", this.MarkerPosition(marker))).IsTrue();
        }
    }

    [TestCase]
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
        string[] navigable =
            ["Storefront", "Storage", "Backoffice", "RightStorage", "ExpansionLot", "Street", "Walls"];
        foreach (var name in navigable)
        {
            AssertBool(_shop.GetNode($"World/{name}").IsInGroup("shop_navigation")).IsTrue();
        }
    }

    [TestCase]
    public void FurnitureRootJoinsTheNavigationGroup()
    {
        AssertBool(_shop.GetNode("World/Furniture").IsInGroup("shop_navigation")).IsTrue();

        var placement = _shop.GetNodeOrNull<FurniturePlacementController>("Services/FurniturePlacement");
        AssertThat(placement).IsNotNull();
        AssertThat(placement!.FurnitureRoot).IsNotNull();
        AssertThat(placement.Navigation).IsNotNull();
    }

    [TestCase]
    public void SpawnAndDespawnMarkersAreOnTheStreet()
    {
        foreach (var marker in StreetMarkerNames)
        {
            var position = MarkerPosition(marker);
            AssertBool(IsInsideArea("Street", position)).IsTrue();
            // Outside the road-door approach.
            AssertBool(Mathf.Abs(position.X - RoadDoorX) > 3f).IsTrue();
        }

        AssertThat(MarkerPosition("CustomerSpawn")).IsEqual(new Vector3(-14f, 0f, 14f));
        AssertThat(MarkerPosition("CustomerDespawn")).IsEqual(new Vector3(22f, 0f, 14f));
    }

    [TestCase]
    public void FurnitureAppIsWiredToTheDesktop()
    {
        var desktop = _shop.GetNode<PcDesktop>("PcDesktopViewport/PcDesktop");
        var furnitureApp = _shop.GetNode<StockApp>("PcDesktopViewport/FurnitureApp");
        AssertThat(desktop.FurnitureApp).IsEqual(furnitureApp);
        AssertThat(furnitureApp.Title).IsEqual("Furniture");
        AssertBool(furnitureApp.Catalog!.Items.Any(item => item.Id == "shelf_unit")).IsTrue();

        // Furniture is sold boxed by the Furniture app only; the Stock app sells the stock that goes on it.
        var stockApp = _shop.GetNode<StockApp>("PcDesktopViewport/StockApp");
        var furnitureIds = furnitureApp.Catalog.Items.Select(item => item.Id).ToHashSet();
        AssertBool(stockApp.Catalog!.Items.Any(item => furnitureIds.Contains(item.Id))).IsFalse();
    }

    private bool IsInsideAnyArea(Vector3 position) =>
        System.Array.Exists(AreaNames, area => IsInsideArea(area, position));

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
}
