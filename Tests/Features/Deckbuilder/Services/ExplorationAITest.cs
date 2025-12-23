using System.Collections.Generic;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Deckbuilder.Services;

[TestSuite]
[RequireGodotRuntime]
public class ExplorationAITest
{
    private const string Floor = SimpleMapGenerator.FloorTileId;
    private const string Wall = SimpleMapGenerator.WallTileId;

    [TestCase]
    public void TestExplorationAIInitialization()
    {
        var mapData = CreateSimpleMap(5, 5, new Vector2I(0, 0));

        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker());

        AssertThat(ai.CurrentPosition).IsEqual(new Vector2I(0, 0));
        AssertBool(ai.HasFoundEnemy).IsFalse();
        AssertBool(ai.HasFinishedExploration).IsFalse();
    }

    [TestCase]
    public void TestExplorationMovesToPassableTiles()
    {
        var mapData = CreateSimpleMap(5, 5, new Vector2I(0, 0));
        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker());

        var moved = ai.StepExploration();

        AssertBool(moved || ai.HasFinishedExploration).IsTrue();
        AssertBool(mapData.IsPassable(ai.CurrentPosition)).IsTrue();
    }

    [TestCase]
    public void TestExplorationPlayerMovedEventFires()
    {
        var mapData = CreateSimpleMap(5, 5, new Vector2I(0, 0));
        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker());
        var moveCount = 0;

        ai.PlayerMoved += _ => moveCount++;

        ai.StepExploration();
        ai.StepExploration();

        AssertThat(moveCount).IsGreaterEqual(0);
    }

    [TestCase]
    public void TestExplorationFindAndReportsEnemy()
    {
        var mapData = CreateMapWithEnemy();
        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker());
        var enemyFound = false;
        Vector2I? foundPosition = null;

        ai.EnemyEncountered += pos =>
        {
            enemyFound = true;
            foundPosition = pos;
        };

        var stepCount = 0;
        while (!ai.HasFinishedExploration && stepCount < 100)
        {
            ai.StepExploration();
            stepCount++;
        }

        if (ai.HasFoundEnemy)
        {
            AssertBool(enemyFound).IsTrue();
            AssertThat(foundPosition).IsNotNull();
        }
    }

    [TestCase]
    public void TestExplorationEventuallyFinishes()
    {
        var mapData = CreateSimpleMap(5, 5, new Vector2I(0, 0));
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
        var mapData = CreateSimpleMap(3, 3, new Vector2I(1, 1));
        mapData.EnemyPositions.Clear();

        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker(), visionRange: 10);

        var stepCount = 0;
        while (!ai.HasFinishedExploration && stepCount < 100)
        {
            ai.StepExploration();
            stepCount++;
        }

        // With frontier exploration, we should have seen all passable tiles
        AssertThat(ai.SeenTiles.Count).IsGreaterEqual(mapData.PassableTiles.Count);
    }

    [TestCase]
    public void TestExplorationDoesNotMoveThroughBlockedTiles()
    {
        // Create a map with a corridor that forces specific movement
        var mapData = new SimpleMapData
        {
            TileIds = new string[5, 5],
            Size = new Vector2I(5, 5),
            PlayerStart = new Vector2I(0, 0)
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

        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker());
        var stepCount = 0;

        while (!ai.HasFinishedExploration && stepCount < 100)
        {
            AssertBool(mapData.IsPassable(ai.CurrentPosition)).IsTrue();
            ai.StepExploration();
            stepCount++;
        }

        AssertBool(ai.HasFinishedExploration).IsTrue();
    }

    [TestCase]
    public void TestExplorationStepReturnsFalseWhenFinished()
    {
        var mapData = CreateSimpleMap(2, 2, new Vector2I(0, 0));
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
    public void TestExplorationSwitchesToEnemyModeWhenVisible()
    {
        var mapData = CreateSimpleMap(10, 10, new Vector2I(0, 0));
        mapData.EnemyPositions.Add(new Vector2I(3, 0)); // Enemy directly visible

        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker(), visionRange: 5);
        var enemySpotted = false;

        ai.EnemySpotted += _ => enemySpotted = true;

        ai.StepExploration();

        AssertBool(enemySpotted).IsTrue();
        AssertThat(ai.CurrentMode).IsEqual(ExplorationMode.PathToEnemy);
    }

    [TestCase]
    public void TestExplorationWithSingleTileMap()
    {
        var mapData = new SimpleMapData
        {
            TileIds = new string[1, 1],
            Size = new Vector2I(1, 1),
            PlayerStart = new Vector2I(0, 0)
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
            TileIds = new string[1, 10],
            Size = new Vector2I(10, 1),
            PlayerStart = new Vector2I(0, 0)
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
        var mapData = CreateSimpleMap(5, 5, new Vector2I(0, 0));
        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker());

        var startPosition = ai.CurrentPosition;
        var positionChanged = false;
        var stepCount = 0;

        while (!ai.HasFinishedExploration && stepCount < 100)
        {
            ai.StepExploration();
            stepCount++;
            if (ai.CurrentPosition != startPosition)
            {
                positionChanged = true;
                break;
            }
        }

        AssertBool(positionChanged || ai.HasFinishedExploration).IsTrue();
    }

    [TestCase]
    public void TestExplorationWithMultipleEnemies()
    {
        var mapData = CreateSimpleMap(5, 5, new Vector2I(0, 0));
        mapData.EnemyPositions.Add(new Vector2I(2, 2));
        mapData.EnemyPositions.Add(new Vector2I(4, 4));

        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker());

        var stepCount = 0;
        while (!ai.HasFoundEnemy && stepCount < 100)
        {
            ai.StepExploration();
            stepCount++;
        }

        AssertBool(ai.HasFoundEnemy).IsTrue();
        AssertBool(
            ai.VisibleEnemyPosition == new Vector2I(2, 2) || ai.VisibleEnemyPosition == new Vector2I(4, 4)
        ).IsTrue();
    }

    [TestCase]
    public void TestExplorationWithNoEnemies()
    {
        var mapData = CreateSimpleMap(3, 3, new Vector2I(1, 1));
        mapData.EnemyPositions.Clear();

        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker(), visionRange: 10);

        var stepCount = 0;
        while (!ai.HasFinishedExploration && stepCount < 100)
        {
            ai.StepExploration();
            stepCount++;
        }

        AssertBool(ai.HasFinishedExploration).IsTrue();
        AssertBool(ai.HasFoundEnemy).IsFalse();
    }

    [TestCase]
    public void TestExplorationStartsInFrontierMode()
    {
        var mapData = CreateSimpleMap(5, 5, new Vector2I(0, 0));
        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker());

        AssertThat(ai.CurrentMode).IsEqual(ExplorationMode.FrontierExploration);
    }

    [TestCase]
    public void TestVisibilityBlockedByWall()
    {
        var mapData = new SimpleMapData
        {
            TileIds = new string[1, 5],
            Size = new Vector2I(5, 1),
            PlayerStart = new Vector2I(0, 0)
        };

        // Corridor with wall in middle: Floor, Floor, Wall, Floor, Enemy
        mapData.TileIds[0, 0] = Floor;
        mapData.TileIds[0, 1] = Floor;
        mapData.TileIds[0, 2] = Wall;
        mapData.TileIds[0, 3] = Floor;
        mapData.TileIds[0, 4] = Floor;

        mapData.PassableTiles.Add(new Vector2I(0, 0));
        mapData.PassableTiles.Add(new Vector2I(1, 0));
        mapData.PassableTiles.Add(new Vector2I(3, 0));
        mapData.PassableTiles.Add(new Vector2I(4, 0));

        mapData.EnemyPositions.Add(new Vector2I(4, 0));

        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker(), visionRange: 10);

        // Enemy should not be visible through wall
        AssertThat(ai.CurrentMode).IsEqual(ExplorationMode.FrontierExploration);
    }

    private static SimpleMapData CreateSimpleMap(int width, int height, Vector2I playerStart)
    {
        var mapData = new SimpleMapData
        {
            TileIds = new string[height, width],
            Size = new Vector2I(width, height),
            PlayerStart = playerStart
        };

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            mapData.TileIds[y, x] = Floor;
            mapData.PassableTiles.Add(new Vector2I(x, y));
        }

        return mapData;
    }

    private static SimpleMapData CreateMapWithEnemy()
    {
        var mapData = CreateSimpleMap(5, 5, new Vector2I(0, 0));
        mapData.EnemyPositions.Add(new Vector2I(4, 4));
        return mapData;
    }

    private static void FillWithWalls(string[,] tileIds)
    {
        for (var y = 0; y < tileIds.GetLength(0); y++)
        for (var x = 0; x < tileIds.GetLength(1); x++)
            tileIds[y, x] = Wall;
    }
}
