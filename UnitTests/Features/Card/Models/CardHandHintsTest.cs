using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Tests.Features.Card.Models;

[TestSuite]
public class CardHandHintsTest
{
    [TestCase]
    public static void AnEmptyHandHasNoHints()
    {
        AssertThat(CardHandHints.For(0, aimingDrop: false).Count).IsEqual(0);
    }

    [TestCase]
    public static void AHandOfCardsShowsTheDropKeysAndTheCount()
    {
        var hints = CardHandHints.For(3, aimingDrop: false);

        AssertThat(hints.Select(hint => hint.Key).ToArray()).IsEqual(new[] { "RMB", "X" });
        AssertThat(hints[0].Action).Contains("3 cards in hand");
    }

    [TestCase]
    public static void AimingADropShowsOnlyTheRelease()
    {
        var hints = CardHandHints.For(1, aimingDrop: true);

        AssertThat(hints.Count).IsEqual(1);
        AssertThat(hints[0].Action).IsEqual("Set the card down");
    }
}
