using System;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class UnlockCatalogTest
{
    private const string BackofficeCatalogPath = "res://Data/Shop/BackofficeCatalog.tres";
    private const string WorkshopCatalogPath = "res://Data/Workshop/WorkshopCatalog.tres";
    private const string StaffCatalogPath = "res://Data/Shop/StaffCatalog.tres";

    [TestCase]
    [TestCategory("Unit")]
    public static void DefaultRowsGiveTheRequiredLevelOfEachItem()
    {
        var catalog = UnlockCatalog.Default;

        AssertThat(catalog.RequiredLevel("bulk_packing_station")).IsEqual(2);
        AssertThat(catalog.RequiredLevel("expansion_side_wing_1")).IsEqual(2);
        AssertThat(catalog.RequiredLevel("staff_cashier")).IsEqual(2);
        foreach (var id in new[]
                 {
                     "shelf", "shelf_unit", "cardboard_box", "product_shelf", "storage_rack",
                     "conveyor", "arcade_cabinet", "wiring",
                 })
        {
            AssertThat(catalog.RequiredLevel(id)).IsEqual(1);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UnknownAndEmptyIdsHaveNoLevel()
    {
        var catalog = UnlockCatalog.Default;

        AssertThat(catalog.RequiredLevel("nope")).IsNull();
        AssertThat(catalog.RequiredLevel("")).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void DuplicateIdThrows()
    {
        var rows = new[]
        {
            new UnlockEntry("shelf", UnlockKind.Furniture, 1),
            new UnlockEntry("shelf", UnlockKind.Product, 2),
        };

        AssertThrown(() => new UnlockCatalog(rows)).IsInstanceOf<ArgumentException>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void LevelBelowOneThrows()
    {
        var rows = new[] { new UnlockEntry("shelf", UnlockKind.Furniture, 0) };

        AssertThrown(() => new UnlockCatalog(rows)).IsInstanceOf<ArgumentException>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EmptyIdThrows()
    {
        var rows = new[] { new UnlockEntry("  ", UnlockKind.Furniture, 1) };

        AssertThrown(() => new UnlockCatalog(rows)).IsInstanceOf<ArgumentException>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void StaffRoleUnlockIdsHaveRows()
    {
        var catalog = UnlockCatalog.Default;
        var roles = GD.Load<StaffCatalog>(StaffCatalogPath).ToRoles();
        foreach (var role in roles.Values)
        {
            AssertThat(catalog.RequiredLevel(role.UnlockId))
                .OverrideFailureMessage($"staff role '{role.Id}' has no unlock row for '{role.UnlockId}'")
                .IsNotNull();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryItemInTheShippedCatalogsHasAnUnlockRow()
    {
        var catalog = UnlockCatalog.Default;
        foreach (var path in new[] { BackofficeCatalogPath, WorkshopCatalogPath })
        {
            var orderCatalog = GD.Load<OrderCatalog>(path);
            AssertThat(orderCatalog).IsNotNull();
            foreach (var item in orderCatalog.Items)
            {
                AssertThat(catalog.RequiredLevel(item.Id))
                    .OverrideFailureMessage($"{path} item '{item.Id}' has no unlock row")
                    .IsNotNull();
            }
        }
    }
}
