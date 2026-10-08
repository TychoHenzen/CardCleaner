using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Tests.TestUtilities.Fixtures;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.FrontierExplorationScenarios;

/// <summary>
///     FrontierExplorationBehavior vision, visited tracking and full-exploration scenarios split
///     out of FrontierExplorationBehaviorTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FrontierVisionTest : FrontierExplorationTestBase
{
    [TestCase]
    public void TestTriviallyVisibleTilesAreMarkedAsVisited()
    {
        // In an open room, trivially visible tiles should be marked as visited
        var (mapData, gridData) = OpenFloorMap.Create(5, 5);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 10);

        // Update vision from center with large vision range
        behavior.UpdateVision(gridData.PositionToCellId(new Vector2I(2, 2)));

        // With large vision, we can see the entire map and all neighbors
        // So all tiles should be marked as visited (trivially visible)
        AssertThat(behavior.VisitedCells.Count).IsEqual(25); // 5x5 = 25
        AssertBool(behavior.IsFullyExplored()).IsTrue();
    }

    [TestCase]
    public void TestTilesAtEdgeOfVisionNotTriviallyVisible()
    {
        // Tiles at the edge of vision range shouldn't be trivially visible
        // because we can't see their outer neighbors
        var (mapData, gridData) = OpenFloorMap.Create(10, 10);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 3);

        // Update vision from corner
        behavior.UpdateVision(gridData.PositionToCellId(new Vector2I(0, 0)));

        // The corner itself is visited
        AssertBool(behavior.VisitedCells.Contains(gridData.PositionToCellId(new Vector2I(0, 0)))).IsTrue();

        // Tiles fully within vision (where all neighbors are also seen) should be visited
        // Tiles at the edge of vision should NOT be trivially visited
        // Check that we haven't marked ALL seen tiles as visited
        AssertThat(behavior.VisitedCells.Count).IsLess(behavior.SeenCells.Count);
    }

    [TestCase]
    public void TestFrontierExistsAtEdgeOfTriviallyVisibleArea()
    {
        var (mapData, gridData) = OpenFloorMap.Create(10, 10);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 3);

        // Visit corner
        behavior.UpdateVision(gridData.PositionToCellId(new Vector2I(0, 0)));

        var frontierCells = behavior.FindFrontierCells();

        // Should have frontier cells at the edge of the trivially visible area
        AssertThat(frontierCells.Count).IsGreater(0);
    }

    [TestCase]
    public void TestCorridorTilesNotTriviallyVisibleAroundCorner()
    {
        // Create an L-shaped corridor where tiles around the corner can't be trivially visible
        //   0 1 2 3 4
        // 0 F F F W W
        // 1 W W F W W
        // 2 W W F F F
        var mapData = new SimpleMapData
        {
            TileIds = new string[3, 5],
            Size = new Vector2I(5, 3),
            PlayerStart = new Vector2I(0, 0)
        };

        FillWithWalls(mapData.TileIds);

        // Top corridor
        mapData.TileIds[0, 0] = Floor;
        mapData.TileIds[0, 1] = Floor;
        mapData.TileIds[0, 2] = Floor;
        mapData.PassableTiles.Add(new Vector2I(0, 0));
        mapData.PassableTiles.Add(new Vector2I(1, 0));
        mapData.PassableTiles.Add(new Vector2I(2, 0));

        // Corner
        mapData.TileIds[1, 2] = Floor;
        mapData.PassableTiles.Add(new Vector2I(2, 1));

        // Bottom corridor
        mapData.TileIds[2, 2] = Floor;
        mapData.TileIds[2, 3] = Floor;
        mapData.TileIds[2, 4] = Floor;
        mapData.PassableTiles.Add(new Vector2I(2, 2));
        mapData.PassableTiles.Add(new Vector2I(3, 2));
        mapData.PassableTiles.Add(new Vector2I(4, 2));

        var gridData = new RegularGridMapData(mapData);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 5);

        // Start at (0,0)
        behavior.UpdateVision(gridData.PositionToCellId(new Vector2I(0, 0)));

        // Tiles around the corner should not be seen (blocked by walls)
        AssertBool(behavior.SeenCells.Contains(gridData.PositionToCellId(new Vector2I(3, 2)))).IsFalse();
        AssertBool(behavior.SeenCells.Contains(gridData.PositionToCellId(new Vector2I(4, 2)))).IsFalse();

        // Frontier should exist to explore around the corner
        var frontier = behavior.FindNearestFrontierCell(gridData.PositionToCellId(new Vector2I(0, 0)));
        AssertThat(frontier).IsNotNull();
    }

    [TestCase]
    public void TestOpenRoomExploredInOneStep()
    {
        // Small open room should be fully explored from any position
        var (mapData, gridData) = OpenFloorMap.Create(3, 3);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 5);

        // Visit center
        behavior.UpdateVision(gridData.PositionToCellId(new Vector2I(1, 1)));

        // All tiles should be trivially visible and marked as visited
        AssertBool(behavior.IsFullyExplored()).IsTrue();
        AssertThat(behavior.VisitedCells.Count).IsEqual(9);

        // No frontier should exist
        var frontier = behavior.FindNearestFrontierCell(gridData.PositionToCellId(new Vector2I(1, 1)));
        AssertThat(frontier).IsNull();
    }

    [TestCase]
    public void TestIsFullyExploredWhenAllPassableTilesVisited()
    {
        var (mapData, gridData) = OpenFloorMap.Create(3, 3);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 1);

        // Visit all tiles
        for (var y = 0; y < 3; y++)
        for (var x = 0; x < 3; x++)
            behavior.UpdateVision(gridData.PositionToCellId(new Vector2I(x, y)));

        AssertBool(behavior.IsFullyExplored()).IsTrue();
    }

    [TestCase]
    public void TestIsNotFullyExploredWhenTilesRemain()
    {
        var (mapData, gridData) = OpenFloorMap.Create(3, 3);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 1);

        // Visit only one tile
        behavior.UpdateVision(gridData.PositionToCellId(new Vector2I(1, 1)));

        AssertBool(behavior.IsFullyExplored()).IsFalse();
    }

    [TestCase]
    public void TestUpdateVisionAddsCurrentPositionToVisited()
    {
        var (mapData, gridData) = OpenFloorMap.Create(3, 3);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 1);

        var centerCell = gridData.PositionToCellId(new Vector2I(1, 1));
        behavior.UpdateVision(centerCell);

        AssertBool(behavior.VisitedCells.Contains(centerCell)).IsTrue();
    }

    [TestCase]
    public void TestVisionBlockedByWalls()
    {
        // Create a map with a wall blocking vision
        //   0 1 2
        // 0 F W F
        var mapData = new SimpleMapData
        {
            TileIds = new string[1, 3],
            Size = new Vector2I(3, 1),
            PlayerStart = new Vector2I(0, 0)
        };

        mapData.TileIds[0, 0] = Floor;
        mapData.TileIds[0, 1] = Wall;
        mapData.TileIds[0, 2] = Floor;
        mapData.PassableTiles.Add(new Vector2I(0, 0));
        mapData.PassableTiles.Add(new Vector2I(2, 0));

        var gridData = new RegularGridMapData(mapData);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 5);

        // Update vision from (0,0) - should not see (2,0) due to wall
        behavior.UpdateVision(gridData.PositionToCellId(new Vector2I(0, 0)));

        AssertBool(behavior.SeenCells.Contains(gridData.PositionToCellId(new Vector2I(0, 0)))).IsTrue();
        AssertBool(behavior.SeenCells.Contains(gridData.PositionToCellId(new Vector2I(2, 0)))).IsFalse();
    }
}
