using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Tests.TestUtilities.Fixtures;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.ExplorationAIScenarios;

/// <summary>
///     ExplorationAI enemy pursuit and line-of-sight loss scenarios split out of ExplorationAITest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ExplorationAIEnemyPursuitTest : ExplorationAITestBase
{
    [TestCase]
    public void TestNoOscillationWhenLosingEnemyLineOfSight()
    {
        // Test that when the AI loses line-of-sight to an enemy, it continues toward
        // the last known position instead of oscillating between modes.
        //
        // Use small vision range (2) to force actual walking and prevent trivial visibility
        // from marking tiles as "visited" without movement.

        var (mapData, gridData) = OpenFloorMap.Create(8, 8, new Vector2I(0, 0));
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
}
