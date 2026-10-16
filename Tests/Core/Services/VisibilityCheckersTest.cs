using CardCleaner.Scripts.Core.Services;
using CardCleaner.Tests.TestUtilities.Fixtures;
using Godot;

namespace CardCleaner.Tests.Core.Services;

[TestSuite]
[RequireGodotRuntime]
public class VisibilityCheckersTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void CreateSimpleReturnsANewSimpleVisibilityCheckerEachCall()
    {
        var first = VisibilityCheckers.CreateSimple();
        var second = VisibilityCheckers.CreateSimple();

        AssertBool(first is SimpleVisibilityChecker).IsTrue();
        AssertBool(ReferenceEquals(first, second)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CreateSimpleCheckerSeesTheCellItStandsOn()
    {
        var (_, gridData) = OpenFloorMap.Create(5, 5);
        var cell = gridData.PositionToCellId(new Vector2I(2, 2));

        AssertBool(VisibilityCheckers.CreateSimple().CanSee(cell, cell, gridData)).IsTrue();
    }
}
