using CardCleaner.Scripts.Features.Deckbuilder.Services;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Deckbuilder.Services;

[TestSuite]
[RequireGodotRuntime]
public class SimpleMapDataTest
{
    // Test-local tile IDs (tests don't depend on specific values, just consistent usage)
    private const string Floor = "floor";
    private const string Wall = "wall";

    [TestCase]
    public void TestDefaultInitialization()
    {
        var mapData = new SimpleMapData();

        AssertThat(mapData.TileIds).IsNotNull();
        AssertThat(mapData.TileIds.GetLength(0)).IsEqual(0);
        AssertThat(mapData.Size).IsEqual(new Vector2I(0, 0));
        AssertThat(mapData.PlayerStart).IsEqual(new Vector2I(0, 0));
        AssertThat(mapData.EnemyPositions).IsNotNull();
        AssertThat(mapData.EnemyPositions.Count).IsEqual(0);
        AssertThat(mapData.PassableTiles).IsNotNull();
        AssertThat(mapData.PassableTiles.Count).IsEqual(0);
    }

    [TestCase]
    public void TestPropertyAssignment()
    {
        var tileIds = new string[10, 15];
        var size = new Vector2I(15, 10);
        var playerStart = new Vector2I(5, 3);

        var mapData = new SimpleMapData
        {
            TileIds = tileIds,
            Size = size,
            PlayerStart = playerStart
        };

        AssertBool(ReferenceEquals(mapData.TileIds, tileIds)).IsTrue();
        AssertThat(mapData.Size).IsEqual(size);
        AssertThat(mapData.PlayerStart).IsEqual(playerStart);
    }

    [TestCase]
    public void TestIsPassableWithPassableTile()
    {
        var mapData = CreateMapData(5, 5);
        mapData.TileIds[2, 3] = Floor;

        AssertBool(mapData.IsPassable(new Vector2I(3, 2))).IsTrue();
    }

    [TestCase]
    public void TestIsPassableWithBlockedTile()
    {
        var mapData = CreateMapData(5, 5);
        mapData.TileIds[1, 2] = Wall;

        AssertBool(mapData.IsPassable(new Vector2I(2, 1))).IsFalse();
    }

    [TestCase]
    public void TestIsPassableNegativeX()
    {
        var mapData = CreateMapData(5, 5);

        AssertBool(mapData.IsPassable(new Vector2I(-1, 2))).IsFalse();
    }

    [TestCase]
    public void TestIsPassableNegativeY()
    {
        var mapData = CreateMapData(5, 5);

        AssertBool(mapData.IsPassable(new Vector2I(2, -1))).IsFalse();
    }

    [TestCase]
    public void TestIsPassableExceedsMaxX()
    {
        var mapData = CreateMapData(5, 5);

        AssertBool(mapData.IsPassable(new Vector2I(5, 2))).IsFalse();
        AssertBool(mapData.IsPassable(new Vector2I(10, 2))).IsFalse();
    }

    [TestCase]
    public void TestIsPassableExceedsMaxY()
    {
        var mapData = CreateMapData(5, 5);

        AssertBool(mapData.IsPassable(new Vector2I(2, 5))).IsFalse();
        AssertBool(mapData.IsPassable(new Vector2I(2, 10))).IsFalse();
    }

    [TestCase]
    public void TestIsPassableBoundaryPositions()
    {
        var mapData = CreateMapData(5, 5);
        mapData.TileIds[0, 0] = Floor;
        mapData.TileIds[0, 4] = Floor;
        mapData.TileIds[4, 0] = Floor;
        mapData.TileIds[4, 4] = Floor;

        AssertBool(mapData.IsPassable(new Vector2I(0, 0))).IsTrue();
        AssertBool(mapData.IsPassable(new Vector2I(4, 0))).IsTrue();
        AssertBool(mapData.IsPassable(new Vector2I(0, 4))).IsTrue();
        AssertBool(mapData.IsPassable(new Vector2I(4, 4))).IsTrue();
    }

    [TestCase]
    public void TestIsPassableCoordinateMapping()
    {
        var mapData = new SimpleMapData
        {
            TileIds = new string[3, 4],
            Size = new Vector2I(4, 3)
        };
        FillWithWalls(mapData.TileIds);
        mapData.TileIds[1, 2] = Floor;

        AssertBool(mapData.IsPassable(new Vector2I(2, 1))).IsTrue();
        AssertBool(mapData.IsPassable(new Vector2I(1, 2))).IsFalse();
    }

    [TestCase]
    public void TestEnemyPositionsCanBePopulated()
    {
        var mapData = new SimpleMapData();
        var enemy1 = new Vector2I(3, 4);
        var enemy2 = new Vector2I(7, 2);

        mapData.EnemyPositions.Add(enemy1);
        mapData.EnemyPositions.Add(enemy2);

        AssertThat(mapData.EnemyPositions.Count).IsEqual(2);
        AssertThat(mapData.EnemyPositions[0]).IsEqual(enemy1);
        AssertThat(mapData.EnemyPositions[1]).IsEqual(enemy2);
    }

    [TestCase]
    public void TestPassableTilesCanBePopulated()
    {
        var mapData = new SimpleMapData();
        var tile1 = new Vector2I(0, 0);
        var tile2 = new Vector2I(5, 5);

        mapData.PassableTiles.Add(tile1);
        mapData.PassableTiles.Add(tile2);

        AssertThat(mapData.PassableTiles.Count).IsEqual(2);
        AssertThat(mapData.PassableTiles[0]).IsEqual(tile1);
        AssertThat(mapData.PassableTiles[1]).IsEqual(tile2);
    }

    [TestCase]
    public void TestNonSquareGrid()
    {
        var mapData = new SimpleMapData
        {
            TileIds = new string[7, 12],
            Size = new Vector2I(12, 7)
        };
        FillWithWalls(mapData.TileIds);
        mapData.TileIds[3, 8] = Floor;
        mapData.TileIds[6, 11] = Floor;

        AssertBool(mapData.IsPassable(new Vector2I(8, 3))).IsTrue();
        AssertBool(mapData.IsPassable(new Vector2I(11, 6))).IsTrue();
        AssertBool(mapData.IsPassable(new Vector2I(12, 6))).IsFalse();
        AssertBool(mapData.IsPassable(new Vector2I(11, 7))).IsFalse();
    }

    [TestCase]
    public void TestMinimalGridSize()
    {
        var mapData = CreateMapData(1, 1);
        mapData.TileIds[0, 0] = Floor;

        AssertBool(mapData.IsPassable(new Vector2I(0, 0))).IsTrue();
        AssertBool(mapData.IsPassable(new Vector2I(1, 0))).IsFalse();
        AssertBool(mapData.IsPassable(new Vector2I(0, 1))).IsFalse();
    }

    [TestCase]
    public void TestLargeGridSize()
    {
        var mapData = CreateMapData(100, 100);
        mapData.TileIds[99, 99] = Floor;

        AssertBool(mapData.IsPassable(new Vector2I(99, 99))).IsTrue();
        AssertBool(mapData.IsPassable(new Vector2I(100, 100))).IsFalse();
    }

    [TestCase]
    public void TestGetTileIdReturnsCorrectId()
    {
        var mapData = CreateMapData(5, 5);
        mapData.TileIds[2, 3] = Floor;
        mapData.TileIds[1, 1] = Wall;

        AssertThat(mapData.GetTileId(new Vector2I(3, 2))).IsEqual(Floor);
        AssertThat(mapData.GetTileId(new Vector2I(1, 1))).IsEqual(Wall);
    }

    [TestCase]
    public void TestGetTileIdOutOfBoundsReturnsWall()
    {
        var mapData = CreateMapData(5, 5);

        AssertThat(mapData.GetTileId(new Vector2I(-1, 0))).IsEqual(Wall);
        AssertThat(mapData.GetTileId(new Vector2I(0, -1))).IsEqual(Wall);
        AssertThat(mapData.GetTileId(new Vector2I(5, 0))).IsEqual(Wall);
        AssertThat(mapData.GetTileId(new Vector2I(0, 5))).IsEqual(Wall);
    }

    [TestCase]
    public void TestIsTransparentForPassableTile()
    {
        var mapData = CreateMapData(5, 5);
        mapData.TileIds[2, 2] = Floor;

        AssertBool(mapData.IsTransparent(new Vector2I(2, 2))).IsTrue();
    }

    [TestCase]
    public void TestIsTransparentForBlockedTile()
    {
        var mapData = CreateMapData(5, 5);
        mapData.TileIds[2, 2] = Wall;

        AssertBool(mapData.IsTransparent(new Vector2I(2, 2))).IsFalse();
    }

    private static SimpleMapData CreateMapData(int width, int height)
    {
        var mapData = new SimpleMapData
        {
            TileIds = new string[height, width],
            Size = new Vector2I(width, height)
        };
        FillWithWalls(mapData.TileIds);
        return mapData;
    }

    private static void FillWithWalls(string[,] tileIds)
    {
        for (var y = 0; y < tileIds.GetLength(0); y++)
        for (var x = 0; x < tileIds.GetLength(1); x++)
            tileIds[y, x] = Wall;
    }
}
