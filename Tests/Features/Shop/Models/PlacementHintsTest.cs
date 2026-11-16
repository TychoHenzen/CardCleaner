using System.Linq;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class PlacementHintsTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void CarryingShowsTurnPlaceAndCancel()
    {
        var hints = PlacementHints.Carrying(valid: true);

        AssertThat(hints.Select(hint => $"{hint.Key}: {hint.Action}").ToArray()).IsEqual(new[]
        {
            "T: Turn",
            "E: Place here",
            "C: Cancel",
        });
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ABlockedPoseSaysItCannotBePlaced()
    {
        AssertThat(PlacementHints.Carrying(valid: false)[1].Action).IsEqual("Cannot place here");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AnAimedPieceOffersAMoveOnlyWhenEmpty()
    {
        AssertThat(PlacementHints.Aimed("Card Shelf", canMove: true)[0].Action).IsEqual("Move this card shelf");
        AssertThat(PlacementHints.Aimed("Card Shelf", canMove: false)[0].Action)
            .IsEqual("Empty this card shelf to move it");
    }
}
