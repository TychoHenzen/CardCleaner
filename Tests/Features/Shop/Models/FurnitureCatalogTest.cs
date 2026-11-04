using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
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
    public static void ShelfEntryMatchesTheShelfScene()
    {
        var item = GD.Load<FurnitureCatalog>(CatalogPath).Find("shelf_unit");
        AssertThat(item).IsNotNull();

        var instance = item!.Scene!.Instantiate<CardShelf>();
        try
        {
            AssertBool(Near(item.Footprint.Width, 2.13f)).IsTrue();
            AssertBool(Near(item.Footprint.Depth, 1.0f)).IsTrue();
            AssertBool(Near(item.Height, 1.07f)).IsTrue();

            var shape = instance.GetNode<CollisionShape3D>("CollisionShape3D").Shape as BoxShape3D;
            AssertThat(shape).IsNotNull();
            AssertBool(Near(shape!.Size.X, item.Width)).IsTrue();
            AssertBool(Near(shape.Size.Y, item.Height)).IsTrue();
            AssertBool(Near(shape.Size.Z, item.Depth)).IsTrue();
        }
        finally
        {
            instance.Free();
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
