using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class ShelfPlacementRuleTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void EveryShelfAndStockPairFollowsTheTable()
    {
        var table = new (ShelfKind Shelf, StockKind Stock, bool Expected)[]
        {
            (ShelfKind.CardShelf, StockKind.Card, true),
            (ShelfKind.CardShelf, StockKind.Booster, false),
            (ShelfKind.CardShelf, StockKind.Pack, false),
            (ShelfKind.CardShelf, StockKind.Box, false),
            (ShelfKind.ProductShelf, StockKind.Card, false),
            (ShelfKind.ProductShelf, StockKind.Booster, true),
            (ShelfKind.ProductShelf, StockKind.Pack, true),
            (ShelfKind.ProductShelf, StockKind.Box, false),
            (ShelfKind.StorageRack, StockKind.Card, false),
            (ShelfKind.StorageRack, StockKind.Booster, true),
            (ShelfKind.StorageRack, StockKind.Pack, true),
            (ShelfKind.StorageRack, StockKind.Box, true),
        };

        foreach (var (shelf, stock, expected) in table)
            AssertThat(ShelfPlacementRule.Accepts(shelf, stock)).IsEqual(expected);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AProductShelfSlotHoldsOnePackOrFourBoosters()
    {
        AssertThat(ShelfPlacementRule.StackLimit(ShelfKind.ProductShelf, StockKind.Pack)).IsEqual(1);
        AssertThat(ShelfPlacementRule.StackLimit(ShelfKind.ProductShelf, StockKind.Booster)).IsEqual(4);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OtherSlotsAreLimitedOnlyByTheirCapacity()
    {
        AssertThat(ShelfPlacementRule.StackLimit(ShelfKind.CardShelf, StockKind.Card)).IsNull();
        AssertThat(ShelfPlacementRule.StackLimit(ShelfKind.StorageRack, StockKind.Box)).IsNull();
        AssertThat(ShelfPlacementRule.StackLimit(ShelfKind.StorageRack, StockKind.Booster)).IsNull();
    }
}
