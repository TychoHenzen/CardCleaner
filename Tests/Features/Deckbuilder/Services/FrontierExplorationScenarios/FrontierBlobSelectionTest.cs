using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Tests.TestUtilities.Fixtures;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.FrontierExplorationScenarios;

/// <summary>
///     FrontierExplorationBehavior unvisited-blob selection scenarios split out of FrontierExplorationBehaviorTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FrontierBlobSelectionTest : FrontierExplorationTestBase
{
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
        var mapData = CreateLShapedCorridorMap();

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
        var (mapData, gridData) = OpenFloorMap.Create(3, 3);
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
}
