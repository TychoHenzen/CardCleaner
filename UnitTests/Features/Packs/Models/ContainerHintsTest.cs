using System.Linq;
using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Tests.Features.Packs.Models;

[TestSuite]
public class ContainerHintsTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void AtAShelfQUnpacksTheContents()
    {
        var hints = ContainerHints.For(CardContainerKind.Box, atShelf: true);

        AssertThat(hints.Select(hint => $"{hint.Key}: {hint.Action}").ToArray()).IsEqual(new[]
        {
            "Q: Unpack the packs onto this shelf",
            "RMB: Put it down",
        });
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AwayFromAShelfQOpensTheContainer()
    {
        var hints = ContainerHints.For(CardContainerKind.Booster, atShelf: false);

        AssertThat(hints[0].Action).IsEqual("Open the booster");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryKindHasAName()
    {
        AssertThat(CardContainerKindText.NameOf(CardContainerKind.Box)).IsEqual("booster box");
        AssertThat(CardContainerKindText.NameOf(CardContainerKind.Pack)).IsEqual("pack");
        AssertThat(CardContainerKindText.ContentsOf(CardContainerKind.Pack)).IsEqual("boosters");
        AssertThat(CardContainerKindText.ContentsOf(CardContainerKind.Booster)).IsEqual("cards");
    }
}
