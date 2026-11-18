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
    private const string AssetsRoot = "res://Assets/";
    private const float Tolerance = 0.001f;

    [TestCase]
    public static void CatalogListsEveryShelfKindAndThePackingStation()
    {
        var ids = GD.Load<FurnitureCatalog>(CatalogPath).Items.Select(item => item.Id).ToArray();

        AssertThat(ids).IsEqual(new[] { "shelf_unit", "product_shelf", "storage_rack", "bulk_packing_station" });
    }

    // A piece collides as several boxes that follow its art (posts, boards, panels). Together they fill the
    // catalog footprint exactly: centred on the piece's origin, standing on the floor, as PlacedFurniture builds it.
    [TestCase]
    public static void EveryEntryMatchesTheCollidersOfItsScene()
    {
        foreach (var item in GD.Load<FurnitureCatalog>(CatalogPath).Items)
        {
            var instance = item.Scene!.Instantiate<Node3D>();
            try
            {
                var bounds = ColliderBounds(instance);
                AssertBool(Near(bounds.Size.X, item.Width)).OverrideFailureMessage($"{item.Id} width {bounds.Size.X}").IsTrue();
                AssertBool(Near(bounds.Size.Y, item.Height)).OverrideFailureMessage($"{item.Id} height {bounds.Size.Y}").IsTrue();
                AssertBool(Near(bounds.Size.Z, item.Depth)).OverrideFailureMessage($"{item.Id} depth {bounds.Size.Z}").IsTrue();
                var centre = bounds.GetCenter();
                AssertBool(Near(centre.X, 0) && Near(bounds.Position.Y, 0) && Near(centre.Z, 0))
                    .OverrideFailureMessage($"{item.Id} colliders are not centred on the floor: {bounds}").IsTrue();
            }
            finally
            {
                instance.Free();
            }
        }
    }

    [TestCase]
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
    public static void EveryItemSceneIsAPhysicalBodyWithCollisionAndAnArtSlot()
    {
        foreach (var item in GD.Load<FurnitureCatalog>(CatalogPath).Items)
        {
            var instance = item.Scene!.Instantiate<Node3D>();
            AddNode(instance);

            AssertThat(instance).IsInstanceOf<RigidBody3D>();
            AssertThat(instance.GetChildren().OfType<CollisionShape3D>().Count()).IsGreater(0);
            var slot = instance.GetNode<ShopArtSlot>("Art");
            AssertBool(slot.ArtLoaded || slot.Placeholder!.Visible).IsTrue();
        }
    }

    [TestCase]
    public static void EveryItemArtPathExistsInTheAssetsSubmodule()
    {
        foreach (var item in GD.Load<FurnitureCatalog>(CatalogPath).Items)
        {
            var instance = item.Scene!.Instantiate<Node3D>();
            var slot = instance.GetNode<ShopArtSlot>("Art");
            AssertBool(slot.ArtPath.StartsWith(AssetsRoot, StringComparison.Ordinal)).IsTrue();
            AssertBool(FileAccess.FileExists(slot.ArtPath)).OverrideFailureMessage(slot.ArtPath).IsTrue();
            instance.Free();
        }
    }

    [TestCase]
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
    public static void FindReturnsNullForAnUnknownId()
    {
        var catalog = GD.Load<FurnitureCatalog>(CatalogPath);

        AssertThat(catalog.Find("nope")).IsNull();
        AssertThat(catalog.Find("")).IsNull();
    }

    [TestCase]
    public static void IdsAreUnique()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in GD.Load<FurnitureCatalog>(CatalogPath).Items)
            AssertBool(seen.Add(item.Id)).IsTrue();
    }

    private static bool Near(float actual, float expected) => Math.Abs(actual - expected) <= Tolerance;

    // The piece's own collision boxes, in its frame.
    private static Aabb ColliderBounds(Node3D piece)
    {
        var shapes = piece.GetChildren().OfType<CollisionShape3D>().ToList();
        AssertThat(shapes.Count).IsGreater(0);
        var bounds = shapes[0].Transform * shapes[0].Shape.GetDebugMesh().GetAabb();
        foreach (var shape in shapes.Skip(1))
            bounds = bounds.Merge(shape.Transform * shape.Shape.GetDebugMesh().GetAabb());
        return bounds;
    }
}
