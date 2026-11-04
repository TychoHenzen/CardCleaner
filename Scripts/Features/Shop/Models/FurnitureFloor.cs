namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>The floor areas furniture may be placed on. Pure data.</summary>
public static class FurnitureFloor
{
    // ASSUMPTION (Low): the storefront floor inset 0.5 m from its walls; #406 widens it.
    /// <summary>The storefront floor: Storefront/Floor is 16 x 12 m centred at x 8, z 6, inset 0.5 m.</summary>
    public static readonly FurnitureRect Shop = new(0.5f, 0.5f, 15.5f, 11.5f);
}
