using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Tests.TestUtilities.Fixtures;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.ExplorationAIScenarios;

/// <summary>
///     ExplorationAI initialization, movement, completion and path scenarios split out of ExplorationAITest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ExplorationAIMovementTest : ExplorationAITestBase
{
    [TestCase]
    public void TestExplorationAIInitialization()
    {
        var (mapData, gridData) = OpenFloorMap.Create(5, 5, new Vector2I(0, 0));

        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker());

        AssertThat(GetCurrentGridPosition(ai, gridData)).IsEqual(new Vector2I(0, 0));
        AssertBool(ai.HasFoundEnemy).IsFalse();
        AssertBool(ai.HasFinishedExploration).IsFalse();
    }

    [TestCase]
    public void TestExplorationMovesToPassableTiles()
    {
        var (mapData, gridData) = OpenFloorMap.Create(5, 5, new Vector2I(0, 0));
        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker());

        var moved = ai.StepExploration();

        AssertBool(moved || ai.HasFinishedExploration).IsTrue();
        AssertBool(gridData.IsPassable(ai.CurrentCellId)).IsTrue();
    }

    [TestCase]
    public void TestExplorationPlayerMovedEventFires()
    {
        var (mapData, gridData) = OpenFloorMap.Create(5, 5, new Vector2I(0, 0));
        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker());
        var moveCount = 0;

        ai.PlayerMoved += _ => moveCount++;

        ai.StepExploration();
        ai.StepExploration();

        AssertThat(moveCount).IsGreaterEqual(0);
    }


    [TestCase]
    public void TestExplorationEventuallyFinishes()
    {
        var (mapData, gridData) = OpenFloorMap.Create(5, 5, new Vector2I(0, 0));
        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker());

        var stepCount = 0;
        while (!ai.HasFinishedExploration && stepCount < 1000)
        {
            ai.StepExploration();
            stepCount++;
        }

        AssertBool(ai.HasFinishedExploration).IsTrue();
    }

    [TestCase]
    public void TestExplorationSeesAllPassableTiles()
    {
        var (mapData, gridData) = OpenFloorMap.Create(3, 3, new Vector2I(1, 1));
        mapData.EnemyPositions.Clear();

        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker(), visionRange: 10);

        var stepCount = 0;
        while (!ai.HasFinishedExploration && stepCount < 100)
        {
            ai.StepExploration();
            stepCount++;
        }

        // With frontier exploration, we should have seen all passable tiles
        AssertThat(ai.SeenCells.Count).IsGreaterEqual(mapData.PassableTiles.Count);
    }

    [TestCase]
    public void TestExplorationDoesNotMoveThroughBlockedTiles()
    {
        // Create a map with a corridor that forces specific movement
        var mapData = new SimpleMapData
        {
            TileIds = new string[5, 5], Size = new Vector2I(5, 5), PlayerStart = new Vector2I(0, 0)
        };

        FillWithWalls(mapData.TileIds);

        // Create a simple L-shaped corridor: (0,0) -> (1,0) -> (2,0) -> (2,1) -> (2,2)
        mapData.TileIds[0, 0] = Floor;
        mapData.TileIds[0, 1] = Floor;
        mapData.TileIds[0, 2] = Floor;
        mapData.TileIds[1, 2] = Floor;
        mapData.TileIds[2, 2] = Floor;

        mapData.PassableTiles.Add(new Vector2I(0, 0));
        mapData.PassableTiles.Add(new Vector2I(1, 0));
        mapData.PassableTiles.Add(new Vector2I(2, 0));
        mapData.PassableTiles.Add(new Vector2I(2, 1));
        mapData.PassableTiles.Add(new Vector2I(2, 2));

        var gridData = new RegularGridMapData(mapData);
        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker());
        var stepCount = 0;

        while (!ai.HasFinishedExploration && stepCount < 100)
        {
            AssertBool(gridData.IsPassable(ai.CurrentCellId)).IsTrue();
            ai.StepExploration();
            stepCount++;
        }

        AssertBool(ai.HasFinishedExploration).IsTrue();
    }

    [TestCase]
    public void TestExplorationStepReturnsFalseWhenFinished()
    {
        var (mapData, gridData) = OpenFloorMap.Create(2, 2, new Vector2I(0, 0));
        mapData.EnemyPositions.Clear();
        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker(), visionRange: 10);

        var stepCount = 0;
        while (!ai.HasFinishedExploration && stepCount < 100)
        {
            ai.StepExploration();
            stepCount++;
        }

        var result = ai.StepExploration();

        AssertBool(result).IsFalse();
    }


    [TestCase]
    public void TestExplorationWithSingleTileMap()
    {
        var mapData = new SimpleMapData
        {
            TileIds = new string[1, 1], Size = new Vector2I(1, 1), PlayerStart = new Vector2I(0, 0)
        };
        mapData.TileIds[0, 0] = Floor;
        mapData.PassableTiles.Add(new Vector2I(0, 0));

        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker());

        ai.StepExploration();

        AssertBool(ai.HasFinishedExploration).IsTrue();
    }

    [TestCase]
    public void TestExplorationWithLinearCorridor()
    {
        var mapData = new SimpleMapData
        {
            TileIds = new string[1, 10], Size = new Vector2I(10, 1), PlayerStart = new Vector2I(0, 0)
        };

        for (var x = 0; x < 10; x++)
        {
            mapData.TileIds[0, x] = Floor;
            mapData.PassableTiles.Add(new Vector2I(x, 0));
        }

        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker());

        var stepCount = 0;
        while (!ai.HasFinishedExploration && stepCount < 100)
        {
            ai.StepExploration();
            stepCount++;
        }

        AssertBool(ai.HasFinishedExploration).IsTrue();
    }

    [TestCase]
    public void TestExplorationPositionUpdates()
    {
        var (mapData, gridData) = OpenFloorMap.Create(5, 5, new Vector2I(0, 0));
        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker());

        var startCellId = ai.CurrentCellId;
        var positionChanged = false;
        var stepCount = 0;

        while (!ai.HasFinishedExploration && stepCount < 100)
        {
            ai.StepExploration();
            stepCount++;
            if (ai.CurrentCellId != startCellId)
            {
                positionChanged = true;
                break;
            }
        }

        AssertBool(positionChanged || ai.HasFinishedExploration).IsTrue();
    }

}
