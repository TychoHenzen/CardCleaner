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
        var hints = ContainerHints.For(CardContainerKind.Pack, lidOpen: true);

        AssertThat(hints[0].Action).IsEqual("Close the booster box");
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
