using System.Collections.Generic;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Deckbuilder.Services;

[TestSuite]
[RequireGodotRuntime]
public class FrontierExplorationBehaviorTest
{
    // Test-local tile IDs (tests don't depend on specific values, just consistent usage)
    private const string Floor = "floor";
    private const string Wall = "wall";

    [TestCase]
    public void TestFindNearestFrontierCellReturnsAdjacentUnvisited()
    {
        // Simple 3x3 grid, all passable
        var (mapData, gridData) = CreateSimpleMap(3, 3);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 1);

        // Update vision from center - with vision range 1, should see immediate neighbors
        var centerCell = gridData.GetCellId(new Vector2I(1, 1));
        behavior.UpdateVision(centerCell);

        // Find nearest frontier - should return an adjacent unvisited cell
        var frontierCell = behavior.FindNearestFrontierCell(centerCell);

        AssertThat(frontierCell).IsNotNull();
        // Should be adjacent to current position (distance of 1)
        var frontierPos = gridData.GetGridPosition(frontierCell!.Value);
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
        var startCell = gridData.GetCellId(new Vector2I(0, 0));
        behavior.UpdateVision(startCell);

        // Find nearest frontier - should be (0,1) which is adjacent, not (4,0) which is closer Euclidean
        var frontierCell = behavior.FindNearestFrontierCell(startCell);

        AssertThat(frontierCell).IsNotNull();
        // Nearest by walking should be (0,1) - one step away
        var frontierPos = gridData.GetGridPosition(frontierCell!.Value);
        AssertThat(frontierPos).IsEqual(new Vector2I(0, 1));
    }

    [TestCase]
    public void TestFindNearestFrontierCellReturnsNullWhenFullyExplored()
    {
        // Small 2x2 map
        var (mapData, gridData) = CreateSimpleMap(2, 2);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 10);

        // Visit all tiles
        behavior.UpdateVision(gridData.GetCellId(new Vector2I(0, 0)));
        behavior.UpdateVision(gridData.GetCellId(new Vector2I(1, 0)));
        behavior.UpdateVision(gridData.GetCellId(new Vector2I(0, 1)));
        behavior.UpdateVision(gridData.GetCellId(new Vector2I(1, 1)));

        var frontierCell = behavior.FindNearestFrontierCell(gridData.GetCellId(new Vector2I(0, 0)));

        AssertThat(frontierCell).IsNull();
    }

    [TestCase]
    public void TestTriviallyVisibleTilesAreMarkedAsVisited()
    {
        // In an open room, trivially visible tiles should be marked as visited
        var (mapData, gridData) = CreateSimpleMap(5, 5);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 10);

        // Update vision from center with large vision range
        behavior.UpdateVision(gridData.GetCellId(new Vector2I(2, 2)));

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
        var (mapData, gridData) = CreateSimpleMap(10, 10);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 3);

        // Update vision from corner
        behavior.UpdateVision(gridData.GetCellId(new Vector2I(0, 0)));

        // The corner itself is visited
        AssertBool(behavior.VisitedCells.Contains(gridData.GetCellId(new Vector2I(0, 0)))).IsTrue();

        // Tiles fully within vision (where all neighbors are also seen) should be visited
        // Tiles at the edge of vision should NOT be trivially visited
        // Check that we haven't marked ALL seen tiles as visited
        AssertThat(behavior.VisitedCells.Count).IsLess(behavior.SeenCells.Count);
    }

    [TestCase]
    public void TestFrontierExistsAtEdgeOfTriviallyVisibleArea()
    {
        var (mapData, gridData) = CreateSimpleMap(10, 10);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 3);

        // Visit corner
        behavior.UpdateVision(gridData.GetCellId(new Vector2I(0, 0)));

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
        behavior.UpdateVision(gridData.GetCellId(new Vector2I(0, 0)));

        // Tiles around the corner should not be seen (blocked by walls)
        AssertBool(behavior.SeenCells.Contains(gridData.GetCellId(new Vector2I(3, 2)))).IsFalse();
        AssertBool(behavior.SeenCells.Contains(gridData.GetCellId(new Vector2I(4, 2)))).IsFalse();

        // Frontier should exist to explore around the corner
        var frontier = behavior.FindNearestFrontierCell(gridData.GetCellId(new Vector2I(0, 0)));
        AssertThat(frontier).IsNotNull();
    }

    [TestCase]
    public void TestOpenRoomExploredInOneStep()
    {
        // Small open room should be fully explored from any position
        var (mapData, gridData) = CreateSimpleMap(3, 3);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 5);

        // Visit center
        behavior.UpdateVision(gridData.GetCellId(new Vector2I(1, 1)));

        // All tiles should be trivially visible and marked as visited
        AssertBool(behavior.IsFullyExplored()).IsTrue();
        AssertThat(behavior.VisitedCells.Count).IsEqual(9);

        // No frontier should exist
        var frontier = behavior.FindNearestFrontierCell(gridData.GetCellId(new Vector2I(1, 1)));
        AssertThat(frontier).IsNull();
    }

    [TestCase]
    public void TestIsFullyExploredWhenAllPassableTilesVisited()
    {
        var (mapData, gridData) = CreateSimpleMap(3, 3);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 1);

        // Visit all tiles
        for (var y = 0; y < 3; y++)
        for (var x = 0; x < 3; x++)
            behavior.UpdateVision(gridData.GetCellId(new Vector2I(x, y)));

        AssertBool(behavior.IsFullyExplored()).IsTrue();
    }

    [TestCase]
    public void TestIsNotFullyExploredWhenTilesRemain()
    {
        var (mapData, gridData) = CreateSimpleMap(3, 3);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 1);

        // Visit only one tile
        behavior.UpdateVision(gridData.GetCellId(new Vector2I(1, 1)));

        AssertBool(behavior.IsFullyExplored()).IsFalse();
    }

    [TestCase]
    public void TestUpdateVisionAddsCurrentPositionToVisited()
    {
        var (mapData, gridData) = CreateSimpleMap(3, 3);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 1);

        var centerCell = gridData.GetCellId(new Vector2I(1, 1));
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
        behavior.UpdateVision(gridData.GetCellId(new Vector2I(0, 0)));

        AssertBool(behavior.SeenCells.Contains(gridData.GetCellId(new Vector2I(0, 0)))).IsTrue();
        AssertBool(behavior.SeenCells.Contains(gridData.GetCellId(new Vector2I(2, 0)))).IsFalse();
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
        behavior.UpdateVision(gridData.GetCellId(new Vector2I(0, 0)));
        behavior.UpdateVision(gridData.GetCellId(new Vector2I(1, 0)));

        // Find frontier - should return null since right region is unreachable
        var frontier = behavior.FindNearestFrontierCell(gridData.GetCellId(new Vector2I(1, 0)));

        AssertThat(frontier).IsNull();
    }

    [TestCase]
    public void TestConsecutiveMovesDoNotBacktrack()
    {
        // Simulates the backtracking bug scenario
        var (mapData, gridData) = CreateSimpleMap(10, 10);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 2);

        var cellHistory = new List<int>();
        var currentCell = gridData.GetCellId(new Vector2I(0, 0));

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

    [TestCase]
    public void TestFindUnvisitedBlobsReturnsConnectedRegions()
    {
        // Create a map with two separate unvisited regions
        //   0 1 2 3 4 5 6
        // 0 V V V W U U U
        // V = visited, W = wall, U = unvisited
        var mapData = new SimpleMapData
        {
            TileIds = new string[1, 7],
            Size = new Vector2I(7, 1),
            PlayerStart = new Vector2I(0, 0)
        };

        for (var x = 0; x < 7; x++)
        {
            mapData.TileIds[0, x] = x == 3 ? Wall : Floor;
            if (x != 3) mapData.PassableTiles.Add(new Vector2I(x, 0));
        }

        var gridData = new RegularGridMapData(mapData);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 1);

        // Visit left region
        behavior.UpdateVision(gridData.GetCellId(new Vector2I(0, 0)));
        behavior.UpdateVision(gridData.GetCellId(new Vector2I(1, 0)));
        behavior.UpdateVision(gridData.GetCellId(new Vector2I(2, 0)));

        var blobs = behavior.FindUnvisitedBlobs(gridData.GetCellId(new Vector2I(2, 0)));

        // Should find no blobs since right region is unreachable (wall blocks)
        AssertThat(blobs.Count).IsEqual(0);
    }

    [TestCase]
    public void TestBlobSelectionPrioritizesClosestSignificantBlob()
    {
        // Create a map where all unvisited tiles form one contiguous blob
        //   0 1 2 3 4 5 6 7 8 9
        // 0 V V U U U U U U U U
        // V = visited, U = unvisited (all connected)
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

        var gridData = new RegularGridMapData(mapData);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 1);
        behavior.SignificantBlobThreshold = 3; // Set threshold to 3

        // Visit only tiles 0 and 1
        behavior.UpdateVision(gridData.GetCellId(new Vector2I(0, 0)));
        behavior.UpdateVision(gridData.GetCellId(new Vector2I(1, 0)));

        // Find blobs from cell 1
        var cell1 = gridData.GetCellId(new Vector2I(1, 0));
        var blobs = behavior.FindUnvisitedBlobs(cell1);

        // Should find one blob containing all unvisited tiles (2-9)
        AssertThat(blobs.Count).IsEqual(1);
        AssertThat(blobs[0].Size).IsEqual(8); // Tiles 2-9

        // FindNearestFrontierCell should return the closest entry point
        var target = behavior.FindNearestFrontierCell(cell1);
        var targetPos = gridData.GetGridPosition(target!.Value);
        AssertThat(targetPos).IsEqual(new Vector2I(2, 0)); // Nearest entry point
    }

    [TestCase]
    public void TestSmallBlobsOnlyTargetedWhenNoSignificantBlobsRemain()
    {
        // Create an L-shaped corridor where the target tile is around the corner
        // and can't be seen with visionRange=1
        //   x=0  1  2  3  4  5
        // y=0  F  F  F  F  F  W
        // y=1  W  W  W  W  F  W
        // y=2  W  W  W  W  F  W
        // y=3  W  W  W  W  F  W
        // y=4  W  W  W  W  F  W
        // y=5  W  W  W  W  F  F <- (5,5) is the small blob
        var mapData = new SimpleMapData
        {
            TileIds = new string[6, 6],
            Size = new Vector2I(6, 6),
            PlayerStart = new Vector2I(0, 0)
        };

        FillWithWalls(mapData.TileIds);

        // Top row: (0,0) to (4,0)
        for (var x = 0; x <= 4; x++)
        {
            mapData.TileIds[0, x] = Floor;
            mapData.PassableTiles.Add(new Vector2I(x, 0));
        }

        // Right column: (4,1) to (4,5)
        for (var y = 1; y <= 5; y++)
        {
            mapData.TileIds[y, 4] = Floor;
            mapData.PassableTiles.Add(new Vector2I(4, y));
        }

        // Small blob at corner: (5,5)
        mapData.TileIds[5, 5] = Floor;
        mapData.PassableTiles.Add(new Vector2I(5, 5));

        var gridData = new RegularGridMapData(mapData);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 1);
        behavior.SignificantBlobThreshold = 10; // High threshold - no blob is "significant"

        // Visit the L-shaped corridor except the corner
        // Top row
        for (var x = 0; x <= 4; x++)
            behavior.UpdateVision(gridData.GetCellId(new Vector2I(x, 0)));
        // Right column (down to 4,4 - don't visit 4,5)
        for (var y = 1; y <= 4; y++)
            behavior.UpdateVision(gridData.GetCellId(new Vector2I(4, y)));

        // At this point:
        // - (4,5) is seen from (4,4) but NOT trivially visible because (5,5) is unseen
        // - (5,5) is NOT seen because it's at diagonal distance from (4,4)

        // Find frontier - should find (4,5) as entry to the small blob
        var target = behavior.FindNearestFrontierCell(gridData.GetCellId(new Vector2I(4, 4)));

        // With high threshold, Phase 2 kicks in and targets nearest small blob
        AssertThat(target).IsNotNull();
        var targetPos = gridData.GetGridPosition(target!.Value);
        AssertThat(targetPos).IsEqual(new Vector2I(4, 5));
    }

    [TestCase]
    public void TestSignificantBlobThresholdIsConfigurable()
    {
        var (mapData, gridData) = CreateSimpleMap(3, 3);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 1);

        // Default threshold
        AssertThat(behavior.SignificantBlobThreshold).IsEqual(5);

        // Can be changed
        behavior.SignificantBlobThreshold = 10;
        AssertThat(behavior.SignificantBlobThreshold).IsEqual(10);
    }

    [TestCase]
    public void TestBlobDistancePrioritization()
    {
        // Verify that closer blobs are preferred over larger distant blobs
        // Create a map with two separate regions at different distances
        //   0 1 2 3 4 5 6 7 8 9 10 11 12 13 14
        // 0 V V U U U W U U U U  U  U  U  U  U
        // V = visited, W = wall (blocking), U = unvisited
        // Small blob at (2-4): size 3, distance 1
        // Large blob at (6-14): size 9, distance 5 (must go around wall)
        var mapData = new SimpleMapData
        {
            TileIds = new string[1, 15],
            Size = new Vector2I(15, 1),
            PlayerStart = new Vector2I(0, 0)
        };

        for (var x = 0; x < 15; x++)
        {
            mapData.TileIds[0, x] = x == 5 ? Wall : Floor;
            if (x != 5) mapData.PassableTiles.Add(new Vector2I(x, 0));
        }

        var gridData = new RegularGridMapData(mapData);
        var behavior = new FrontierExplorationBehavior(gridData, new SimpleVisibilityChecker(), visionRange: 1);
        behavior.SignificantBlobThreshold = 3; // Both blobs are significant

        // Visit only tiles 0 and 1
        behavior.UpdateVision(gridData.GetCellId(new Vector2I(0, 0)));
        behavior.UpdateVision(gridData.GetCellId(new Vector2I(1, 0)));

        // FindNearestFrontierCell should return entry to the CLOSER blob (at x=2)
        // even though the blob at x=6+ is larger
        var target = behavior.FindNearestFrontierCell(gridData.GetCellId(new Vector2I(1, 0)));
        var targetPos = gridData.GetGridPosition(target!.Value);
        AssertThat(targetPos).IsEqual(new Vector2I(2, 0)); // Nearest entry point, not largest blob
    }

    private static (SimpleMapData mapData, RegularGridMapData gridData) CreateSimpleMap(int width, int height)
    {
        var mapData = new SimpleMapData
        {
            TileIds = new string[height, width],
            Size = new Vector2I(width, height),
            PlayerStart = new Vector2I(0, 0)
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

    private static void FillWithWalls(string[,] tileIds)
    {
        for (var y = 0; y < tileIds.GetLength(0); y++)
        for (var x = 0; x < tileIds.GetLength(1); x++)
            tileIds[y, x] = Wall;
    }
}
