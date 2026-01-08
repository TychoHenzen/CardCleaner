using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Core.Services;

[TestSuite]
[RequireGodotRuntime]
public class SimpleVisibilityCheckerTest
{
    // Test-local tile IDs (tests don't depend on specific values, just consistent usage)
    private const string Floor = "floor";
    private const string Wall = "wall";

    private SimpleVisibilityChecker _checker = null!;

    [BeforeTest]
    public void Setup()
    {
        _checker = new SimpleVisibilityChecker();
    }

    [TestCase]
    public void TestCanSeeSamePosition()
    {
        var mapData = CreateSimpleMap(5, 5);
        var position = new Vector2I(2, 2);

        var result = _checker.CanSee(position, position, mapData);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestCanSeeAdjacentTile()
    {
        var mapData = CreateSimpleMap(5, 5);

        var result = _checker.CanSee(new Vector2I(2, 2), new Vector2I(3, 2), mapData);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestCanSeeDiagonalTile()
    {
        var mapData = CreateSimpleMap(5, 5);

        var result = _checker.CanSee(new Vector2I(0, 0), new Vector2I(4, 4), mapData);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestCannotSeeThroughWall()
    {
        var mapData = CreateSimpleMap(5, 1);
        // Place wall in the middle
        mapData.TileIds[0, 2] = Wall;

        var result = _checker.CanSee(new Vector2I(0, 0), new Vector2I(4, 0), mapData);

        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void TestCanSeeUpToWall()
    {
        var mapData = CreateSimpleMap(5, 1);
        // Place wall in the middle
        mapData.TileIds[0, 2] = Wall;

        // Can see the wall tile itself
        var result = _checker.CanSee(new Vector2I(0, 0), new Vector2I(2, 0), mapData);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestCanSeeFromWallPosition()
    {
        var mapData = CreateSimpleMap(5, 1);
        mapData.TileIds[0, 0] = Wall;

        // Vision check starting from a wall position should still work
        var result = _checker.CanSee(new Vector2I(0, 0), new Vector2I(2, 0), mapData);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestCannotSeeThroughMultipleWalls()
    {
        var mapData = CreateSimpleMap(5, 5);
        // Create a wall barrier
        mapData.TileIds[0, 2] = Wall;
        mapData.TileIds[1, 2] = Wall;
        mapData.TileIds[2, 2] = Wall;
        mapData.TileIds[3, 2] = Wall;
        mapData.TileIds[4, 2] = Wall;

        var result = _checker.CanSee(new Vector2I(0, 0), new Vector2I(4, 0), mapData);

        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void TestCanSeeAroundWall()
    {
        var mapData = CreateSimpleMap(5, 5);
        // Wall at (2,2)
        mapData.TileIds[2, 2] = Wall;

        // Should still see (4,0) from (0,0) - wall is not in direct line
        var result = _checker.CanSee(new Vector2I(0, 0), new Vector2I(4, 0), mapData);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestVerticalLineOfSight()
    {
        var mapData = CreateSimpleMap(1, 5);

        var result = _checker.CanSee(new Vector2I(0, 0), new Vector2I(0, 4), mapData);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestVerticalLineOfSightBlockedByWall()
    {
        var mapData = CreateSimpleMap(1, 5);
        mapData.TileIds[2, 0] = Wall;

        var result = _checker.CanSee(new Vector2I(0, 0), new Vector2I(0, 4), mapData);

        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void TestBresenhamLineSymmetry()
    {
        var mapData = CreateSimpleMap(10, 10);
        mapData.TileIds[5, 5] = Wall;

        // Line from (0,0) to (8,6) should give same result as (8,6) to (0,0)
        var forward = _checker.CanSee(new Vector2I(0, 0), new Vector2I(8, 6), mapData);
        var backward = _checker.CanSee(new Vector2I(8, 6), new Vector2I(0, 0), mapData);

        AssertThat(forward).IsEqual(backward);
    }

    [TestCase]
    public void TestCannotSeeThroughDiagonalCorner()
    {
        // Create a map where two walls meet at a corner:
        //   0 1 2
        // 0 . W .
        // 1 W . .
        // 2 . . .
        // Visibility from (0,0) to (2,2) should be blocked by the corner at (1,0)-(0,1)
        var mapData = CreateSimpleMap(3, 3);
        mapData.TileIds[0, 1] = Wall; // Wall at (1, 0)
        mapData.TileIds[1, 0] = Wall; // Wall at (0, 1)

        // Cannot see through the diagonal corner
        var result = _checker.CanSee(new Vector2I(0, 0), new Vector2I(2, 2), mapData);

        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void TestCanSeeThroughPartialCorner()
    {
        // Create a map where only one wall exists at the corner:
        //   0 1 2
        // 0 . W .
        // 1 . . .
        // 2 . . .
        // Visibility from (0,0) to (2,2) should NOT be blocked (only one wall)
        var mapData = CreateSimpleMap(3, 3);
        mapData.TileIds[0, 1] = Wall; // Wall at (1, 0)

        // Can see through a partial corner (one wall is not enough to block)
        var result = _checker.CanSee(new Vector2I(0, 0), new Vector2I(2, 2), mapData);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestCornerBlockingSymmetry()
    {
        // Corner blocking should be symmetric
        var mapData = CreateSimpleMap(3, 3);
        mapData.TileIds[0, 1] = Wall; // Wall at (1, 0)
        mapData.TileIds[1, 0] = Wall; // Wall at (0, 1)

        var forward = _checker.CanSee(new Vector2I(0, 0), new Vector2I(2, 2), mapData);
        var backward = _checker.CanSee(new Vector2I(2, 2), new Vector2I(0, 0), mapData);

        AssertThat(forward).IsEqual(backward);
        AssertBool(forward).IsFalse();
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
}
