using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class RestockPlannerTest
{
    private static readonly IReadOnlySet<(int Source, int Target)> NoBlocks = new HashSet<(int Source, int Target)>();

    [TestCase]
    [TestCategory("Unit")]
    public static void Pack_beats_box()
    {
        var sources = new[] { Src(1, StockKind.Box, 1f), Src(2, StockKind.Pack, 9f) };

        var step = Plan(sources, ProductSlots(1, 8));

        AssertThat(step).IsEqual(new RestockStep(RestockStepKind.Move, 2, 0));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Booster_and_pack_both_move()
    {
        AssertThat(Plan(new[] { Src(1, StockKind.Booster, 1f) }, ProductSlots(1, 1)).Kind)
            .IsEqual(RestockStepKind.Move);
        AssertThat(Plan(new[] { Src(1, StockKind.Pack, 1f) }, ProductSlots(1, 1)).Kind)
            .IsEqual(RestockStepKind.Move);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Nearest_source_goes_first()
    {
        var sources = new[] { Src(1, StockKind.Pack, 5f), Src(2, StockKind.Pack, 2f) };

        AssertThat(Plan(sources, ProductSlots(1, 1)).SourceId).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Equal_distance_picks_the_shorter_source_to_slot_distance_then_the_lowest_slot_id()
    {
        var sources = new[] { Src(1, StockKind.Pack, 3f) };
        var slots = ProductSlots(1, 3);

        var shorter = RestockPlanner.Plan(sources, slots, NoBlocks, (_, slot) => slot == 2 ? 1f : 5f);
        var tied = RestockPlanner.Plan(sources, slots, NoBlocks, (_, _) => 4f);

        AssertThat(shorter.TargetId).IsEqual(2);
        AssertThat(tied.TargetId).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Blocked_pair_is_skipped_and_the_next_pair_is_chosen()
    {
        var sources = new[] { Src(1, StockKind.Pack, 1f), Src(2, StockKind.Pack, 2f) };
        var blocked = new HashSet<(int Source, int Target)> { (1, 0), (1, 1) };

        var step = RestockPlanner.Plan(sources, ProductSlots(1, 2), blocked, (_, _) => 0f);

        AssertThat(step).IsEqual(new RestockStep(RestockStepKind.Move, 2, 0));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Card_shelf_and_storage_rack_slots_are_ignored()
    {
        var slots = new[] { new FreeSlot(0, 1, ShelfKind.CardShelf), new FreeSlot(1, 2, ShelfKind.StorageRack) };

        AssertThat(Plan(new[] { Src(1, StockKind.Pack, 1f) }, slots)).IsEqual(RestockStep.Idle);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Box_with_seven_free_product_slots_is_Idle()
    {
        AssertThat(Plan(new[] { Src(1, StockKind.Box, 1f) }, ProductSlots(1, 7))).IsEqual(RestockStep.Idle);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Box_with_eight_free_product_slots_unpacks_onto_the_shelf_with_the_most_free_slots()
    {
        var step = Plan(new[] { Src(7, StockKind.Box, 1f) }, TwoShelves());

        AssertThat(step).IsEqual(new RestockStep(RestockStepKind.UnpackBox, 7, 2));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Blocked_box_and_shelf_pair_is_skipped()
    {
        var blocked = new HashSet<(int Source, int Target)> { (7, 2) };

        var step = RestockPlanner.Plan(new[] { Src(7, StockKind.Box, 1f) }, TwoShelves(), blocked, (_, _) => 0f);

        AssertThat(step).IsEqual(new RestockStep(RestockStepKind.UnpackBox, 7, 1));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Empty_inputs_are_Idle()
    {
        AssertThat(Plan(Array.Empty<StockSource>(), Array.Empty<FreeSlot>())).IsEqual(RestockStep.Idle);
    }

    private static RestockStep Plan(IReadOnlyList<StockSource> sources, IReadOnlyList<FreeSlot> slots) =>
        RestockPlanner.Plan(sources, slots, NoBlocks, (_, _) => 0f);

    private static StockSource Src(int id, StockKind kind, float distance) => new(id, kind, true, distance);

    private static List<FreeSlot> ProductSlots(int shelfId, int count, int firstId = 0) =>
        Enumerable.Range(firstId, count).Select(i => new FreeSlot(i, shelfId, ShelfKind.ProductShelf)).ToList();

    // Shelf 1 has 3 free slots and shelf 2 has 5, so 8 are free in all.
    private static List<FreeSlot> TwoShelves() => ProductSlots(1, 3).Concat(ProductSlots(2, 5, 10)).ToList();
}
