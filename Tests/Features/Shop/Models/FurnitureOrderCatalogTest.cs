using System.Linq;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Models;

/// <summary>
///     The furniture order catalog is data: each row sells a furniture item, its box scene carries that item, and
///     the row's id has an unlock level so the Furniture app can show it.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FurnitureOrderCatalogTest
{
    private const string OrderCatalogPath = "res://Data/Shop/FurnitureOrderCatalog.tres";
    private const string StockCatalogPath = "res://Data/Shop/BackofficeCatalog.tres";
    private const string FurnitureCatalogPath = "res://Data/Shop/Furniture/FurnitureItems.tres";

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryOrderItemHasAFurnitureItemAndABox()
    {
        var furniture = GD.Load<FurnitureCatalog>(FurnitureCatalogPath);
        var catalog = GD.Load<OrderCatalog>(OrderCatalogPath);
        AssertThat(catalog.Items.Length).IsGreater(0);

        foreach (var order in catalog.Items)
        {
            AssertThat(furniture.Find(order.Id)).IsNotNull();

            var instance = order.Scene!.Instantiate<FurnitureBox>();
            try
            {
                AssertThat(instance.Item).IsNotNull();
                AssertThat(instance.Item!.Id).IsEqual(order.Id);
            }
            finally
            {
                instance.Free();
            }
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryOrderItemHasAnUnlockRow()
    {
        var catalog = GD.Load<OrderCatalog>(OrderCatalogPath);

        foreach (var order in catalog.Items)
            AssertThat(UnlockCatalog.Default.RequiredLevel(order.Id)).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ShelfUnitCostsOneHundredTwenty()
    {
        var order = GD.Load<OrderCatalog>(OrderCatalogPath).Items.Single(item => item.Id == "shelf_unit");

        AssertThat(order.Price).IsEqual(120);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NoFurnitureIdCollidesWithTheStockCatalog()
    {
        var stockIds = GD.Load<OrderCatalog>(StockCatalogPath).Items.Select(item => item.Id).ToHashSet();

        foreach (var order in GD.Load<OrderCatalog>(OrderCatalogPath).Items)
            AssertBool(stockIds.Contains(order.Id)).IsFalse();
    }
}
