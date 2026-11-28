using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Shelves;

namespace CardCleaner.Scripts.Features.Shop.Restocking;

/// <summary>
///     Picks the restocker's next step from the stock and the shelf levels with room (<see cref="FreeSlot" />). A pack
///     or booster that fits a product-shelf level goes first, onto a part-filled level of the same product before an
///     empty level; otherwise a box is unpacked onto the emptiest product shelf once enough seats are free for its
///     booster boxes (<see cref="FreeSlotsToUnpack" />, <see cref="SeatsForBoxContents" />); otherwise the restocker
///     idles. Pure, so it is tested without a scene.
/// </summary>
public static class RestockPlanner
{
    public static RestockStep Plan(
        IReadOnlyList<StockSource> sources,
        IReadOnlyList<FreeSlot> slots,
        IReadOnlySet<(int Source, int Target)> blocked,
        Func<int, int, float> sourceToSlotDistance,
        int boxUnpackThreshold = 8,
        IReadOnlySet<(int Source, int Shelf)>? unreachable = null)
    {
        unreachable ??= new HashSet<(int Source, int Shelf)>();
        var productSlots = slots.Where(s => s.Shelf == ShelfKind.ProductShelf).ToList();
        var move = PlanMove(sources, productSlots, blocked, unreachable, sourceToSlotDistance);
        if (move != null)
        {
            return move;
        }

        return PlanUnpack(sources, productSlots, blocked, unreachable, boxUnpackThreshold) ?? RestockStep.Idle;
    }

    // ASSUMPTION: a partly emptied box is unpacked as soon as its rest fits (Risk Low); the threshold only keeps a
    // full box from being opened for a slot or two.
    /// <summary>Free product-shelf seats needed to unpack a box of <paramref name="products" /> items.</summary>
    public static int FreeSlotsToUnpack(int threshold, int products) => Math.Max(1, Math.Min(threshold, products));

    /// <summary>
    ///     The seats a box's booster boxes can go into across <paramref name="levels" />: the empty seats of every
    ///     product-shelf level that takes a booster box. A booster box fills a seat alone, and a level holds one kind,
    ///     so the free seats of a level of boosters do not count.
    /// </summary>
    public static int SeatsForBoxContents(IEnumerable<FreeSlot> levels) =>
        levels.Where(level => level.Shelf == ShelfKind.ProductShelf && level.Takes(StockKind.Pack))
            .Sum(level => level.EmptySeats);

    private static RestockStep? PlanMove(
        IReadOnlyList<StockSource> sources,
        List<FreeSlot> productSlots,
        IReadOnlySet<(int Source, int Target)> blocked,
        IReadOnlySet<(int Source, int Shelf)> unreachable,
        Func<int, int, float> sourceToSlotDistance)
    {
        // A pack or booster tops up a part-filled level of its own product before it takes an empty level, so empty
        // levels stay open for other products (shelf-levels spec).
        var best = (from source in sources
                where source.Kind is StockKind.Pack or StockKind.Booster
                from slot in productSlots
                where slot.Takes(source.Kind) && !blocked.Contains((source.Id, slot.Id))
                      && !unreachable.Contains((source.Id, slot.ShelfId))
                orderby source.DistanceFromRestocker, slot.IsEmpty, sourceToSlotDistance(source.Id, slot.Id), slot.Id,
                    source.Id
                select (source, slot))
            .Select(pair => ((StockSource source, FreeSlot slot)?)pair)
            .FirstOrDefault();
        return best is { } pair
            ? new RestockStep(RestockStepKind.Move, pair.source.Id, pair.slot.Id, pair.slot.ShelfId)
            : null;
    }

    private static RestockStep? PlanUnpack(
        IReadOnlyList<StockSource> sources,
        List<FreeSlot> productSlots,
        IReadOnlySet<(int Source, int Target)> blocked,
        IReadOnlySet<(int Source, int Shelf)> unreachable,
        int threshold)
    {
        // A box holds booster boxes, which fill a seat alone, so only seats a booster box can take count.
        var open = productSlots.Where(s => s.Takes(StockKind.Pack)).ToList();
        var seats = SeatsForBoxContents(open);
        var shelves = open.GroupBy(s => s.ShelfId)
            .OrderByDescending(g => g.Sum(s => s.EmptySeats))
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .ToList();
        var best = (from box in sources
                where box.Kind == StockKind.Box
                      && seats >= FreeSlotsToUnpack(threshold, box.Products)
                from shelfId in shelves
                where !blocked.Contains((box.Id, shelfId)) && !unreachable.Contains((box.Id, shelfId))
                orderby box.DistanceFromRestocker, box.Id, shelves.IndexOf(shelfId)
                select (box, shelfId))
            .Select(pair => ((StockSource box, int shelfId)?)pair)
            .FirstOrDefault();
        return best is { } pair
            ? new RestockStep(RestockStepKind.UnpackBox, pair.box.Id, pair.shelfId, pair.shelfId)
            : null;
    }
}
