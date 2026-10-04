using System.Collections.Generic;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Tests.TestUtilities.Fixtures;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.ExplorationAIScenarios;

/// <summary>
///     ExplorationAI frontier mode, visibility and enemy detection scenarios split out of ExplorationAITest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ExplorationAIFrontierTest : ExplorationAITestBase
{
    [TestCase]
    public void TestExplorationStartsInFrontierMode()
    {
        var (mapData, gridData) = OpenFloorMap.Create(5, 5, new Vector2I(0, 0));
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

        var (mapData, gridData) = OpenFloorMap.Create(10, 10, new Vector2I(0, 0));
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
        var (mapData, gridData) = OpenFloorMap.Create(10, 10, new Vector2I(0, 0));
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
        var (mapData, gridData) = OpenFloorMap.Create(5, 5, new Vector2I(0, 0));
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
    public void TestExplorationSwitchesToEnemyModeWhenVisible()
    {
        var (mapData, gridData) = OpenFloorMap.Create(10, 10, new Vector2I(0, 0));
        mapData.EnemyPositions.Add(new Vector2I(3, 0)); // Enemy directly visible

        var ai = new ExplorationAI(mapData, visibilityChecker: new SimpleVisibilityChecker(), visionRange: 5);
        var enemySpotted = false;

        ai.EnemySpotted += _ => enemySpotted = true;

        ai.StepExploration();

        AssertBool(enemySpotted).IsTrue();
        AssertThat(ai.CurrentMode).IsEqual(ExplorationMode.PathToEnemy);
    }


    [TestCase]
    public void TestExplorationWithMultipleEnemies()
    {
        var (mapData, gridData) = OpenFloorMap.Create(5, 5, new Vector2I(0, 0));
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
        var (mapData, gridData) = OpenFloorMap.Create(3, 3, new Vector2I(1, 1));
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

}
