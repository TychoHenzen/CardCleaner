using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Tests.TestUtilities.Fixtures;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services;

[TestSuite]
[RequireGodotRuntime]
public class ExplorationAIsTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void CreateStartsAnExplorationAtTheGivenCell()
    {
        var (_, gridData) = OpenFloorMap.Create(5, 5, new Vector2I(0, 0));
        var start = gridData.PositionToCellId(new Vector2I(2, 2));

        IExplorationAI ai = ExplorationAIs.Create(gridData, start, VisibilityCheckers.CreateSimple());

        AssertBool(ai is ExplorationAI).IsTrue();
        AssertInt(((ExplorationAI)ai).CurrentCellId).IsEqual(start);
        AssertBool(ai.HasFoundEnemy).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CreatedExplorationRaisesItsEventsThroughTheInterface()
    {
        var (_, gridData) = OpenFloorMap.Create(5, 5, new Vector2I(0, 0));
        IExplorationAI ai = ExplorationAIs.Create(gridData, null, VisibilityCheckers.CreateSimple());
        var moves = 0;
        ai.PlayerMoved += _ => moves++;

        var moved = ai.StepExploration();

        AssertBool(!moved || moves > 0).IsTrue();
    }
}
