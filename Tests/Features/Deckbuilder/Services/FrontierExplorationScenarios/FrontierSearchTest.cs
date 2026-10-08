using System.Collections.Generic;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Tests.TestUtilities.Fixtures;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.FrontierExplorationScenarios;

/// <summary>
///     FrontierExplorationBehavior nearest-frontier search and movement scenarios split out of
///     FrontierExplorationBehaviorTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FrontierSearchTest : FrontierExplorationTestBase
{
    [TestCase]
    public void TestFindNearestFrontierCellReturnsAdjacentUnvisited()
    {
        // Simple 3x3 grid, all passable
        var (mapData, gridData) = OpenFloorMap.Create(3, 3);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 1);

        // Update vision from center - with vision range 1, should see immediate neighbors
        var centerCell = gridData.PositionToCellId(new Vector2I(1, 1));
        behavior.UpdateVision(centerCell);

        // Find nearest frontier - should return an adjacent unvisited cell
        var frontierCell = behavior.FindNearestFrontierCell(centerCell);

        AssertThat(frontierCell).IsNotNull();
        // Should be adjacent to current position (distance of 1)
        var frontierPos = gridData.CellIdToPosition(frontierCell!.Value);
        var distance = Mathf.Abs(frontierPos.X - 1) + Mathf.Abs(frontierPos.Y - 1);
        AssertThat(distance).IsEqual(1);
    }

    [TestCase]
    public void TestFindNearestFrontierCellUsesWalkingDistanceNotEuclidean()
    {
        // Create an L-shaped corridor where the "close" tile by Euclidean distance
        // is actually far by walking distance
        //
        // Better layout:
        //   0 1 2 3 4
        // 0 F W W W F  <- Start at (0,0), target at (4,0) but blocked
        // 1 F W W W F
        // 2 F W W W F
        // 3 F W W W F
        // 4 F F F F F  <- Must go around bottom

        var mapData = new SimpleMapData
        {
            TileIds = new string[5, 5],
            Size = new Vector2I(5, 5),
            PlayerStart = new Vector2I(0, 0)
        };

        FillWithWalls(mapData.TileIds);

        // Create the L-shaped corridor
        // Left column
        for (var y = 0; y < 5; y++)
        {
            mapData.TileIds[y, 0] = Floor;
            mapData.PassableTiles.Add(new Vector2I(0, y));
        }
        // Bottom row
        for (var x = 1; x < 5; x++)
        {
            mapData.TileIds[4, x] = Floor;
            mapData.PassableTiles.Add(new Vector2I(x, 4));
        }
        // Right column
        for (var y = 0; y < 4; y++)
        {
            mapData.TileIds[y, 4] = Floor;
            mapData.PassableTiles.Add(new Vector2I(4, y));
        }

        var gridData = new RegularGridMapData(mapData);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 1);

        // Start at (0,0) and update vision
        var startCell = gridData.PositionToCellId(new Vector2I(0, 0));
        behavior.UpdateVision(startCell);

        // Find nearest frontier - should be (0,1) which is adjacent, not (4,0) which is closer Euclidean
        var frontierCell = behavior.FindNearestFrontierCell(startCell);

        AssertThat(frontierCell).IsNotNull();
        // Nearest by walking should be (0,1) - one step away
        var frontierPos = gridData.CellIdToPosition(frontierCell!.Value);
        AssertThat(frontierPos).IsEqual(new Vector2I(0, 1));
    }

    [TestCase]
    public void TestFindNearestFrontierCellReturnsNullWhenFullyExplored()
    {
        // Small 2x2 map
        var (mapData, gridData) = OpenFloorMap.Create(2, 2);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 10);

        // Visit all tiles
        behavior.UpdateVision(gridData.PositionToCellId(new Vector2I(0, 0)));
        behavior.UpdateVision(gridData.PositionToCellId(new Vector2I(1, 0)));
        behavior.UpdateVision(gridData.PositionToCellId(new Vector2I(0, 1)));
        behavior.UpdateVision(gridData.PositionToCellId(new Vector2I(1, 1)));

        var frontierCell = behavior.FindNearestFrontierCell(gridData.PositionToCellId(new Vector2I(0, 0)));

        AssertThat(frontierCell).IsNull();
    }

    [TestCase]
    public void TestFindNearestFrontierSkipsUnreachableTiles()
    {
        // Create a map with disconnected regions
        //   0 1 2 3 4
        // 0 F F W F F  <- Two disconnected regions
        var mapData = new SimpleMapData
        {
            TileIds = new string[1, 5],
            Size = new Vector2I(5, 1),
            PlayerStart = new Vector2I(0, 0)
        };

        mapData.TileIds[0, 0] = Floor;
        mapData.TileIds[0, 1] = Floor;
        mapData.TileIds[0, 2] = Wall;
        mapData.TileIds[0, 3] = Floor;
        mapData.TileIds[0, 4] = Floor;

        mapData.PassableTiles.Add(new Vector2I(0, 0));
        mapData.PassableTiles.Add(new Vector2I(1, 0));
        mapData.PassableTiles.Add(new Vector2I(3, 0));
        mapData.PassableTiles.Add(new Vector2I(4, 0));

        var gridData = new RegularGridMapData(mapData);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 1);

        // Visit left region
        behavior.UpdateVision(gridData.PositionToCellId(new Vector2I(0, 0)));
        behavior.UpdateVision(gridData.PositionToCellId(new Vector2I(1, 0)));

        // Find frontier - should return null since right region is unreachable
        var frontier = behavior.FindNearestFrontierCell(gridData.PositionToCellId(new Vector2I(1, 0)));

        AssertThat(frontier).IsNull();
    }

    [TestCase]
    public void TestConsecutiveMovesDoNotBacktrack()
    {
        // Simulates the backtracking bug scenario
        var (mapData, gridData) = OpenFloorMap.Create(10, 10);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 2);

        var cellHistory = new List<int>();
        var currentCell = gridData.PositionToCellId(new Vector2I(0, 0));

        // Simulate 20 exploration steps
        for (var i = 0; i < 20; i++)
        {
            behavior.UpdateVision(currentCell);
            cellHistory.Add(currentCell);

            var nextFrontier = behavior.FindNearestFrontierCell(currentCell);
            if (nextFrontier == null) break;

            // Move to frontier (in real code, pathfinding would be used)
            currentCell = nextFrontier.Value;
        }

        // Check that we don't have excessive backtracking
        // Count how many times we return to a previously visited cell
        var backtrackCount = 0;
        var visited = new HashSet<int>();
        foreach (var cell in cellHistory)
        {
            if (visited.Contains(cell))
                backtrackCount++;
            visited.Add(cell);
        }

        // Should have minimal backtracking (some is OK due to topology)
        // With BFS-based frontier finding, we shouldn't have excessive backtracking
        AssertThat(backtrackCount).IsLess(cellHistory.Count / 2);
    }
}
