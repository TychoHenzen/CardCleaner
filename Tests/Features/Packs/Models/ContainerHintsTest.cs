using System.Linq;
using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Tests.Features.Packs.Models;

[TestSuite]
[RequireGodotRuntime]
public class ContainerHintsTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void AShutBoxOpensWithTheRightButtonAndQPutsItDown()
    {
        var hints = ContainerHints.For(CardContainerKind.Box, lidOpen: false);

        AssertThat(hints.Select(hint => $"{hint.Key}: {hint.Action}").ToArray()).IsEqual(new[]
        {
            "RMB: Open the cardboard box",
            "Q: Put it down",
        });
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AnOpenBoxClosesWithTheRightButton()
    {
        var hints = ContainerHints.For(CardContainerKind.Box, lidOpen: true);

        AssertThat(hints[0].Action).IsEqual("Close the cardboard box");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AnOpenCardboardBoxSetsOneItemDownWithTheLeftButton()
    {
        var hints = ContainerHints.For(CardContainerKind.Box, lidOpen: true, itemsInside: 3);

        AssertThat(hints.Select(hint => $"{hint.Key}: {hint.Action}").ToArray()).IsEqual(new[]
        {
            "RMB: Close the cardboard box",
            "Q: Put it down",
            "LMB: Put one on the floor",
        });
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AnEmptyOpenCardboardBoxGoesInATrashCan()
    {
        var hints = ContainerHints.For(CardContainerKind.Box, lidOpen: true, itemsInside: 0);

        AssertThat(hints[^1].Action).IsEqual("Throw it in a trash can");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ABoosterBoxEmptiesIntoTheHandWithTheRightButton()
    {
        var hints = ContainerHints.For(CardContainerKind.Pack, lidOpen: false, itemsInside: 8);

        AssertThat(hints.Select(hint => $"{hint.Key}: {hint.Action}").ToArray()).IsEqual(new[]
        {
            "RMB: Empty the booster box into your hand",
            "Q: Put it down",
        });
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AHandOfBoosterPacksOpensOneAndPutsOneDown()
    {
        var hints = ContainerHints.For(CardContainerKind.Booster, lidOpen: false, held: 3);

        AssertThat(hints.Select(hint => $"{hint.Key}: {hint.Action}").ToArray()).IsEqual(new[]
        {
            "RMB: Open one of the 3 booster packs",
            "Q: Put one down",
        });
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ABoosterPackOpensWithTheRightButton()
    {
        var hints = ContainerHints.For(CardContainerKind.Booster, lidOpen: false);

        AssertThat(hints[0].Action).IsEqual("Open the booster pack");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryKindHasTheNameThePlayerReads()
    {
        AssertThat(CardContainerKindText.NameOf(CardContainerKind.Box)).IsEqual("cardboard box");
        AssertThat(CardContainerKindText.NameOf(CardContainerKind.Pack)).IsEqual("booster box");
        AssertThat(CardContainerKindText.NameOf(CardContainerKind.Booster)).IsEqual("booster pack");
    }
}
