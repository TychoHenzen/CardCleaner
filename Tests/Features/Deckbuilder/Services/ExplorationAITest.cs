using System.Collections.Generic;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Deckbuilder.Services;

[TestSuite]
[RequireGodotRuntime]
public class ExplorationAITest
{
    [TestCase]
    public void TestExplorationAIInitialization()
    {
        var mapData = CreateSimpleMap(5, 5, new Vector2I(0, 0));

        var ai = new ExplorationAI(mapData);

        AssertThat(ai.CurrentPosition).IsEqual(new Vector2I(0, 0));
        AssertBool(ai.HasFoundEnemy).IsFalse();
        AssertBool(ai.HasFinishedExploration).IsFalse();
    }

    [TestCase]
    public void TestExplorationMovesToPassableTiles()
    {
        var mapData = CreateSimpleMap(5, 5, new Vector2I(0, 0));
        var ai = new ExplorationAI(mapData);

        var moved = ai.StepExploration();

        AssertBool(moved || ai.HasFinishedExploration).IsTrue();
        AssertBool(mapData.IsPassable(ai.CurrentPosition)).IsTrue();
    }

    [TestCase]
    public void TestExplorationPlayerMovedEventFires()
    {
        var mapData = CreateSimpleMap(5, 5, new Vector2I(0, 0));
        var ai = new ExplorationAI(mapData);
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
        var ai = new ExplorationAI(mapData);
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
            AssertThat(foundPosition).IsEqual(ai.EnemyPosition);
        }
    }

    [TestCase]
    public void TestExplorationEventuallyFinishes()
    {
        var mapData = CreateSimpleMap(5, 5, new Vector2I(0, 0));
        var ai = new ExplorationAI(mapData);

        var stepCount = 0;
        while (!ai.HasFinishedExploration && stepCount < 1000)
        {
            ai.StepExploration();
            stepCount++;
        }

        AssertBool(ai.HasFinishedExploration).IsTrue();
    }

    [TestCase]
    public void TestExplorationVisitsAllPassableTiles()
    {
        var mapData = CreateSimpleMap(3, 3, new Vector2I(0, 0));
        mapData.EnemyPositions.Clear();

        var ai = new ExplorationAI(mapData);
        var visitedPositions = new HashSet<Vector2I> { ai.CurrentPosition };

        ai.PlayerMoved += pos => visitedPositions.Add(pos);

        var stepCount = 0;
        while (!ai.HasFinishedExploration && stepCount < 100)
        {
            ai.StepExploration();
            stepCount++;
        }

        AssertThat(visitedPositions.Count).IsEqual(mapData.PassableTiles.Count);
    }

    [TestCase]
    public void TestExplorationDoesNotMoveThroughBlockedTiles()
    {
        // Create a map with a corridor that forces specific movement
        var mapData = new SimpleMapData
        {
            Grid = new bool[5, 5],
            Size = new Vector2I(5, 5),
            PlayerStart = new Vector2I(0, 0)
        };

        // Create a simple L-shaped corridor: (0,0) -> (1,0) -> (2,0) -> (2,1) -> (2,2)
        mapData.Grid[0, 0] = true;
        mapData.Grid[0, 1] = true;
        mapData.Grid[0, 2] = true;
        mapData.Grid[1, 2] = true;
        mapData.Grid[2, 2] = true;

        mapData.PassableTiles.Add(new Vector2I(0, 0));
        mapData.PassableTiles.Add(new Vector2I(1, 0));
        mapData.PassableTiles.Add(new Vector2I(2, 0));
        mapData.PassableTiles.Add(new Vector2I(2, 1));
        mapData.PassableTiles.Add(new Vector2I(2, 2));

        var ai = new ExplorationAI(mapData);
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
        var ai = new ExplorationAI(mapData);

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
    public void TestExplorationPrioritizesEnemyPositions()
    {
        var mapData = new SimpleMapData
        {
            Grid = new bool[10, 10],
            Size = new Vector2I(10, 10),
            PlayerStart = new Vector2I(0, 0)
        };

        for (var y = 0; y < 10; y++)
        for (var x = 0; x < 10; x++)
        {
            mapData.Grid[y, x] = true;
            mapData.PassableTiles.Add(new Vector2I(x, y));
        }

        mapData.EnemyPositions.Add(new Vector2I(5, 5));

        var ai = new ExplorationAI(mapData);

        var stepCount = 0;
        while (!ai.HasFoundEnemy && stepCount < 200)
        {
            ai.StepExploration();
            stepCount++;
        }

        AssertBool(ai.HasFoundEnemy).IsTrue();
        AssertThat(ai.EnemyPosition).IsEqual(new Vector2I(5, 5));
    }

    [TestCase]
    public void TestExplorationWithSingleTileMap()
    {
        var mapData = new SimpleMapData
        {
            Grid = new bool[1, 1],
            Size = new Vector2I(1, 1),
            PlayerStart = new Vector2I(0, 0)
        };
        mapData.Grid[0, 0] = true;
        mapData.PassableTiles.Add(new Vector2I(0, 0));

        var ai = new ExplorationAI(mapData);

        ai.StepExploration();

        AssertBool(ai.HasFinishedExploration).IsTrue();
    }

    [TestCase]
    public void TestExplorationWithLinearCorridor()
    {
        var mapData = new SimpleMapData
        {
            Grid = new bool[1, 10],
            Size = new Vector2I(10, 1),
            PlayerStart = new Vector2I(0, 0)
        };

        for (var x = 0; x < 10; x++)
        {
            mapData.Grid[0, x] = true;
            mapData.PassableTiles.Add(new Vector2I(x, 0));
        }

        var ai = new ExplorationAI(mapData);

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
        var ai = new ExplorationAI(mapData);

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
        var mapData = new SimpleMapData
        {
            Grid = new bool[5, 5],
            Size = new Vector2I(5, 5),
            PlayerStart = new Vector2I(0, 0)
        };

        for (var y = 0; y < 5; y++)
        for (var x = 0; x < 5; x++)
        {
            mapData.Grid[y, x] = true;
            mapData.PassableTiles.Add(new Vector2I(x, y));
        }

        mapData.EnemyPositions.Add(new Vector2I(2, 2));
        mapData.EnemyPositions.Add(new Vector2I(4, 4));

        var ai = new ExplorationAI(mapData);

        var stepCount = 0;
        while (!ai.HasFoundEnemy && stepCount < 100)
        {
            ai.StepExploration();
            stepCount++;
        }

        AssertBool(ai.HasFoundEnemy).IsTrue();
        AssertBool(
            ai.EnemyPosition == new Vector2I(2, 2) || ai.EnemyPosition == new Vector2I(4, 4)
        ).IsTrue();
    }

    [TestCase]
    public void TestExplorationDoesNotFindRemovedEnemies()
    {
        var mapData = new SimpleMapData
        {
            Grid = new bool[5, 5],
            Size = new Vector2I(5, 5),
            PlayerStart = new Vector2I(0, 0)
        };

        for (var y = 0; y < 5; y++)
        for (var x = 0; x < 5; x++)
        {
            mapData.Grid[y, x] = true;
            mapData.PassableTiles.Add(new Vector2I(x, y));
        }

        // Add two enemies, then remove one (simulating defeat)
        var enemy1 = new Vector2I(2, 2);
        var enemy2 = new Vector2I(4, 4);
        mapData.EnemyPositions.Add(enemy1);
        mapData.EnemyPositions.Add(enemy2);

        // Remove first enemy (as if defeated)
        mapData.EnemyPositions.Remove(enemy1);

        var ai = new ExplorationAI(mapData);

        var stepCount = 0;
        while (!ai.HasFoundEnemy && stepCount < 100)
        {
            ai.StepExploration();
            stepCount++;
        }

        // Should only find the remaining enemy
        AssertBool(ai.HasFoundEnemy).IsTrue();
        AssertThat(ai.EnemyPosition).IsEqual(enemy2);
    }

    [TestCase]
    public void TestExplorationWithNoEnemies()
    {
        var mapData = new SimpleMapData
        {
            Grid = new bool[3, 3],
            Size = new Vector2I(3, 3),
            PlayerStart = new Vector2I(0, 0)
        };

        for (var y = 0; y < 3; y++)
        for (var x = 0; x < 3; x++)
        {
            mapData.Grid[y, x] = true;
            mapData.PassableTiles.Add(new Vector2I(x, y));
        }

        // No enemies on map
        var ai = new ExplorationAI(mapData);

        var stepCount = 0;
        while (!ai.HasFinishedExploration && stepCount < 100)
        {
            ai.StepExploration();
            stepCount++;
        }

        // Should finish exploration without finding any enemies
        AssertBool(ai.HasFinishedExploration).IsTrue();
        AssertBool(ai.HasFoundEnemy).IsFalse();
    }

    [TestCase]
    public void TestExplorationFindsEnemiesOneByOneAsTheyAreRemoved()
    {
        var mapData = new SimpleMapData
        {
            Grid = new bool[5, 5],
            Size = new Vector2I(5, 5),
            PlayerStart = new Vector2I(0, 0)
        };

        for (var y = 0; y < 5; y++)
        for (var x = 0; x < 5; x++)
        {
            mapData.Grid[y, x] = true;
            mapData.PassableTiles.Add(new Vector2I(x, y));
        }

        // Add three enemies at different positions
        var enemy1 = new Vector2I(1, 1);
        var enemy2 = new Vector2I(2, 2);
        var enemy3 = new Vector2I(3, 3);
        mapData.EnemyPositions.Add(enemy1);
        mapData.EnemyPositions.Add(enemy2);
        mapData.EnemyPositions.Add(enemy3);

        var enemiesEncountered = new List<Vector2I>();

        // First exploration - find first enemy
        var ai1 = new ExplorationAI(mapData);
        var stepCount = 0;
        while (!ai1.HasFoundEnemy && stepCount < 100)
        {
            ai1.StepExploration();
            stepCount++;
        }
        AssertBool(ai1.HasFoundEnemy).IsTrue();
        enemiesEncountered.Add(ai1.EnemyPosition);
        // Remove defeated enemy from map
        mapData.EnemyPositions.Remove(ai1.EnemyPosition);

        // Second exploration - should find different enemy
        var ai2 = new ExplorationAI(mapData);
        stepCount = 0;
        while (!ai2.HasFoundEnemy && stepCount < 100)
        {
            ai2.StepExploration();
            stepCount++;
        }
        AssertBool(ai2.HasFoundEnemy).IsTrue();
        AssertBool(!enemiesEncountered.Contains(ai2.EnemyPosition)).IsTrue();
        enemiesEncountered.Add(ai2.EnemyPosition);
        mapData.EnemyPositions.Remove(ai2.EnemyPosition);

        // Third exploration - should find last enemy
        var ai3 = new ExplorationAI(mapData);
        stepCount = 0;
        while (!ai3.HasFoundEnemy && stepCount < 100)
        {
            ai3.StepExploration();
            stepCount++;
        }
        AssertBool(ai3.HasFoundEnemy).IsTrue();
        AssertBool(!enemiesEncountered.Contains(ai3.EnemyPosition)).IsTrue();
        enemiesEncountered.Add(ai3.EnemyPosition);

        // All three unique enemies should have been found
        AssertThat(enemiesEncountered.Count).IsEqual(3);
    }

    private static SimpleMapData CreateSimpleMap(int width, int height, Vector2I playerStart)
    {
        var mapData = new SimpleMapData
        {
            Grid = new bool[height, width],
            Size = new Vector2I(width, height),
            PlayerStart = playerStart
        };

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            mapData.Grid[y, x] = true;
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
}
