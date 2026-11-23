using CardCleaner.Scripts.Features.Packs.Models;
using Godot;
using CardCleaner.Scripts.Features.Shop.Ordering;

namespace CardCleaner.Tests.Features.Shop.Services;

/// <summary>The wholesale cost of a sealed product is the price of its row in the order catalog.</summary>
[TestSuite]
[RequireGodotRuntime]
public class WholesaleCostsTest
{
    private const string WholesaleCatalog = "res://Data/Shop/BackofficeCatalog.tres";

    [TestCase]
    public void TheWholesaleCostComesFromTheOrderCatalog()
    {
        var costs = AutoFree(new WholesaleCosts { Wholesale = GD.Load<OrderCatalog>(WholesaleCatalog) })!;

        AssertBool(costs.WholesaleFor(CardContainerKind.Booster) == 2).IsTrue();
        AssertBool(costs.WholesaleFor(CardContainerKind.Pack) == 10).IsTrue();
    }

    [TestCase]
    public void WithoutACatalogNothingHasAWholesaleCost()
    {
        var costs = AutoFree(new WholesaleCosts())!;

        AssertBool(costs.WholesaleFor(CardContainerKind.Booster).HasValue).IsFalse();
    }
}
