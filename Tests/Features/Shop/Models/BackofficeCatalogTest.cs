using System.Linq;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Models;

/// <summary>
/// The backoffice catalog is data: items, prices and scenes come from the resource, and each item
/// scene keeps working (placeholder visible) when the licensed pack files are not imported.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BackofficeCatalogTest
{
    private const string CatalogPath = "res://Data/Shop/BackofficeCatalog.tres";
    private const string ManifestPath = "res://tools/synty-assets.json";
    private const string PackPrefix = "res://Assets/Synty/";

    private static readonly string[] ExpectedIds = ["shelf", "bulk_packing_station", "cardboard_box"];

    private OrderCatalog _catalog = null!;

    [BeforeTest]
    public void Setup()
    {
        _catalog = GD.Load<OrderCatalog>(CatalogPath);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CatalogListsShelfPackingStationAndCardboardBox()
    {
        AssertThat(_catalog).IsNotNull();
        AssertThat(_catalog.Items.Select(i => i.Id).ToArray()).IsEqual(ExpectedIds);
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
    public void EveryItemSceneIsAPhysicalBodyWithCollisionAndAnArtSlot()
    {
        foreach (var item in _catalog.Items)
        {
            var instance = item.Scene!.Instantiate<Node3D>();
            AddNode(instance);

            AssertThat(instance).IsInstanceOf<RigidBody3D>();
            AssertThat(instance.GetNodeOrNull<CollisionShape3D>("CollisionShape3D")).IsNotNull();
            var slot = instance.GetNode<ShopArtSlot>("Art");
            AssertBool(slot.ArtLoaded || slot.Placeholder!.Visible).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EveryItemArtPathIsListedInTheAssetManifest()
    {
        var manifest = FileAccess.GetFileAsString(ManifestPath);
        AssertBool(manifest.Length > 0).IsTrue();

        foreach (var item in _catalog.Items)
        {
            var instance = item.Scene!.Instantiate<Node3D>();
            var slot = instance.GetNode<ShopArtSlot>("Art");
            AssertBool(slot.ArtPath.StartsWith(PackPrefix)).IsTrue();
            AssertBool(manifest.Contains($"\"{slot.ArtPath[PackPrefix.Length..]}\"")).IsTrue();
            instance.Free();
        }
    }
}
