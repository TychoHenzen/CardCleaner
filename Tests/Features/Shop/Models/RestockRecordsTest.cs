using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class RestockRecordsTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void Records_compare_by_value()
    {
        AssertThat(new StockSource(1, StockKind.Pack, true, 2f)).IsEqual(new StockSource(1, StockKind.Pack, true, 2f));
        AssertThat(new StockSource(1, StockKind.Pack, true, 2f))
            .IsNotEqual(new StockSource(2, StockKind.Pack, true, 2f));
        AssertThat(new FreeSlot(3, 4, ShelfKind.ProductShelf)).IsEqual(new FreeSlot(3, 4, ShelfKind.ProductShelf));
        AssertThat(new FreeSlot(3, 4, ShelfKind.ProductShelf)).IsNotEqual(new FreeSlot(3, 5, ShelfKind.ProductShelf));
        AssertThat(new RestockStep(RestockStepKind.Move, 1, 2)).IsEqual(new RestockStep(RestockStepKind.Move, 1, 2));
        AssertThat(new RestockStep(RestockStepKind.Move, 1, 2)).IsNotEqual(new RestockStep(RestockStepKind.Move, 2, 1));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Idle_step_has_kind_Idle_and_ids_of_minus_one()
    {
        AssertThat(RestockStep.Idle.Kind).IsEqual(RestockStepKind.Idle);
        AssertThat(RestockStep.Idle.SourceId).IsEqual(-1);
        AssertThat(RestockStep.Idle.TargetId).IsEqual(-1);
    }
}
