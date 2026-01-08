using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services.Exploration;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.Exploration;

[TestSuite]
[RequireGodotRuntime]
public class ExplorationStrategyTest
{
    // Test-local tile IDs (tests don't depend on specific values, just consistent usage)
    private const string Floor = "floor";
    private const string Wall = "wall";

    [TestCase]
    public void TestFrontierExplorationStrategyReturnsNearestFrontier()
    {
        var mapData = CreateSimpleMap(5, 5, new Vector2I(0, 0));
        var visibilityChecker = new SimpleVisibilityChecker();
        var frontierBehavior = new FrontierExplorationBehavior(mapData, visibilityChecker);
        frontierBehavior.UpdateVision(new Vector2I(0, 0));

        var strategy = new FrontierExplorationStrategy();
        var context = new ExplorationContext
        {
            MapData = mapData,
            CurrentPosition = new Vector2I(0, 0),
            FrontierBehavior = frontierBehavior
        };

        var target = strategy.GetNextTarget(context);

        // Should return a valid frontier tile
        AssertThat(target).IsNotNull();
        AssertBool(mapData.IsPassable(target!.Value)).IsTrue();
    }

    [TestCase]
    public void TestFrontierExplorationStrategyReturnsNullWhenFullyExplored()
    {
        var mapData = CreateSimpleMap(2, 2, new Vector2I(0, 0));
        var visibilityChecker = new SimpleVisibilityChecker();
        var frontierBehavior = new FrontierExplorationBehavior(mapData, visibilityChecker, visionRange: 10);

        // With large vision range, all tiles should be trivially visible
        frontierBehavior.UpdateVision(new Vector2I(0, 0));

        var strategy = new FrontierExplorationStrategy();
        var context = new ExplorationContext
        {
            MapData = mapData,
            CurrentPosition = new Vector2I(0, 0),
            FrontierBehavior = frontierBehavior
        };

        var target = strategy.GetNextTarget(context);

        // Should return null when no frontier remains
        AssertThat(target).IsNull();
    }

    [TestCase]
    public void TestPathToEnemyStrategyPrioritizesVisibleEnemy()
    {
        var mapData = CreateSimpleMap(5, 5, new Vector2I(0, 0));
        var visibilityChecker = new SimpleVisibilityChecker();
        var frontierBehavior = new FrontierExplorationBehavior(mapData, visibilityChecker);

        var strategy = new PathToEnemyStrategy();
        var visibleEnemy = new Vector2I(3, 3);
        var lastKnownEnemy = new Vector2I(4, 4);

        var context = new ExplorationContext
        {
            MapData = mapData,
            CurrentPosition = new Vector2I(0, 0),
            FrontierBehavior = frontierBehavior,
            VisibleEnemyPosition = visibleEnemy,
            LastKnownEnemyPosition = lastKnownEnemy
        };

        var target = strategy.GetNextTarget(context);

        // Should prioritize visible enemy over last known
        AssertThat(target).IsEqual(visibleEnemy);
    }

    [TestCase]
    public void TestPathToEnemyStrategyFallsBackToLastKnown()
    {
        var mapData = CreateSimpleMap(5, 5, new Vector2I(0, 0));
        var visibilityChecker = new SimpleVisibilityChecker();
        var frontierBehavior = new FrontierExplorationBehavior(mapData, visibilityChecker);

        var strategy = new PathToEnemyStrategy();
        var lastKnownEnemy = new Vector2I(4, 4);

        var context = new ExplorationContext
        {
            MapData = mapData,
            CurrentPosition = new Vector2I(0, 0),
            FrontierBehavior = frontierBehavior,
            VisibleEnemyPosition = null,
            LastKnownEnemyPosition = lastKnownEnemy
        };

        var target = strategy.GetNextTarget(context);

        // Should fall back to last known position
        AssertThat(target).IsEqual(lastKnownEnemy);
    }

    [TestCase]
    public void TestPathToEnemyStrategyReturnsNullWhenNoEnemy()
    {
        var mapData = CreateSimpleMap(5, 5, new Vector2I(0, 0));
        var visibilityChecker = new SimpleVisibilityChecker();
        var frontierBehavior = new FrontierExplorationBehavior(mapData, visibilityChecker);

        var strategy = new PathToEnemyStrategy();

        var context = new ExplorationContext
        {
            MapData = mapData,
            CurrentPosition = new Vector2I(0, 0),
            FrontierBehavior = frontierBehavior,
            VisibleEnemyPosition = null,
            LastKnownEnemyPosition = null
        };

        var target = strategy.GetNextTarget(context);

        // Should return null when no enemy info available
        AssertThat(target).IsNull();
    }

    [TestCase]
    public void TestExplorationContextContainsAllRequiredData()
    {
        var mapData = CreateSimpleMap(5, 5, new Vector2I(0, 0));
        var visibilityChecker = new SimpleVisibilityChecker();
        var frontierBehavior = new FrontierExplorationBehavior(mapData, visibilityChecker);
        var currentPos = new Vector2I(2, 2);
        var visibleEnemy = new Vector2I(3, 3);
        var lastKnownEnemy = new Vector2I(4, 4);

        var context = new ExplorationContext
        {
            MapData = mapData,
            CurrentPosition = currentPos,
            FrontierBehavior = frontierBehavior,
            VisibleEnemyPosition = visibleEnemy,
            LastKnownEnemyPosition = lastKnownEnemy
        };

        AssertThat(context.MapData).IsEqual(mapData);
        AssertThat(context.CurrentPosition).IsEqual(currentPos);
        AssertThat(context.FrontierBehavior).IsEqual(frontierBehavior);
        AssertThat(context.VisibleEnemyPosition).IsEqual(visibleEnemy);
        AssertThat(context.LastKnownEnemyPosition).IsEqual(lastKnownEnemy);
    }

    [TestCase]
    public void TestStrategiesAreInterchangeable()
    {
        var mapData = CreateSimpleMap(5, 5, new Vector2I(0, 0));
        mapData.EnemyPositions.Add(new Vector2I(4, 4));
        var visibilityChecker = new SimpleVisibilityChecker();
        var frontierBehavior = new FrontierExplorationBehavior(mapData, visibilityChecker);
        frontierBehavior.UpdateVision(new Vector2I(0, 0));

        var context = new ExplorationContext
        {
            MapData = mapData,
            CurrentPosition = new Vector2I(0, 0),
            FrontierBehavior = frontierBehavior,
            VisibleEnemyPosition = new Vector2I(4, 4)
        };

        // Both strategies implement the same interface
        IExplorationStrategy frontierStrategy = new FrontierExplorationStrategy();
        IExplorationStrategy enemyStrategy = new PathToEnemyStrategy();

        // Both can be used polymorphically
        var frontierTarget = frontierStrategy.GetNextTarget(context);
        var enemyTarget = enemyStrategy.GetNextTarget(context);

        // Frontier strategy looks for unexplored tiles
        AssertThat(frontierTarget).IsNotNull();

        // Enemy strategy returns enemy position
        AssertThat(enemyTarget).IsEqual(new Vector2I(4, 4));
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
}
