using CardCleaner.Scripts.Features.Deckbuilder.Controllers;

namespace CardCleaner.Tests.Features.Deckbuilder.Controllers;

/// <summary>
/// The cabinet's map kind toggle: it works only while the cabinet is idle and flips between the two kinds.
/// </summary>
[TestSuite]
public class MapKindToggleTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void ToggleWorksOnlyWhileNoSessionRunsAndNoMapAwaitsItsReset()
    {
        AssertBool(MapKindToggle.CanToggle(sessionRunning: false, awaitingReset: false)).IsTrue();
        AssertBool(MapKindToggle.CanToggle(sessionRunning: true, awaitingReset: false)).IsFalse();
        AssertBool(MapKindToggle.CanToggle(sessionRunning: false, awaitingReset: true)).IsFalse();
        AssertBool(MapKindToggle.CanToggle(sessionRunning: true, awaitingReset: true)).IsFalse();
    }

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
