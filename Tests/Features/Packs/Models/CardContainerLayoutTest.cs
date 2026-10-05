using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Tests.Features.Packs.Models;

[TestSuite]
[RequireGodotRuntime]
public class CardContainerLayoutTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void EveryContainerHoldsEightItems()
    {
        AssertThat(CardContainerLayout.ItemsPerContainer).IsEqual(8);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void BoxHoldsPacksPackHoldsBoostersBoosterHoldsCards()
    {
        AssertThat(CardContainerLayout.ChildKind(CardContainerKind.Box)).IsEqual(CardContainerKind.Pack);
        AssertThat(CardContainerLayout.ChildKind(CardContainerKind.Pack)).IsEqual(CardContainerKind.Booster);
        AssertThat(CardContainerLayout.ChildKind(CardContainerKind.Booster)).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void BoxHoldsFiveHundredAndTwelveCards()
    {
        AssertThat(CardContainerLayout.CardsPerBox).IsEqual(512);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void LevelsBelowCountsContainerLevelsUnderEachKind()
    {
        AssertThat(CardContainerLayout.LevelsBelow(CardContainerKind.Booster)).IsEqual(0);
        AssertThat(CardContainerLayout.LevelsBelow(CardContainerKind.Pack)).IsEqual(1);
        AssertThat(CardContainerLayout.LevelsBelow(CardContainerKind.Box)).IsEqual(2);
    }
}
