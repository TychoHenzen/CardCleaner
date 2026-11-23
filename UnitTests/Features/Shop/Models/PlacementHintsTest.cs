using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Furniture;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
public class PlacementHintsTest
{
    [TestCase]
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
    public static void ABlockedPoseSaysItCannotBePlaced()
    {
        AssertThat(PlacementHints.Carrying(valid: false)[1].Action).IsEqual("Cannot place here");
    }

    [TestCase]
    public static void AnAimedPieceOffersAMoveAndAPackOnlyWhenEmpty()
    {
        AssertThat(Lines(PlacementHints.Aimed("Card Shelf", canMove: true))).IsEqual(new[]
        {
            "G: Move this card shelf",
            "B: Pack this card shelf into its box",
        });
        AssertThat(Lines(PlacementHints.Aimed("Card Shelf", canMove: false))).IsEqual(new[]
        {
            "G: Empty this card shelf to move or pack it",
        });
    }

    private static string[] Lines(IEnumerable<ControlHint> hints) =>
        hints.Select(hint => $"{hint.Key}: {hint.Action}").ToArray();
}
