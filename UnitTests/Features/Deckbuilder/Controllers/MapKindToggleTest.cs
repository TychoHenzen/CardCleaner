using CardCleaner.Scripts.Features.Deckbuilder.Controllers;

namespace CardCleaner.Tests.Features.Deckbuilder.Controllers;

/// <summary>
/// The cabinet's map kind toggle: it flips between the two kinds and names the picked one. When the toggle may be
/// pressed is covered on the controller by DeckBuilderControllerGridSessionTest.
/// </summary>
[TestSuite]
public class MapKindToggleTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void EachKindTogglesToTheOther()
    {
        AssertThat(MapKindToggle.Next(MapGenerationType.RegularGrid)).IsEqual(MapGenerationType.IrregularMesh);
        AssertThat(MapKindToggle.Next(MapGenerationType.IrregularMesh)).IsEqual(MapGenerationType.RegularGrid);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EachKindHasItsOwnStatusText()
    {
        AssertThat(MapKindToggle.Describe(MapGenerationType.RegularGrid)).IsEqual("Map: grid");
        AssertThat(MapKindToggle.Describe(MapGenerationType.IrregularMesh)).IsEqual("Map: irregular");
    }
}
