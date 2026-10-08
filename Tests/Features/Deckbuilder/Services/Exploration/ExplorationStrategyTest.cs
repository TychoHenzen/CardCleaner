using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services.Exploration;
using CardCleaner.Tests.TestUtilities.Fixtures;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.Exploration;

[TestSuite]
[RequireGodotRuntime]
public class ExplorationStrategyTest
{
    // Test-local tile IDs (tests don't depend on specific values, just consistent usage)
    private const string Wall = "wall";

    [TestCase]
    public void TestFrontierExplorationStrategyReturnsNearestFrontier()
    {
        var (mapData, gridData) = OpenFloorMap.Create(5, 5, new Vector2I(0, 0));
        var visibilityChecker = new SimpleVisibilityChecker();
        var frontierBehavior = new FrontierExplorationBehavior(gridData, visibilityChecker);
        var startCell = gridData.PositionToCellId(new Vector2I(0, 0));
        frontierBehavior.UpdateVision(startCell);

        var strategy = new FrontierExplorationStrategy();
        var context = new ExplorationContext
        {
            MapData = gridData,
            CurrentCellId = startCell,
            FrontierBehavior = frontierBehavior
        };

        var target = strategy.GetNextTarget(context);

        // Should return a valid frontier cell
        AssertThat(target).IsNotNull();
        AssertBool(gridData.IsPassable(target!.Value)).IsTrue();
    }

    [TestCase]
    public void TestFrontierExplorationStrategyReturnsNullWhenFullyExplored()
    {
        var (mapData, gridData) = OpenFloorMap.Create(2, 2, new Vector2I(0, 0));
        var visibilityChecker = new SimpleVisibilityChecker();
        var frontierBehavior = new FrontierExplorationBehavior(gridData, visibilityChecker, visionRange: 10);

        // With large vision range, all tiles should be trivially visible
        var startCell = gridData.PositionToCellId(new Vector2I(0, 0));
        frontierBehavior.UpdateVision(startCell);

        var strategy = new FrontierExplorationStrategy();
        var context = new ExplorationContext
        {
            MapData = gridData,
            CurrentCellId = startCell,
            FrontierBehavior = frontierBehavior
        };

        var target = strategy.GetNextTarget(context);

        // Should return null when no frontier remains
        AssertThat(target).IsNull();
    }

    [TestCase]
    public void TestPathToEnemyStrategyPrioritizesVisibleEnemy()
    {
        var (mapData, gridData) = OpenFloorMap.Create(5, 5, new Vector2I(0, 0));
        var visibilityChecker = new SimpleVisibilityChecker();
        var frontierBehavior = new FrontierExplorationBehavior(gridData, visibilityChecker);

        var strategy = new PathToEnemyStrategy();
        var visibleEnemyCell = gridData.PositionToCellId(new Vector2I(3, 3));
        var lastKnownEnemyCell = gridData.PositionToCellId(new Vector2I(4, 4));
        var currentCell = gridData.PositionToCellId(new Vector2I(0, 0));

        var context = new ExplorationContext
        {
            MapData = gridData,
            CurrentCellId = currentCell,
            FrontierBehavior = frontierBehavior,
            VisibleEnemyCellId = visibleEnemyCell,
            LastKnownEnemyCellId = lastKnownEnemyCell
        };

        var target = strategy.GetNextTarget(context);

        // Should prioritize visible enemy over last known
        AssertThat(target).IsEqual(visibleEnemyCell);
    }

    [TestCase]
    public void TestPathToEnemyStrategyFallsBackToLastKnown()
    {
        var (mapData, gridData) = OpenFloorMap.Create(5, 5, new Vector2I(0, 0));
        var visibilityChecker = new SimpleVisibilityChecker();
        var frontierBehavior = new FrontierExplorationBehavior(gridData, visibilityChecker);

        var strategy = new PathToEnemyStrategy();
        var lastKnownEnemyCell = gridData.PositionToCellId(new Vector2I(4, 4));
        var currentCell = gridData.PositionToCellId(new Vector2I(0, 0));

        var context = new ExplorationContext
        {
            MapData = gridData,
            CurrentCellId = currentCell,
            FrontierBehavior = frontierBehavior,
            VisibleEnemyCellId = null,
            LastKnownEnemyCellId = lastKnownEnemyCell
        };

        var target = strategy.GetNextTarget(context);

        // Should fall back to last known position
        AssertThat(target).IsEqual(lastKnownEnemyCell);
    }

    [TestCase]
    public void TestPathToEnemyStrategyReturnsNullWhenNoEnemy()
    {
        var (mapData, gridData) = OpenFloorMap.Create(5, 5, new Vector2I(0, 0));
        var visibilityChecker = new SimpleVisibilityChecker();
        var frontierBehavior = new FrontierExplorationBehavior(gridData, visibilityChecker);

        var strategy = new PathToEnemyStrategy();
        var currentCell = gridData.PositionToCellId(new Vector2I(0, 0));

        var context = new ExplorationContext
        {
            MapData = gridData,
            CurrentCellId = currentCell,
            FrontierBehavior = frontierBehavior,
            VisibleEnemyCellId = null,
            LastKnownEnemyCellId = null
        };

        var target = strategy.GetNextTarget(context);

        // Should return null when no enemy info available
        AssertThat(target).IsNull();
    }

    [TestCase]
    public void TestExplorationContextContainsAllRequiredData()
    {
        var (mapData, gridData) = OpenFloorMap.Create(5, 5, new Vector2I(0, 0));
        var visibilityChecker = new SimpleVisibilityChecker();
        var frontierBehavior = new FrontierExplorationBehavior(gridData, visibilityChecker);
        var currentCell = gridData.PositionToCellId(new Vector2I(2, 2));
        var visibleEnemyCell = gridData.PositionToCellId(new Vector2I(3, 3));
        var lastKnownEnemyCell = gridData.PositionToCellId(new Vector2I(4, 4));

        var context = new ExplorationContext
        {
            MapData = gridData,
            CurrentCellId = currentCell,
            FrontierBehavior = frontierBehavior,
            VisibleEnemyCellId = visibleEnemyCell,
            LastKnownEnemyCellId = lastKnownEnemyCell
        };

        AssertThat(context.MapData).IsEqual(gridData);
        AssertThat(context.CurrentCellId).IsEqual(currentCell);
        AssertThat(context.FrontierBehavior).IsEqual(frontierBehavior);
        AssertThat(context.VisibleEnemyCellId).IsEqual(visibleEnemyCell);
        AssertThat(context.LastKnownEnemyCellId).IsEqual(lastKnownEnemyCell);
    }

    [TestCase]
    public void TestStrategiesAreInterchangeable()
    {
        var (mapData, gridData) = OpenFloorMap.Create(5, 5, new Vector2I(0, 0));
        mapData.EnemyPositions.Add(new Vector2I(4, 4));
        var visibilityChecker = new SimpleVisibilityChecker();
        var frontierBehavior = new FrontierExplorationBehavior(gridData, visibilityChecker);
        var currentCell = gridData.PositionToCellId(new Vector2I(0, 0));
        frontierBehavior.UpdateVision(currentCell);

        var visibleEnemyCell = gridData.PositionToCellId(new Vector2I(4, 4));
        var context = new ExplorationContext
        {
            MapData = gridData,
            CurrentCellId = currentCell,
            FrontierBehavior = frontierBehavior,
            VisibleEnemyCellId = visibleEnemyCell
        };

        // Both strategies implement the same interface
        IExplorationStrategy frontierStrategy = new FrontierExplorationStrategy();
        IExplorationStrategy enemyStrategy = new PathToEnemyStrategy();

        // Both can be used polymorphically
        var frontierTarget = frontierStrategy.GetNextTarget(context);
        var enemyTarget = enemyStrategy.GetNextTarget(context);

        // Frontier strategy looks for unexplored tiles
        AssertThat(frontierTarget).IsNotNull();

        // Enemy strategy returns enemy cell
        AssertThat(enemyTarget).IsEqual(visibleEnemyCell);
    }
}
