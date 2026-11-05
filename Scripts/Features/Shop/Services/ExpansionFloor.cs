using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>The one place that turns the shop's expansion state into the floor furniture may stand on.</summary>
public static class ExpansionFloor
{
    /// <summary>The floor for the owned lots, or the base storefront floor with no expansion service.</summary>
    public static FurnitureRect For(IShopExpansion? expansion) =>
        expansion is null ? FurnitureFloor.Shop : FurnitureFloor.For(expansion.Plan);
}
