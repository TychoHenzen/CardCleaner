using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Tests.Features.Packs.Models;

[TestSuite]
public class CardContainerLayoutTest
{
    [TestCase]
    public static void EveryContainerHoldsEightItems()
    {
        AssertThat(CardContainerLayout.ItemsPerContainer).IsEqual(8);
    }

    [TestCase]
    public static void BoxHoldsFiveHundredAndTwelveCards()
    {
        AssertThat(CardContainerLayout.CardsPerBox).IsEqual(512);
    }
}
