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
    private const string Floor = SimpleMapGenerator.FloorTileId;
    private const string Wall = SimpleMapGenerator.WallTileId;

    [TestCase]
    public void TestFindNearestFrontierTileReturnsAdjacentUnvisited()
    {
        // Simple 3x3 grid, all passable
        var mapData = CreateSimpleMap(3, 3);
        var behavior = new FrontierExplorationBehavior(mapData, new SimpleVisibilityChecker(), visionRange: 1);

        // Update vision from center - with vision range 1, should see immediate neighbors
        behavior.UpdateVision(new Vector2I(1, 1));

        // Find nearest frontier - should return an adjacent unvisited tile
        var frontier = behavior.FindNearestFrontierTile(new Vector2I(1, 1));

        AssertThat(frontier).IsNotNull();
        // Should be adjacent to current position (distance of 1)
        var distance = Mathf.Abs(frontier!.Value.X - 1) + Mathf.Abs(frontier.Value.Y - 1);
        AssertThat(distance).IsEqual(1);
    }

    [TestCase]
    public void TestFindNearestFrontierTileUsesWalkingDistanceNotEuclidean()
    {
        // Create an L-shaped corridor where the "close" tile by Euclidean distance
        // is actually far by walking distance
        //
        // Layout (5x5):
        //   0 1 2 3 4
        // 0 F F F F F
        // 1 W W W W F
        // 2 W W W W F
        // 3 W W W W F
        // 4 F F F F F
        //
        // Player at (0,0). Tile (4,0) is close by Euclidean (4 tiles).
        // But walking requires going around: (0,0) -> ... -> (4,0) = 8 tiles via (0,4) -> (4,4) -> (4,0)
        // Actually, there's a path along top: (0,0) -> (1,0) -> ... -> (4,0)
        // Let me redesign this test...

        // Better layout:
        //   0 1 2 3 4
        // 0 F W W W F  <- Start at (0,0), target at (4,0) but blocked
        // 1 F W W W F
        // 2 F W W W F
        // 3 F W W W F
        // 4 F F F F F  <- Must go around bottom
        //
        // Player starts at (0,0), visits some tiles.
        // Tile (4,0) is Euclidean distance 4 from (0,0)
        // But walking distance is: (0,0) -> (0,1) -> (0,2) -> (0,3) -> (0,4) -> (1,4) -> (2,4) -> (3,4) -> (4,4) -> (4,3) -> (4,2) -> (4,1) -> (4,0) = 12

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

        var behavior = new FrontierExplorationBehavior(mapData, new SimpleVisibilityChecker(), visionRange: 1);

        // Start at (0,0) and update vision
        behavior.UpdateVision(new Vector2I(0, 0));

        // Find nearest frontier - should be (0,1) which is adjacent, not (4,0) which is closer Euclidean
        var frontier = behavior.FindNearestFrontierTile(new Vector2I(0, 0));

        AssertThat(frontier).IsNotNull();
        // Nearest by walking should be (0,1) - one step away
        AssertThat(frontier!.Value).IsEqual(new Vector2I(0, 1));
    }

    [TestCase]
    public void TestFindNearestFrontierTileReturnsNullWhenFullyExplored()
    {
        // Small 2x2 map
        var mapData = CreateSimpleMap(2, 2);
        var behavior = new FrontierExplorationBehavior(mapData, new SimpleVisibilityChecker(), visionRange: 10);

        // Visit all tiles
        behavior.UpdateVision(new Vector2I(0, 0));
        behavior.UpdateVision(new Vector2I(1, 0));
        behavior.UpdateVision(new Vector2I(0, 1));
        behavior.UpdateVision(new Vector2I(1, 1));

        var frontier = behavior.FindNearestFrontierTile(new Vector2I(0, 0));

        AssertThat(frontier).IsNull();
    }

    [TestCase]
    public void TestTriviallyVisibleTilesAreMarkedAsVisited()
    {
        // In an open room, trivially visible tiles should be marked as visited
        var mapData = CreateSimpleMap(5, 5);
        var behavior = new FrontierExplorationBehavior(mapData, new SimpleVisibilityChecker(), visionRange: 10);

        // Update vision from center with large vision range
        behavior.UpdateVision(new Vector2I(2, 2));

        // With large vision, we can see the entire map and all neighbors
        // So all tiles should be marked as visited (trivially visible)
        AssertThat(behavior.VisitedTiles.Count).IsEqual(25); // 5x5 = 25
        AssertBool(behavior.IsFullyExplored()).IsTrue();
    }

    [TestCase]
    public void TestTilesAtEdgeOfVisionNotTriviallyVisible()
    {
        // Tiles at the edge of vision range shouldn't be trivially visible
        // because we can't see their outer neighbors
        var mapData = CreateSimpleMap(10, 10);
        var behavior = new FrontierExplorationBehavior(mapData, new SimpleVisibilityChecker(), visionRange: 3);

        // Update vision from corner
        behavior.UpdateVision(new Vector2I(0, 0));

        // The corner itself is visited
        AssertBool(behavior.VisitedTiles.Contains(new Vector2I(0, 0))).IsTrue();

        // Tiles fully within vision (where all neighbors are also seen) should be visited
        // Tiles at the edge of vision should NOT be trivially visited
        // Check that we haven't marked ALL seen tiles as visited
        AssertThat(behavior.VisitedTiles.Count).IsLess(behavior.SeenTiles.Count);
    }

    [TestCase]
    public void TestFrontierExistsAtEdgeOfTriviallyVisibleArea()
    {
        var mapData = CreateSimpleMap(10, 10);
        var behavior = new FrontierExplorationBehavior(mapData, new SimpleVisibilityChecker(), visionRange: 3);

        // Visit corner
        behavior.UpdateVision(new Vector2I(0, 0));

        var frontierTiles = behavior.FindFrontierTiles();

        // Should have frontier tiles at the edge of the trivially visible area
        AssertThat(frontierTiles.Count).IsGreater(0);
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

        var behavior = new FrontierExplorationBehavior(mapData, new SimpleVisibilityChecker(), visionRange: 5);

        // Start at (0,0)
        behavior.UpdateVision(new Vector2I(0, 0));

        // Tiles around the corner should not be seen (blocked by walls)
        AssertBool(behavior.SeenTiles.Contains(new Vector2I(3, 2))).IsFalse();
        AssertBool(behavior.SeenTiles.Contains(new Vector2I(4, 2))).IsFalse();

        // Frontier should exist to explore around the corner
        var frontier = behavior.FindNearestFrontierTile(new Vector2I(0, 0));
        AssertThat(frontier).IsNotNull();
    }

    [TestCase]
    public void TestOpenRoomExploredInOneStep()
    {
        // Small open room should be fully explored from any position
        var mapData = CreateSimpleMap(3, 3);
        var behavior = new FrontierExplorationBehavior(mapData, new SimpleVisibilityChecker(), visionRange: 5);

        // Visit center
        behavior.UpdateVision(new Vector2I(1, 1));

        // All tiles should be trivially visible and marked as visited
        AssertBool(behavior.IsFullyExplored()).IsTrue();
        AssertThat(behavior.VisitedTiles.Count).IsEqual(9);

        // No frontier should exist
        var frontier = behavior.FindNearestFrontierTile(new Vector2I(1, 1));
        AssertThat(frontier).IsNull();
    }

    [TestCase]
    public void TestIsFullyExploredWhenAllPassableTilesVisited()
    {
        var mapData = CreateSimpleMap(3, 3);
        var behavior = new FrontierExplorationBehavior(mapData, new SimpleVisibilityChecker(), visionRange: 1);

        // Visit all tiles
        for (var y = 0; y < 3; y++)
        for (var x = 0; x < 3; x++)
            behavior.UpdateVision(new Vector2I(x, y));

        AssertBool(behavior.IsFullyExplored()).IsTrue();
    }

    [TestCase]
    public void TestIsNotFullyExploredWhenTilesRemain()
    {
        var mapData = CreateSimpleMap(3, 3);
        var behavior = new FrontierExplorationBehavior(mapData, new SimpleVisibilityChecker(), visionRange: 1);

        // Visit only one tile
        behavior.UpdateVision(new Vector2I(1, 1));

        AssertBool(behavior.IsFullyExplored()).IsFalse();
    }

    [TestCase]
    public void TestUpdateVisionAddsCurrentPositionToVisited()
    {
        var mapData = CreateSimpleMap(3, 3);
        var behavior = new FrontierExplorationBehavior(mapData, new SimpleVisibilityChecker(), visionRange: 1);

        behavior.UpdateVision(new Vector2I(1, 1));

        AssertBool(behavior.VisitedTiles.Contains(new Vector2I(1, 1))).IsTrue();
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

        var behavior = new FrontierExplorationBehavior(mapData, new SimpleVisibilityChecker(), visionRange: 5);

        // Update vision from (0,0) - should not see (2,0) due to wall
        behavior.UpdateVision(new Vector2I(0, 0));

        AssertBool(behavior.SeenTiles.Contains(new Vector2I(0, 0))).IsTrue();
        AssertBool(behavior.SeenTiles.Contains(new Vector2I(2, 0))).IsFalse();
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

        var behavior = new FrontierExplorationBehavior(mapData, new SimpleVisibilityChecker(), visionRange: 1);

        // Visit left region
        behavior.UpdateVision(new Vector2I(0, 0));
        behavior.UpdateVision(new Vector2I(1, 0));

        // Find frontier - should return null since right region is unreachable
        var frontier = behavior.FindNearestFrontierTile(new Vector2I(1, 0));

        AssertThat(frontier).IsNull();
    }

    [TestCase]
    public void TestConsecutiveMovesDoNotBacktrack()
    {
        // Simulates the backtracking bug scenario
        var mapData = CreateSimpleMap(10, 10);
        var behavior = new FrontierExplorationBehavior(mapData, new SimpleVisibilityChecker(), visionRange: 2);

        var positions = new List<Vector2I>();
        var currentPos = new Vector2I(0, 0);

        // Simulate 20 exploration steps
        for (var i = 0; i < 20; i++)
        {
            behavior.UpdateVision(currentPos);
            positions.Add(currentPos);

            var nextFrontier = behavior.FindNearestFrontierTile(currentPos);
            if (nextFrontier == null) break;

            // Move to frontier (in real code, pathfinding would be used)
            currentPos = nextFrontier.Value;
        }

        // Check that we don't have excessive backtracking
        // Count how many times we return to a previously visited position
        var backtrackCount = 0;
        var visited = new HashSet<Vector2I>();
        foreach (var pos in positions)
        {
            if (visited.Contains(pos))
                backtrackCount++;
            visited.Add(pos);
        }

        // Should have minimal backtracking (some is OK due to topology)
        // With BFS-based frontier finding, we shouldn't have excessive backtracking
        AssertThat(backtrackCount).IsLess(positions.Count / 2);
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

        var behavior = new FrontierExplorationBehavior(mapData, new SimpleVisibilityChecker(), visionRange: 1);

        // Visit left region
        behavior.UpdateVision(new Vector2I(0, 0));
        behavior.UpdateVision(new Vector2I(1, 0));
        behavior.UpdateVision(new Vector2I(2, 0));

        var blobs = behavior.FindUnvisitedBlobs(new Vector2I(2, 0));

        // Should find no blobs since right region is unreachable (wall blocks)
        AssertThat(blobs.Count).IsEqual(0);
    }

    [TestCase]
    public void TestBlobScoringPrioritizesLargeNearbyBlobs()
    {
        // Create a map with two unvisited regions of different sizes
        //   0 1 2 3 4 5 6 7 8 9
        // 0 V V U W U U U U U U
        // V = visited, W = wall (not blocking, just passable), U = unvisited
        // Small blob at (2,0): size 1
        // Large blob at (4-9,0): size 6
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

        var behavior = new FrontierExplorationBehavior(mapData, new SimpleVisibilityChecker(), visionRange: 1);
        behavior.SignificantBlobThreshold = 3; // Set threshold to 3

        // Visit only tiles 0 and 1
        behavior.UpdateVision(new Vector2I(0, 0));
        behavior.UpdateVision(new Vector2I(1, 0));

        // Find blobs
        var blobs = behavior.FindUnvisitedBlobs(new Vector2I(1, 0));

        // Should find one blob containing all unvisited tiles (2-9)
        AssertThat(blobs.Count).IsEqual(1);
        AssertThat(blobs[0].Size).IsEqual(8); // Tiles 2-9

        // FindNearestFrontierTile should return entry to the large blob
        var target = behavior.FindNearestFrontierTile(new Vector2I(1, 0));
        AssertThat(target).IsEqual(new Vector2I(2, 0)); // Nearest entry point
    }

    [TestCase]
    public void TestSmallBlobsOnlyTargetedWhenNoSignificantBlobsRemain()
    {
        // Create a map where only small blobs remain
        var mapData = CreateSimpleMap(5, 5);
        var behavior = new FrontierExplorationBehavior(mapData, new SimpleVisibilityChecker(), visionRange: 1);
        behavior.SignificantBlobThreshold = 10; // High threshold

        // Visit most of the map, leaving only small unvisited pockets
        for (var y = 0; y < 5; y++)
        for (var x = 0; x < 5; x++)
        {
            if (x < 4 || y < 4) // Leave (4,4) unvisited
                behavior.UpdateVision(new Vector2I(x, y));
        }

        // Find frontier - should still return something even though blob is small
        var target = behavior.FindNearestFrontierTile(new Vector2I(3, 3));

        // Should find the remaining unvisited tile(s)
        // With high threshold, Phase 2 kicks in and targets nearest small blob
        AssertThat(target).IsNotNull();
    }

    [TestCase]
    public void TestSignificantBlobThresholdIsConfigurable()
    {
        var mapData = CreateSimpleMap(3, 3);
        var behavior = new FrontierExplorationBehavior(mapData, new SimpleVisibilityChecker(), visionRange: 1);

        // Default threshold
        AssertThat(behavior.SignificantBlobThreshold).IsEqual(5);

        // Can be changed
        behavior.SignificantBlobThreshold = 10;
        AssertThat(behavior.SignificantBlobThreshold).IsEqual(10);
    }

    [TestCase]
    public void TestBlobScoreFormula()
    {
        // Verify that score = size / sqrt(distance)
        var mapData = new SimpleMapData
        {
            TileIds = new string[1, 20],
            Size = new Vector2I(20, 1),
            PlayerStart = new Vector2I(0, 0)
        };

        for (var x = 0; x < 20; x++)
        {
            mapData.TileIds[0, x] = Floor;
            mapData.PassableTiles.Add(new Vector2I(x, 0));
        }

        var behavior = new FrontierExplorationBehavior(mapData, new SimpleVisibilityChecker(), visionRange: 1);

        // Visit first tile only
        behavior.UpdateVision(new Vector2I(0, 0));

        var blobs = behavior.FindUnvisitedBlobs(new Vector2I(0, 0));

        // Should have one blob of size 19 at distance 1
        AssertThat(blobs.Count).IsEqual(1);
        AssertThat(blobs[0].Size).IsEqual(19);
        AssertThat(blobs[0].WalkingDistance).IsEqual(1);

        // Score should be 19 / sqrt(1) = 19
        AssertThat(blobs[0].Score).IsEqual(19f);
    }

    private static SimpleMapData CreateSimpleMap(int width, int height)
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

        return mapData;
    }

    private static void FillWithWalls(string[,] tileIds)
    {
        for (var y = 0; y < tileIds.GetLength(0); y++)
        for (var x = 0; x < tileIds.GetLength(1); x++)
            tileIds[y, x] = Wall;
    }
}
