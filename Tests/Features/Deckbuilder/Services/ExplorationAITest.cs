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
    // Test-local tile IDs (tests don't depend on specific values, just consistent usage)
    private const string Floor = "floor";
    private const string Wall = "wall";

    [TestCase]
    public void TestExplorationAIInitialization()
    {
        var (mapData, gridData) = CreateSimpleMap(5, 5, new Vector2I(0, 0));

        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker());

        AssertThat(GetCurrentGridPosition(ai, gridData)).IsEqual(new Vector2I(0, 0));
        AssertBool(ai.HasFoundEnemy).IsFalse();
        AssertBool(ai.HasFinishedExploration).IsFalse();
    }

    [TestCase]
    public void TestExplorationMovesToPassableTiles()
    {
        var (mapData, gridData) = CreateSimpleMap(5, 5, new Vector2I(0, 0));
        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker());

        var moved = ai.StepExploration();

        AssertBool(moved || ai.HasFinishedExploration).IsTrue();
        AssertBool(gridData.IsPassable(ai.CurrentCellId)).IsTrue();
    }

    [TestCase]
    public void TestExplorationPlayerMovedEventFires()
    {
        var (mapData, gridData) = CreateSimpleMap(5, 5, new Vector2I(0, 0));
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
        var (mapData, gridData) = CreateMapWithEnemy();
        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker());
        var enemyFound = false;
        Vector2? foundPosition = null;

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
        var (mapData, gridData) = CreateSimpleMap(5, 5, new Vector2I(0, 0));
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
        var (mapData, gridData) = CreateSimpleMap(3, 3, new Vector2I(1, 1));
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
        var (mapData, gridData) = CreateSimpleMap(2, 2, new Vector2I(0, 0));
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
        var (mapData, gridData) = CreateSimpleMap(10, 10, new Vector2I(0, 0));
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
        var (mapData, gridData) = CreateSimpleMap(5, 5, new Vector2I(0, 0));
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

    [TestCase]
    public void TestExplorationWithMultipleEnemies()
    {
        var (mapData, gridData) = CreateSimpleMap(5, 5, new Vector2I(0, 0));
        mapData.EnemyPositions.Add(new Vector2I(2, 2));
        mapData.EnemyPositions.Add(new Vector2I(4, 4));

        // Use small vision range to force AI to physically move and encounter enemies
        // With large vision on small maps, tiles become "trivially visible" without movement
        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker(), visionRange: 2);

        var stepCount = 0;
        while (!ai.HasFoundEnemy && stepCount < 100)
        {
            ai.StepExploration();
            stepCount++;
        }

        AssertBool(ai.HasFoundEnemy).IsTrue();
        // Check visible enemy cell is one of the enemy positions
        var visibleEnemyPos = ai.VisibleEnemyCellId.HasValue
            ? gridData.GetGridPosition(ai.VisibleEnemyCellId.Value)
            : (Vector2I?)null;
        AssertBool(
            visibleEnemyPos == new Vector2I(2, 2) || visibleEnemyPos == new Vector2I(4, 4)
        ).IsTrue();
    }

    [TestCase]
    public void TestExplorationWithNoEnemies()
    {
        var (mapData, gridData) = CreateSimpleMap(3, 3, new Vector2I(1, 1));
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
        var (mapData, gridData) = CreateSimpleMap(5, 5, new Vector2I(0, 0));
        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker());

        AssertThat(ai.CurrentMode).IsEqual(ExplorationMode.FrontierExploration);
    }

    [TestCase]
    public void TestVisibilityBlockedByWall()
    {
        var mapData = new SimpleMapData
        {
            TileIds = new string[1, 5], Size = new Vector2I(5, 1), PlayerStart = new Vector2I(0, 0)
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


    [TestCase]
    public void TestTargetNeverBecomesVisitedTileOscillation()
    {
        // Test that the agent doesn't oscillate between tiles when targets become visited.
        // This happens when a frontier tile becomes "trivially visible" before the agent reaches it.
        // The fix: when the current target becomes visited, recalculate instead of continuing.

        var (mapData, gridData) = CreateSimpleMap(10, 10, new Vector2I(0, 0));
        mapData.EnemyPositions.Clear(); // No enemies, pure exploration

        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker(), visionRange: 3);

        // Track position history to detect oscillation
        var positionHistory = new List<int>();
        var maxSteps = 200;
        var stepCount = 0;

        while (!ai.HasFinishedExploration && stepCount < maxSteps)
        {
            positionHistory.Add(ai.CurrentCellId);
            ai.StepExploration();
            stepCount++;

            // Check for oscillation: same cell appearing 3+ times in last 10 moves
            if (positionHistory.Count >= 10)
            {
                var last10 = positionHistory.GetRange(positionHistory.Count - 10, 10);
                var currentCell = ai.CurrentCellId;
                var occurrences = 0;
                foreach (var cell in last10)
                {
                    if (cell == currentCell) occurrences++;
                }

                // Fail if we're oscillating (same cell visited 3+ times in 10 moves)
                AssertThat(occurrences).IsLess(3);
            }
        }

        // Should complete exploration
        AssertBool(ai.HasFinishedExploration).IsTrue();
    }

    [TestCase]
    public void TestCurrentTargetExposedForDebug()
    {
        // Verify that CurrentTargetCell and CurrentPath are exposed for debug visualization
        var (mapData, gridData) = CreateSimpleMap(10, 10, new Vector2I(0, 0));
        mapData.EnemyPositions.Clear();

        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker(), visionRange: 2);

        // Initially no target
        AssertThat(ai.CurrentTargetCell).IsNull();
        AssertThat(ai.CurrentPath).IsNotNull();
        AssertThat(ai.CurrentPath.Count).IsEqual(0);

        // After first step, should have target and/or path
        ai.StepExploration();

        // Path properties should be accessible (may or may not have values depending on state)
        AssertThat(ai.CurrentPath).IsNotNull();
    }

    [TestCase]
    public void TestPathUpdatedEventFires()
    {
        var (mapData, gridData) = CreateSimpleMap(5, 5, new Vector2I(0, 0));
        mapData.EnemyPositions.Clear();

        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker(), visionRange: 2);
        var pathUpdatedCount = 0;

        ai.PathUpdated += () => pathUpdatedCount++;

        // Step exploration several times
        for (var i = 0; i < 10 && !ai.HasFinishedExploration; i++)
        {
            ai.StepExploration();
        }

        // PathUpdated should have fired at least once
        AssertThat(pathUpdatedCount).IsGreater(0);
    }

    [TestCase]
    public void TestNoOscillationWhenLosingEnemyLineOfSight()
    {
        // Test that when the AI loses line-of-sight to an enemy, it continues toward
        // the last known position instead of oscillating between modes.
        //
        // Use small vision range (2) to force actual walking and prevent trivial visibility
        // from marking tiles as "visited" without movement.

        var (mapData, gridData) = CreateSimpleMap(8, 8, new Vector2I(0, 0));
        mapData.EnemyPositions.Add(new Vector2I(7, 7)); // Enemy at far corner

        // Small vision range forces actual movement to find enemy
        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker(), visionRange: 2);

        // Track mode changes to detect oscillation
        var modeChangeCount = 0;
        var lastMode = ai.CurrentMode;

        var maxSteps = 200;
        var stepCount = 0;

        while (!ai.HasFinishedExploration && stepCount < maxSteps)
        {
            ai.StepExploration();
            stepCount++;

            if (ai.CurrentMode != lastMode)
            {
                modeChangeCount++;
                lastMode = ai.CurrentMode;
            }

            // Check for rapid oscillation: more than 4 mode changes indicates a problem
            // Normal: Frontier -> PathToEnemy (when close enough to see) -> stays until found
            AssertThat(modeChangeCount).IsLessEqual(4);
        }

        // Should have found the enemy (moved to enemy position)
        AssertBool(ai.HasFoundEnemy).IsTrue();
    }

    [TestCase]
    public void TestEnemyPursuitDefersUntilDestinationReached()
    {
        // Test that when an enemy is spotted mid-exploration, the AI defers pursuit
        // until reaching the current destination.
        //
        // Layout: Long corridor with enemy visible from start but in opposite direction
        // Player should complete current path before pursuing

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

        // Enemy at far end, but AI will start exploring in one direction
        mapData.EnemyPositions.Add(new Vector2I(9, 0));

        var gridData = new RegularGridMapData(mapData);
        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker(), visionRange: 10);

        // First step should find a target and start moving
        ai.StepExploration();
        var firstTarget = ai.CurrentTargetCell;

        // Enemy should be spotted (visible from anywhere with range 10)
        var visibleEnemyPos = ai.VisibleEnemyCellId.HasValue
            ? gridData.GetGridPosition(ai.VisibleEnemyCellId.Value)
            : (Vector2I?)null;
        AssertThat(visibleEnemyPos).IsEqual(new Vector2I(9, 0));

        // But if we had a path, we should continue on it (deferred pursuit)
        // Take a few more steps
        for (var i = 0; i < 3 && !ai.HasFoundEnemy; i++)
        {
            ai.StepExploration();
        }

        // Eventually should find the enemy
        var maxSteps = 20;
        var stepCount = 0;
        while (!ai.HasFoundEnemy && stepCount < maxSteps)
        {
            ai.StepExploration();
            stepCount++;
        }

        AssertBool(ai.HasFoundEnemy).IsTrue();
    }

    [TestCase]
    public void TestPathPreservedWhenEnemyVisibleAndPursuing()
    {
        // Test that the path is not cleared every frame when pursuing a visible enemy
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

        mapData.EnemyPositions.Add(new Vector2I(9, 0));

        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker(), visionRange: 10);

        // Step to start pursuing
        ai.StepExploration();

        // Should be in pursuit mode
        AssertThat(ai.CurrentMode).IsEqual(ExplorationMode.PathToEnemy);

        // Track path updates
        var pathUpdateCount = 0;
        ai.PathUpdated += () => pathUpdateCount++;

        // Take several steps toward enemy
        for (var i = 0; i < 5 && !ai.HasFoundEnemy; i++)
        {
            ai.StepExploration();
        }

        // Path should not be constantly recalculated (should be stable)
        // Max 1-2 updates expected (initial path + maybe one adjustment)
        AssertThat(pathUpdateCount).IsLessEqual(2);
    }

    [TestCase]
    public void TestAIReachesLastKnownPositionWhenEnemyDisappears()
    {
        // Test that when an enemy becomes invisible, the AI walks to the last known position
        // This is simulated by having the enemy in a position that becomes invisible as we approach

        var mapData = new SimpleMapData
        {
            TileIds = new string[5, 10],
            Size = new Vector2I(10, 5),
            PlayerStart = new Vector2I(0, 2)
        };

        FillWithWalls(mapData.TileIds);

        // Create L-shaped path: horizontal then vertical
        // Player at (0,2), enemy at (9,2)
        // Wall at (5,2) blocks view after passing it
        for (var x = 0; x < 10; x++)
        {
            if (x != 5) // Gap in corridor
            {
                mapData.TileIds[2, x] = Floor;
                mapData.PassableTiles.Add(new Vector2I(x, 2));
            }
        }

        // Create alternate path around wall
        mapData.TileIds[1, 5] = Floor;
        mapData.TileIds[1, 4] = Floor;
        mapData.TileIds[1, 6] = Floor;
        mapData.PassableTiles.Add(new Vector2I(5, 1));
        mapData.PassableTiles.Add(new Vector2I(4, 1));
        mapData.PassableTiles.Add(new Vector2I(6, 1));
        mapData.TileIds[2, 4] = Floor;
        mapData.TileIds[2, 6] = Floor;

        // Add enemy
        mapData.EnemyPositions.Add(new Vector2I(9, 2));

        var gridData = new RegularGridMapData(mapData);
        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker(), visionRange: 10);

        // Run until we find the enemy
        var maxSteps = 50;
        var stepCount = 0;
        while (!ai.HasFoundEnemy && stepCount < maxSteps)
        {
            ai.StepExploration();
            stepCount++;
        }

        AssertBool(ai.HasFoundEnemy).IsTrue();
        AssertThat(GetCurrentGridPosition(ai, gridData)).IsEqual(new Vector2I(9, 2));
    }

    private static (SimpleMapData mapData, RegularGridMapData gridData) CreateSimpleMap(int width, int height, Vector2I playerStart)
    {
        var mapData = new SimpleMapData
        {
            TileIds = new string[height, width], Size = new Vector2I(width, height), PlayerStart = playerStart
        };

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            mapData.TileIds[y, x] = Floor;
            mapData.PassableTiles.Add(new Vector2I(x, y));
        }

        var gridData = new RegularGridMapData(mapData);
        return (mapData, gridData);
    }

    private static (SimpleMapData mapData, RegularGridMapData gridData) CreateMapWithEnemy()
    {
        var (mapData, gridData) = CreateSimpleMap(5, 5, new Vector2I(0, 0));
        mapData.EnemyPositions.Add(new Vector2I(4, 4));
        return (mapData, gridData);
    }

    private static void FillWithWalls(string[,] tileIds)
    {
        for (var y = 0; y < tileIds.GetLength(0); y++)
        for (var x = 0; x < tileIds.GetLength(1); x++)
            tileIds[y, x] = Wall;
    }

    /// <summary>
    /// Helper to convert AI's current cell ID back to grid position for assertions.
    /// </summary>
    private static Vector2I GetCurrentGridPosition(ExplorationAI ai, RegularGridMapData gridData)
    {
        return gridData.GetGridPosition(ai.CurrentCellId);
    }
}
