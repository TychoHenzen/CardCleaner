using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class StaffCatalogTest
{
    private const string ShippedCatalogPath =
        "res://Data/Shop/StaffCatalog.tres";

    [TestCase]
    [TestCategory("Unit")]
    public static void Shipped_catalog_has_the_cashier_then_the_restocker_with_default_values()
    {
        var roles = GD.Load<StaffCatalog>(ShippedCatalogPath).ToRoles();

        AssertThat(roles.Count).IsEqual(2);
        AssertThat(roles.Keys.ToList())
            .IsEqual(new List<string> { StaffDefaults.CashierId, StaffDefaults.RestockerId });
        AssertThat(roles[StaffDefaults.CashierId]).IsEqual(StaffDefaults.Cashier());
        AssertThat(roles[StaffDefaults.RestockerId]).IsEqual(StaffDefaults.Restocker());
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Repeated_and_empty_ids_are_skipped()
    {
        var catalog = new StaffCatalog
        {
            Roles = new[]
            {
                Role("cashier", "Cashier"),
                Role("cashier", "Second cashier"),
                Role("", "Nameless"),
                Role("   ", "Blank"),
                Role("stocker", "Stocker"),
            },
        };

        var roles = catalog.ToRoles();

        AssertThat(roles.Count).IsEqual(2);
        AssertThat(roles["cashier"].DisplayName).IsEqual("Cashier");
        AssertThat(roles["stocker"].DisplayName).IsEqual("Stocker");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Empty_catalog_gives_no_roles()
    {
        var roles = new StaffCatalog().ToRoles();

        AssertThat(roles.Count).IsEqual(0);
    }

    private static StaffRoleData Role(string id, string name) => new()
    {
        Id = id,
        DisplayName = name,
        UnlockId = "staff_" + id,
        HireFee = 10,
        DailyWage = 1,
        SecondsPerScan = 1f,
        SecondsPerPayment = 1f,
        TakeOverDelay = 1f,
    };
}
