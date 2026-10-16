using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Tests.TestUtilities.Fixtures;
using Godot;

namespace CardCleaner.Tests.Core.Services;

[TestSuite]
[RequireGodotRuntime]
public class PathfindersTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void CreateReturnsAPathfinderThatFindsTheConcreteClassPath()
    {
        var (_, gridData) = OpenFloorMap.Create(5, 5);
        var start = gridData.PositionToCellId(new Vector2I(0, 0));
        var goal = gridData.PositionToCellId(new Vector2I(4, 4));

        IPathfinder pathfinder = Pathfinders.Create(gridData);

        var path = pathfinder.FindPath(start, goal);
        AssertBool(pathfinder is Pathfinder).IsTrue();
        AssertInt(path.Count).IsGreater(1);
        AssertBool(path.SequenceEqual(new Pathfinder(gridData).FindPath(start, goal))).IsTrue();
    }
}
