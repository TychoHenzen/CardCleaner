using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
public class RestockPlannerTest
{
    private static readonly IReadOnlySet<(int Source, int Target)> NoBlocks = new HashSet<(int Source, int Target)>();

    [TestCase]
    public static void Pack_beats_box()
    {
        var sources = new[] { Src(1, StockKind.Box, 1f), Src(2, StockKind.Pack, 9f) };

        var step = Plan(sources, ProductSlots(1, 8));

        AssertThat(step).IsEqual(new RestockStep(RestockStepKind.Move, 2, 0, 1));
    }

    [TestCase]
    public static void Booster_and_pack_both_move()
    {
        AssertThat(Plan(new[] { Src(1, StockKind.Booster, 1f) }, ProductSlots(1, 1)).Kind)
            .IsEqual(RestockStepKind.Move);
        AssertThat(Plan(new[] { Src(1, StockKind.Pack, 1f) }, ProductSlots(1, 1)).Kind)
            .IsEqual(RestockStepKind.Move);
    }

    [TestCase]
    public static void Nearest_source_goes_first()
    {
        var sources = new[] { Src(1, StockKind.Pack, 5f), Src(2, StockKind.Pack, 2f) };

        AssertThat(Plan(sources, ProductSlots(1, 1)).SourceId).IsEqual(2);
    }

    [TestCase]
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
    public static void Blocked_pair_is_skipped_and_the_next_pair_is_chosen()
    {
        var sources = new[] { Src(1, StockKind.Pack, 1f), Src(2, StockKind.Pack, 2f) };
        var blocked = new HashSet<(int Source, int Target)> { (1, 0), (1, 1) };

        var step = RestockPlanner.Plan(sources, ProductSlots(1, 2), blocked, (_, _) => 0f);

        AssertThat(step).IsEqual(new RestockStep(RestockStepKind.Move, 2, 0, 1));
    }

    [TestCase]
    public static void A_source_skips_every_slot_of_a_shelf_it_cannot_reach()
    {
        var sources = new[] { Src(1, StockKind.Pack, 1f) };
        var unreachable = new HashSet<(int Source, int Shelf)> { (1, 2) };

        var onlyThatShelf = RestockPlanner.Plan(sources, ProductSlots(2, 16), NoBlocks, (_, _) => 0f, 8, unreachable);
        var withAnother = RestockPlanner.Plan(sources, TwoShelves(), NoBlocks, (_, _) => 0f, 8, unreachable);

        AssertThat(onlyThatShelf).IsEqual(RestockStep.Idle);
        AssertThat(withAnother.ShelfId).IsEqual(1);
    }

    [TestCase]
    public static void A_box_is_not_unpacked_onto_a_shelf_it_cannot_reach()
    {
        var unreachable = new HashSet<(int Source, int Shelf)> { (7, 2) };

        var step = RestockPlanner.Plan(
            new[] { Src(7, StockKind.Box, 1f) }, TwoShelves(), NoBlocks, (_, _) => 0f, 8, unreachable);

        AssertThat(step).IsEqual(new RestockStep(RestockStepKind.UnpackBox, 7, 1, 1));
    }

    [TestCase]
    public static void Card_shelf_and_storage_rack_slots_are_ignored()
    {
        var slots = new[] { new FreeSlot(0, 1, ShelfKind.CardShelf), new FreeSlot(1, 2, ShelfKind.StorageRack) };

        AssertThat(Plan(new[] { Src(1, StockKind.Pack, 1f) }, slots)).IsEqual(RestockStep.Idle);
    }

    [TestCase]
    public static void Box_with_seven_free_product_slots_is_Idle()
    {
        AssertThat(Plan(new[] { Src(1, StockKind.Box, 1f) }, ProductSlots(1, 7))).IsEqual(RestockStep.Idle);
    }

    [TestCase]
    public static void Box_with_eight_free_product_slots_unpacks_onto_the_shelf_with_the_most_free_slots()
    {
        var step = Plan(new[] { Src(7, StockKind.Box, 1f) }, TwoShelves());

        AssertThat(step).IsEqual(new RestockStep(RestockStepKind.UnpackBox, 7, 2, 2));
    }

    // A box one shelf took five booster boxes from holds three more: three free slots are enough to finish it.
    [TestCase]
    public static void A_box_holding_fewer_products_than_the_threshold_unpacks_once_they_all_fit()
    {
        var partly = new StockSource(7, StockKind.Box, false, 0f, 3);

        AssertThat(Plan(new[] { partly }, ProductSlots(1, 2))).IsEqual(RestockStep.Idle);
        AssertThat(Plan(new[] { partly }, ProductSlots(1, 3)))
            .IsEqual(new RestockStep(RestockStepKind.UnpackBox, 7, 1, 1));
    }

    [TestCase]
    public static void Blocked_box_and_shelf_pair_is_skipped()
    {
        var blocked = new HashSet<(int Source, int Target)> { (7, 2) };

        var step = RestockPlanner.Plan(new[] { Src(7, StockKind.Box, 1f) }, TwoShelves(), blocked, (_, _) => 0f);

        AssertThat(step).IsEqual(new RestockStep(RestockStepKind.UnpackBox, 7, 1, 1));
    }

    // A customer takes boosters off a stack one at a time, so a product slot can hold part of a stack.
    [TestCase]
    public static void A_booster_tops_up_a_part_filled_booster_stack()
    {
        var slots = new[] { PartStack(0, 1) };

        AssertThat(Plan(new[] { Src(1, StockKind.Booster, 1f) }, slots))
            .IsEqual(new RestockStep(RestockStepKind.Move, 1, 0, 1));
    }

    [TestCase]
    public static void A_booster_box_never_goes_onto_a_booster_stack()
    {
        AssertThat(Plan(new[] { Src(1, StockKind.Pack, 1f) }, new[] { PartStack(0, 1) })).IsEqual(RestockStep.Idle);
    }

    [TestCase]
    public static void A_booster_fills_a_part_filled_stack_before_an_empty_slot()
    {
        var slots = ProductSlots(1, 2);
        slots.Add(PartStack(5, 1));

        AssertThat(Plan(new[] { Src(1, StockKind.Booster, 1f) }, slots).TargetId).IsEqual(5);
    }

    [TestCase]
    public static void A_part_filled_stack_does_not_count_toward_unpacking_a_box()
    {
        var slots = ProductSlots(1, 7);
        slots.Add(PartStack(20, 1));

        AssertThat(Plan(new[] { Src(7, StockKind.Box, 1f) }, slots)).IsEqual(RestockStep.Idle);
    }

    [TestCase]
    public static void Empty_inputs_are_Idle()
    {
        AssertThat(Plan(Array.Empty<StockSource>(), Array.Empty<FreeSlot>())).IsEqual(RestockStep.Idle);
    }

    private static RestockStep Plan(IReadOnlyList<StockSource> sources, IReadOnlyList<FreeSlot> slots) =>
        RestockPlanner.Plan(sources, slots, NoBlocks, (_, _) => 0f);

    // A box is full (eight booster boxes); a booster box or booster is one product.
    private static StockSource Src(int id, StockKind kind, float distance) =>
        new(id, kind, true, distance, kind == StockKind.Box ? 8 : 1);

    // A product slot holding boosters below the stack limit.
    private static FreeSlot PartStack(int id, int shelfId) =>
        new(id, shelfId, ShelfKind.ProductShelf, StockKind.Booster);

    private static List<FreeSlot> ProductSlots(int shelfId, int count, int firstId = 0) =>
        Enumerable.Range(firstId, count).Select(i => new FreeSlot(i, shelfId, ShelfKind.ProductShelf)).ToList();

    // Shelf 1 has 3 free slots and shelf 2 has 5, so 8 are free in all.
    private static List<FreeSlot> TwoShelves() => ProductSlots(1, 3).Concat(ProductSlots(2, 5, 10)).ToList();
}
