using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
public class ShelfPlacementRuleTest
{
    [TestCase]
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
    public static void AProductShelfSlotHoldsOnePackOrFourBoosters()
    {
        AssertThat(ShelfPlacementRule.StackLimit(ShelfKind.ProductShelf, StockKind.Pack)).IsEqual(1);
        AssertThat(ShelfPlacementRule.StackLimit(ShelfKind.ProductShelf, StockKind.Booster)).IsEqual(4);
    }

    [TestCase]
    public static void AnEmptyLevelTakesAnyStockItsShelfTakes()
    {
        AssertBool(ShelfPlacementRule.LevelTakes(ShelfKind.ProductShelf, null, StockKind.Booster)).IsTrue();
        AssertBool(ShelfPlacementRule.LevelTakes(ShelfKind.ProductShelf, null, StockKind.Pack)).IsTrue();
        AssertBool(ShelfPlacementRule.LevelTakes(ShelfKind.StorageRack, null, StockKind.Box)).IsTrue();
    }

    [TestCase]
    public static void ALevelHoldsOneKindOfStock()
    {
        AssertBool(ShelfPlacementRule.LevelTakes(ShelfKind.ProductShelf, StockKind.Booster, StockKind.Pack)).IsFalse();
        AssertBool(ShelfPlacementRule.LevelTakes(ShelfKind.ProductShelf, StockKind.Booster, StockKind.Booster))
            .IsTrue();
        AssertBool(ShelfPlacementRule.LevelTakes(ShelfKind.StorageRack, StockKind.Box, StockKind.Pack)).IsFalse();
    }

    [TestCase]
    public static void TheShelfKindStillLimitsALevel()
    {
        AssertBool(ShelfPlacementRule.LevelTakes(ShelfKind.ProductShelf, null, StockKind.Box)).IsFalse();
        AssertBool(ShelfPlacementRule.LevelTakes(ShelfKind.CardShelf, null, StockKind.Booster)).IsFalse();
    }

    [TestCase]
    public static void TheFirstItemOnAProductShelfLevelKeepsItsOwnPrice()
    {
        AssertThat(ShelfPlacementRule.PriceOnPut(ShelfKind.ProductShelf, true, null, 9)).IsEqual(9);
        AssertThat(ShelfPlacementRule.PriceOnPut(ShelfKind.ProductShelf, true, 12, null)).IsNull();
    }

    [TestCase]
    public static void ALaterItemOnAProductShelfLevelTakesTheLevelsPrice()
    {
        AssertThat(ShelfPlacementRule.PriceOnPut(ShelfKind.ProductShelf, false, 12, 9)).IsEqual(12);
        AssertThat(ShelfPlacementRule.PriceOnPut(ShelfKind.ProductShelf, false, null, 9)).IsNull();
    }

    [TestCase]
    public static void CardsAndRackStockKeepTheirOwnPrices()
    {
        AssertThat(ShelfPlacementRule.PriceOnPut(ShelfKind.CardShelf, false, 40, 5)).IsEqual(5);
        AssertThat(ShelfPlacementRule.PriceOnPut(ShelfKind.StorageRack, false, 40, 5)).IsEqual(5);
    }

    [TestCase]
    public static void OtherSlotsAreLimitedOnlyByTheirCapacity()
    {
        AssertThat(ShelfPlacementRule.StackLimit(ShelfKind.CardShelf, StockKind.Card)).IsNull();
        AssertThat(ShelfPlacementRule.StackLimit(ShelfKind.StorageRack, StockKind.Box)).IsNull();
        AssertThat(ShelfPlacementRule.StackLimit(ShelfKind.StorageRack, StockKind.Booster)).IsNull();
    }
}
