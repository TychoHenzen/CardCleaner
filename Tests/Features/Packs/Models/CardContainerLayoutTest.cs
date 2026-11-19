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
    public static void BoxHoldsFiveHundredAndTwelveCards()
    {
        AssertThat(CardContainerLayout.CardsPerBox).IsEqual(512);
    }
}
