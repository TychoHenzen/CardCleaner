using System;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>The floor areas furniture may be placed on. Pure data.</summary>
public static class FurnitureFloor
{
    /// <summary>The inset of an owned lot's floor from the lot's storefront-part minimum x, in metres.</summary>
    private const float LotInset = 0.5f;

    // ASSUMPTION (Low): the storefront floor inset 0.5 m from its walls.
    /// <summary>The storefront floor: Storefront/Floor is 16 x 12 m centred at x 8, z 6, inset 0.5 m.</summary>
    public static readonly FurnitureRect Shop = new(0.5f, 0.5f, 15.5f, 11.5f);

    /// <summary>
    ///     The floor for a shop with the given lots owned: <see cref="Shop" /> with its minimum x pulled out to the
    ///     furthest owned lot's floor edge. Only the minimum x changes; the other three edges stay as they are.
    /// </summary>
    public static FurnitureRect For(ExpansionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var minX = Shop.MinX;
        foreach (var lot in plan.Lots)
        {
            if (plan.Owned.Contains(lot.Id))
                minX = Math.Min(minX, lot.StorefrontMinX + LotInset);
        }

        return Shop with { MinX = minX };
    }
}
