using System.Linq;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Workshop.Models;

/// <summary>
/// The workshop catalog is data: a conveyor, an arcade cabinet and a wiring placeholder, each with a price and
/// a physical scene that keeps its placeholder visible when the licensed pack files are not imported.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WorkshopCatalogTest
{
    private const string CatalogPath = "res://Data/Workshop/WorkshopCatalog.tres";
    private const string ShopCatalogPath = "res://Data/Shop/BackofficeCatalog.tres";
    private const string ConveyorId = "conveyor";
    private const string PackPrefix = "res://Assets/Synty/";

    private static readonly string[] ExpectedIds = ["conveyor", "arcade_cabinet", "wiring"];

    private OrderCatalog _catalog = null!;

    [BeforeTest]
    public void Setup()
    {
        _catalog = GD.Load<OrderCatalog>(CatalogPath);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CatalogListsConveyorArcadeCabinetAndWiring()
    {
        AssertThat(_catalog).IsNotNull();
        AssertThat(_catalog.Items.Select(i => i.Id).ToArray()).IsEqual(ExpectedIds);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ItemsAreDisjointFromTheShopCatalogByIdAndScene()
    {
        var shop = GD.Load<OrderCatalog>(ShopCatalogPath);

        AssertThat(_catalog.Items.Select(i => i.Id).Intersect(shop.Items.Select(i => i.Id)).Count()).IsEqual(0);
        AssertThat(_catalog.Items.Select(i => i.Scene!.ResourcePath)
            .Intersect(shop.Items.Select(i => i.Scene!.ResourcePath)).Count()).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EveryItemHasANamePriceAndScene()
    {
        foreach (var item in _catalog.Items)
        {
            AssertBool(string.IsNullOrWhiteSpace(item.DisplayName)).IsFalse();
            AssertBool(item.Price > 0).IsTrue();
            AssertThat(item.Scene).IsNotNull();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ConveyorItemReusesTheExistingConveyorScene()
    {
        var conveyor = _catalog.Items.Single(i => i.Id == "conveyor").Scene!.Instantiate<Node3D>();
        AddNode(conveyor);

        AssertThat(conveyor.GetNodeOrNull("Belt")!.SceneFilePath).IsEqual("res://Scenes/Components/Conveyor_Straight.tscn");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EveryItemSceneIsAPlaceableNodeWithAnArtSlotAndPlaceholderFallback()
    {
        foreach (var item in _catalog.Items)
        {
            var instance = item.Scene!.Instantiate<Node3D>();
            AddNode(instance);

            var slot = instance.GetNode<ShopArtSlot>("Art");
            AssertThat(slot.Placeholder).IsNotNull();
            AssertBool(slot.ArtLoaded != slot.Placeholder!.Visible).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void BodyItemsAreRigidBodiesWithACollisionShape()
    {
        foreach (var item in _catalog.Items.Where(i => i.Id != ConveyorId))
        {
            var instance = item.Scene!.Instantiate<Node3D>();
            AddNode(instance);

            AssertThat(instance).IsInstanceOf<RigidBody3D>();
            AssertThat(instance.GetNodeOrNull<CollisionShape3D>("CollisionShape3D")).IsNotNull();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ConveyorCollidesThroughItsNestedBeltBody()
    {
        var conveyor = _catalog.Items.Single(i => i.Id == ConveyorId).Scene!.Instantiate<Node3D>();
        AddNode(conveyor);

        var belt = conveyor.GetNode("Belt");
        AssertBool(belt.IsClass("StaticBody3D")).IsTrue();
        AssertThat(belt.GetNodeOrNull<CollisionShape3D>("CollisionShape3D")).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EveryItemArtPathIsListedInTheAssetManifest()
    {
        AssertBool(FileAccess.FileExists(SyntyManifest.Path)).IsTrue();

        foreach (var item in _catalog.Items)
        {
            var instance = item.Scene!.Instantiate<Node3D>();
            var slot = instance.GetNode<ShopArtSlot>("Art");
            AssertBool(slot.ArtPath.StartsWith(PackPrefix)).IsTrue();
            AssertBool(SyntyManifest.ListsTarget(slot.ArtPath[PackPrefix.Length..])).IsTrue();
            instance.Free();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ItemSceneKeepsItsPlaceholderWhenThePackFileIsMissing()
    {
        foreach (var item in _catalog.Items)
        {
            var instance = item.Scene!.Instantiate<Node3D>();
            var slot = instance.GetNode<ShopArtSlot>("Art");
            slot.ArtPath = PackPrefix + "DoesNotExist/Missing.fbx";
            AddNode(instance);

            AssertBool(slot.ArtLoaded).IsFalse();
            AssertBool(slot.Placeholder!.Visible).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ItemSceneShowsThePackMeshAndHidesItsPlaceholderWhenTheFileIsImported()
    {
        foreach (var item in _catalog.Items)
        {
            var instance = item.Scene!.Instantiate<Node3D>();
            var slot = instance.GetNode<ShopArtSlot>("Art");
            AddNode(instance);

            var imported = ResourceLoader.Exists(slot.ArtPath);
            AssertBool(slot.ArtLoaded).IsEqual(imported);
            AssertBool(slot.Placeholder!.Visible).IsEqual(!imported);
        }
    }
}
