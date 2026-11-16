using System;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>The shop's rooms as furniture floor, each inset 0.5 m from its walls. Pure data.</summary>
public static class FurnitureFloor
{
    /// <summary>The inset of a room's furniture floor from its walls, in metres.</summary>
    private const float WallInset = 0.5f;

    // ASSUMPTION (Low): every room's floor is inset 0.5 m from its walls, so a piece never touches a wall.
    /// <summary>The storefront: Storefront/Floor is 16 x 12 m at x 0..16, z 0..12.</summary>
    public static readonly FurnitureRect Storefront = new(WallInset, WallInset, 16f - WallInset, 12f - WallInset);

    /// <summary>The storage and backoffice strip behind the storefront: x 0..24, z -8..0.</summary>
    public static readonly FurnitureRect BackRoom = new(WallInset, -8f + WallInset, 24f - WallInset, -WallInset);

    /// <summary>
    ///     The right storage arm beside the storefront: x 16..24, z -8..12 (it opens into the back room).
    /// </summary>
    public static readonly FurnitureRect RightArm =
        new(16f + WallInset, -8f + WallInset, 24f - WallInset, 12f - WallInset);

    /// <summary>The floor of the shop with no expansion lot owned.</summary>
    public static FurnitureFloorArea Shop { get; } = new([Storefront, BackRoom, RightArm]);

    /// <summary>
    ///     The floor for a shop with the given lots owned: the storefront's minimum x is pulled out to the furthest
    ///     owned lot's floor edge, since an owned lot opens the storefront's left wall. The other rooms stay as they
    ///     are.
    /// </summary>
    public static FurnitureFloorArea For(ExpansionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var minX = Storefront.MinX;
        foreach (var lot in plan.Lots)
        {
            if (plan.Owned.Contains(lot.Id))
                minX = Math.Min(minX, lot.StorefrontMinX + WallInset);
        }

        return new FurnitureFloorArea([Storefront with { MinX = minX }, BackRoom, RightArm]);
    }
}
