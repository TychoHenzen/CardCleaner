using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Tests.TestUtilities.Fixtures;
using Godot;

namespace CardCleaner.Tests.Core.Services;

[TestSuite]
[RequireGodotRuntime]
public class SimpleVisibilityCheckerTest
{
    // Test-local tile IDs (tests don't depend on specific values, just consistent usage)
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
        var (_, gridData) = OpenFloorMap.Create(5, 5);
        var position = new Vector2I(2, 2);
        var cellId = gridData.GetCellId(position);

        var result = _checker.CanSee(cellId, cellId, gridData);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestCanSeeAdjacentTile()
    {
        var (_, gridData) = OpenFloorMap.Create(5, 5);

        var result = _checker.CanSee(
            gridData.GetCellId(new Vector2I(2, 2)),
            gridData.GetCellId(new Vector2I(3, 2)),
            gridData);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestCanSeeDiagonalTile()
    {
        var (_, gridData) = OpenFloorMap.Create(5, 5);

        var result = _checker.CanSee(
            gridData.GetCellId(new Vector2I(0, 0)),
            gridData.GetCellId(new Vector2I(4, 4)),
            gridData);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestCannotSeeThroughWall()
    {
        var (mapData, gridData) = OpenFloorMap.Create(5, 1);
        // Place wall in the middle - remove from passable tiles
        SetWall(mapData, 2, 0);

        var result = _checker.CanSee(
            gridData.GetCellId(new Vector2I(0, 0)),
            gridData.GetCellId(new Vector2I(4, 0)),
            gridData);

        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void TestCanSeeUpToWall()
    {
        var (mapData, gridData) = OpenFloorMap.Create(5, 1);
        // Place wall in the middle
        SetWall(mapData, 2, 0);

        // Can see the wall tile itself
        var result = _checker.CanSee(
            gridData.GetCellId(new Vector2I(0, 0)),
            gridData.GetCellId(new Vector2I(2, 0)),
            gridData);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestCanSeeFromWallPosition()
    {
        var (mapData, gridData) = OpenFloorMap.Create(5, 1);
        SetWall(mapData, 0, 0);

        // Vision check starting from a wall position should still work
        var result = _checker.CanSee(
            gridData.GetCellId(new Vector2I(0, 0)),
            gridData.GetCellId(new Vector2I(2, 0)),
            gridData);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestCannotSeeThroughMultipleWalls()
    {
        var (mapData, gridData) = OpenFloorMap.Create(5, 5);
        // Create a wall barrier
        SetWall(mapData, 2, 0);
        SetWall(mapData, 2, 1);
        SetWall(mapData, 2, 2);
        SetWall(mapData, 2, 3);
        SetWall(mapData, 2, 4);

        var result = _checker.CanSee(
            gridData.GetCellId(new Vector2I(0, 0)),
            gridData.GetCellId(new Vector2I(4, 0)),
            gridData);

        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void TestCanSeeAroundWall()
    {
        var (mapData, gridData) = OpenFloorMap.Create(5, 5);
        // Wall at (2,2)
        SetWall(mapData, 2, 2);

        // Should still see (4,0) from (0,0) - wall is not in direct line
        var result = _checker.CanSee(
            gridData.GetCellId(new Vector2I(0, 0)),
            gridData.GetCellId(new Vector2I(4, 0)),
            gridData);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestVerticalLineOfSight()
    {
        var (_, gridData) = OpenFloorMap.Create(1, 5);

        var result = _checker.CanSee(
            gridData.GetCellId(new Vector2I(0, 0)),
            gridData.GetCellId(new Vector2I(0, 4)),
            gridData);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestVerticalLineOfSightBlockedByWall()
    {
        var (mapData, gridData) = OpenFloorMap.Create(1, 5);
        SetWall(mapData, 0, 2);

        var result = _checker.CanSee(
            gridData.GetCellId(new Vector2I(0, 0)),
            gridData.GetCellId(new Vector2I(0, 4)),
            gridData);

        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void TestBresenhamLineSymmetry()
    {
        var (mapData, gridData) = OpenFloorMap.Create(10, 10);
        SetWall(mapData, 5, 5);

        // Line from (0,0) to (8,6) should give same result as (8,6) to (0,0)
        var from1 = gridData.GetCellId(new Vector2I(0, 0));
        var to1 = gridData.GetCellId(new Vector2I(8, 6));

        var forward = _checker.CanSee(from1, to1, gridData);
        var backward = _checker.CanSee(to1, from1, gridData);

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
        var (mapData, gridData) = OpenFloorMap.Create(3, 3);
        SetWall(mapData, 1, 0);
        SetWall(mapData, 0, 1);

        // Cannot see through the diagonal corner
        var result = _checker.CanSee(
            gridData.GetCellId(new Vector2I(0, 0)),
            gridData.GetCellId(new Vector2I(2, 2)),
            gridData);

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
        var (mapData, gridData) = OpenFloorMap.Create(3, 3);
        SetWall(mapData, 1, 0);

        // Can see through a partial corner (one wall is not enough to block)
        var result = _checker.CanSee(
            gridData.GetCellId(new Vector2I(0, 0)),
            gridData.GetCellId(new Vector2I(2, 2)),
            gridData);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestCornerBlockingSymmetry()
    {
        // Corner blocking should be symmetric
        var (mapData, gridData) = OpenFloorMap.Create(3, 3);
        SetWall(mapData, 1, 0);
        SetWall(mapData, 0, 1);

        var from = gridData.GetCellId(new Vector2I(0, 0));
        var to = gridData.GetCellId(new Vector2I(2, 2));

        var forward = _checker.CanSee(from, to, gridData);
        var backward = _checker.CanSee(to, from, gridData);

        AssertThat(forward).IsEqual(backward);
        AssertBool(forward).IsFalse();
    }

    /// <summary>
    /// Sets a tile as a wall (not passable, not transparent).
    /// Updates TileIds and removes from PassableTiles.
    /// Note: RegularGridMapData caches passable tiles, so create it after setting walls
    /// if the grid needs to reflect wall passability.
    /// </summary>
    private static void SetWall(SimpleMapData mapData, int x, int y)
    {
        mapData.TileIds[y, x] = Wall;
        mapData.PassableTiles.Remove(new Vector2I(x, y));
    }
}
