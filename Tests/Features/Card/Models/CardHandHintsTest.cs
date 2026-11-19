using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Tests.Features.Card.Models;

[TestSuite]
[RequireGodotRuntime]
public class CardHandHintsTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void AnEmptyHandHasNoHints()
    {
        AssertThat(CardHandHints.For(0, aimingDrop: false).Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AHandOfCardsShowsTheDropAndStrapKeysAndTheCount()
    {
        var hints = CardHandHints.For(3, aimingDrop: false);

        AssertThat(hints.Select(hint => hint.Key).ToArray()).IsEqual(new[] { "RMB", "X", "V" });
        AssertThat(hints[0].Action).Contains("3 cards in hand");
        AssertThat(hints[2].Action).IsEqual("Strap the cards into a deck");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OneCardIsNotStrapped()
    {
        var hints = CardHandHints.For(1, aimingDrop: false);

        AssertThat(hints.Select(hint => hint.Key).ToArray()).IsEqual(new[] { "RMB", "X" });
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AimingADropShowsOnlyTheRelease()
    {
        var hints = CardHandHints.For(1, aimingDrop: true);

        AssertThat(hints.Count).IsEqual(1);
        AssertThat(hints[0].Action).IsEqual("Set the card down");
    }
}
