using System;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

[TestSuite]
[RequireGodotRuntime]
public class PlacedFurnitureTest
{
    private const float Tolerance = 0.001f;
    private const string CatalogPath = "res://Data/Shop/Furniture/FurnitureItems.tres";

    private static readonly FurniturePose Pose = new(8f, 6f, 1);

    private FurnitureItem _shelf = null!;

    [BeforeTest]
    public void Setup()
    {
        var catalog = ResourceLoader.Load<FurnitureCatalog>(CatalogPath);
        _shelf = catalog!.Find("shelf_unit")!;
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Create_BuildsAStaticBodyWithTheItemBox()
    {
        var placed = PlacedFurniture.Create(_shelf, new FurniturePose(8f, 6f, 0));
        AssertBool(placed != null).IsTrue();
        AddNode(placed!);

        AssertBool(placed is StaticBody3D).IsTrue();
        AssertInt((int)placed!.CollisionLayer).IsEqual(1);
        AssertInt((int)placed.CollisionMask).IsEqual(0);

        var shape = placed.GetChildren().OfType<CollisionShape3D>().Single();
        var box = (BoxShape3D)shape.Shape;
        AssertBool(MathF.Abs(box.Size.X - 2.13f) <= Tolerance).IsTrue();
        AssertBool(MathF.Abs(box.Size.Y - 1.07f) <= Tolerance).IsTrue();
        AssertBool(MathF.Abs(box.Size.Z - 1.0f) <= Tolerance).IsTrue();
        AssertBool(MathF.Abs(shape.Position.Y - 0.535f) <= Tolerance).IsTrue();

        var content = placed.GetNode<Node3D>(PlacedFurniture.ContentName);
        AssertBool(content is CardShelf).IsTrue();
        AssertBool(((RigidBody3D)content).Freeze).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Place_SetsPositionAndYaw()
    {
        var placed = PlacedFurniture.Create(_shelf, new FurniturePose(0f, 0f, 0));
        AssertBool(placed != null).IsTrue();
        AddNode(placed!);

        placed!.Place(Pose);

        AssertBool(MathF.Abs(placed.Position.X - 8f) <= Tolerance).IsTrue();
        AssertBool(MathF.Abs(placed.Position.Y) <= Tolerance).IsTrue();
        AssertBool(MathF.Abs(placed.Position.Z - 6f) <= Tolerance).IsTrue();
        AssertBool(MathF.Abs(placed.Rotation.Y - Mathf.Pi / 2f) <= Tolerance).IsTrue();
        AssertBool(placed.Pose == Pose).IsTrue();
        AssertBool(placed.Rect == FurniturePlacementModel.RectOf(_shelf.Footprint, Pose)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Create_ReturnsNullWithoutAScene()
    {
        var item = new FurnitureItem { Id = "no_scene", Width = 1f, Depth = 1f, Height = 1f, Scene = null };

        var placed = PlacedFurniture.Create(item, Pose);

        AssertBool(placed == null).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CanPickUp_IsTrueForAnEmptyShelfAndFalseWhenStockIsHeld()
    {
        var placed = PlacedFurniture.Create(_shelf, Pose);
        AssertBool(placed != null).IsTrue();
        AddNode(placed!);

        AssertBool(placed!.CanPickUp).IsTrue();

        placed.HoldsStockOverride = () => true;
        AssertBool(placed.CanPickUp).IsFalse();
    }
}
