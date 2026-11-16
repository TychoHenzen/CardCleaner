using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class FurnitureCatalogTest
{
    private const string CatalogPath = "res://Data/Shop/Furniture/FurnitureItems.tres";
    private const float Tolerance = 0.001f;

    [TestCase]
    [TestCategory("Unit")]
    public static void CatalogListsEveryShelfKindAndThePackingStation()
    {
        var ids = GD.Load<FurnitureCatalog>(CatalogPath).Items.Select(item => item.Id).ToArray();

        AssertThat(ids).IsEqual(new[] { "shelf_unit", "product_shelf", "storage_rack", "bulk_packing_station" });
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryEntryMatchesTheColliderOfItsScene()
    {
        foreach (var item in GD.Load<FurnitureCatalog>(CatalogPath).Items)
        {
            var instance = item.Scene!.Instantiate<Node3D>();
            try
            {
                var shape = instance.GetNode<CollisionShape3D>("CollisionShape3D").Shape as BoxShape3D;
                AssertThat(shape).IsNotNull();
                AssertBool(Near(shape!.Size.X, item.Width)).OverrideFailureMessage(item.Id + " width").IsTrue();
                AssertBool(Near(shape.Size.Y, item.Height)).OverrideFailureMessage(item.Id + " height").IsTrue();
                AssertBool(Near(shape.Size.Z, item.Depth)).OverrideFailureMessage(item.Id + " depth").IsTrue();
            }
            finally
            {
                instance.Free();
            }
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryShelfKindIsACardShelf()
    {
        var catalog = GD.Load<FurnitureCatalog>(CatalogPath);
        var kinds = new Dictionary<string, ShelfKind>
        {
            ["shelf_unit"] = ShelfKind.CardShelf,
            ["product_shelf"] = ShelfKind.ProductShelf,
            ["storage_rack"] = ShelfKind.StorageRack,
        };

        foreach (var (id, kind) in kinds)
        {
            var instance = catalog.Find(id)!.Scene!.Instantiate<CardShelf>();
            AssertThat(instance.Kind).IsEqual(kind);
            instance.Free();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryItemSceneIsAPhysicalBodyWithCollisionAndAnArtSlot()
    {
        foreach (var item in GD.Load<FurnitureCatalog>(CatalogPath).Items)
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
    public static void EveryItemArtPathExistsInTheAssetsSubmodule()
    {
        foreach (var item in GD.Load<FurnitureCatalog>(CatalogPath).Items)
        {
            var instance = item.Scene!.Instantiate<Node3D>();
            var slot = instance.GetNode<ShopArtSlot>("Art");
            AssertBool(slot.ArtPath.StartsWith(SyntyAssets.Root)).IsTrue();
            AssertBool(SyntyAssets.Exists(slot.ArtPath[SyntyAssets.Root.Length..])).IsTrue();
            instance.Free();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ItemSceneKeepsItsPlaceholderWhenThePackFileIsMissing()
    {
        foreach (var item in GD.Load<FurnitureCatalog>(CatalogPath).Items)
        {
            var instance = item.Scene!.Instantiate<Node3D>();
            var slot = instance.GetNode<ShopArtSlot>("Art");
            slot.ArtPath = SyntyAssets.Root + "DoesNotExist/Missing.fbx";
            AddNode(instance);

            AssertBool(slot.ArtLoaded).IsFalse();
            AssertBool(slot.Placeholder!.Visible).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ItemSceneShowsThePackMeshAndHidesItsPlaceholderWhenTheFileIsImported()
    {
        foreach (var item in GD.Load<FurnitureCatalog>(CatalogPath).Items)
        {
            var instance = item.Scene!.Instantiate<Node3D>();
            var slot = instance.GetNode<ShopArtSlot>("Art");
            AddNode(instance);

            // The pack files come from the private Assets submodule, which may be missing or unimported here, so
            // this machine either swaps in the pack mesh or keeps the placeholder.
            var imported = ResourceLoader.Exists(slot.ArtPath);
            AssertBool(slot.ArtLoaded).IsEqual(imported);
            AssertBool(slot.Placeholder!.Visible).IsEqual(!imported);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void FindReturnsNullForAnUnknownId()
    {
        var catalog = GD.Load<FurnitureCatalog>(CatalogPath);

        AssertThat(catalog.Find("nope")).IsNull();
        AssertThat(catalog.Find("")).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void IdsAreUnique()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in GD.Load<FurnitureCatalog>(CatalogPath).Items)
            AssertBool(seen.Add(item.Id)).IsTrue();
    }

    private static bool Near(float actual, float expected) => Math.Abs(actual - expected) <= Tolerance;
}
