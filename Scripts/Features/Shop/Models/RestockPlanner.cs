using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     Picks the restocker's next step from the stock and the free slots. A pack or booster that fits a free
///     product-shelf slot goes first; otherwise a box is unpacked onto the emptiest product shelf once enough slots
///     are free; otherwise the restocker idles. Pure, so it is tested without a scene.
/// </summary>
public static class RestockPlanner
{
    public static RestockStep Plan(
        IReadOnlyList<StockSource> sources,
        IReadOnlyList<FreeSlot> slots,
        IReadOnlySet<(int Source, int Target)> blocked,
        Func<int, int, float> sourceToSlotDistance,
        int boxUnpackThreshold = 8)
    {
        var productSlots = slots.Where(s => s.Shelf == ShelfKind.ProductShelf).ToList();
        var move = PlanMove(sources, productSlots, blocked, sourceToSlotDistance);
        if (move != null)
        {
            return move;
        }

        return PlanUnpack(sources, productSlots, blocked, boxUnpackThreshold) ?? RestockStep.Idle;
    }

    private static RestockStep? PlanMove(
        IReadOnlyList<StockSource> sources,
        List<FreeSlot> productSlots,
        IReadOnlySet<(int Source, int Target)> blocked,
        Func<int, int, float> sourceToSlotDistance)
    {
        var best = (from source in sources
                where source.Kind is StockKind.Pack or StockKind.Booster
                from slot in productSlots
                where ShelfPlacementRule.Accepts(slot.Shelf, source.Kind) && !blocked.Contains((source.Id, slot.Id))
                orderby source.DistanceFromRestocker, sourceToSlotDistance(source.Id, slot.Id), slot.Id, source.Id
                select (source, slot))
            .Cast<(StockSource source, FreeSlot slot)?>()
            .FirstOrDefault();
        return best is { } pair ? new RestockStep(RestockStepKind.Move, pair.source.Id, pair.slot.Id) : null;
    }

    private static RestockStep? PlanUnpack(
        IReadOnlyList<StockSource> sources,
        List<FreeSlot> productSlots,
        IReadOnlySet<(int Source, int Target)> blocked,
        int threshold)
    {
        if (productSlots.Count < threshold)
        {
            return null;
        }

        var shelves = productSlots.GroupBy(s => s.ShelfId)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .ToList();
        var best = (from box in sources
                where box.Kind == StockKind.Box
                from shelfId in shelves
                where !blocked.Contains((box.Id, shelfId))
                orderby box.DistanceFromRestocker, box.Id, shelves.IndexOf(shelfId)
                select (box, shelfId))
            .Cast<(StockSource box, int shelfId)?>()
            .FirstOrDefault();
        return best is { } pair ? new RestockStep(RestockStepKind.UnpackBox, pair.box.Id, pair.shelfId) : null;
    }
}
