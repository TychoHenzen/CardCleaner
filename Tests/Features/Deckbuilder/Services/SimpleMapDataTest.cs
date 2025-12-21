using CardCleaner.Scripts.Features.Deckbuilder.Services;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Deckbuilder.Services;

[TestSuite]
[RequireGodotRuntime]
public class SimpleMapDataTest
{
    [TestCase]
    public void TestDefaultInitialization()
    {
        var mapData = new SimpleMapData();

        AssertThat(mapData.Grid).IsNull();
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
        var grid = new bool[10, 15];
        var size = new Vector2I(15, 10);
        var playerStart = new Vector2I(5, 3);

        var mapData = new SimpleMapData
        {
            Grid = grid,
            Size = size,
            PlayerStart = playerStart
        };

        // 2D arrays don't work with IsSame, use ReferenceEquals
        AssertBool(ReferenceEquals(mapData.Grid, grid)).IsTrue();
        AssertThat(mapData.Size).IsEqual(size);
        AssertThat(mapData.PlayerStart).IsEqual(playerStart);
    }

    [TestCase]
    public void TestIsPassableWithPassableTile()
    {
        var mapData = new SimpleMapData
        {
            Grid = new bool[5, 5],
            Size = new Vector2I(5, 5)
        };
        mapData.Grid[2, 3] = true;

        AssertBool(mapData.IsPassable(new Vector2I(3, 2))).IsTrue();
    }

    [TestCase]
    public void TestIsPassableWithBlockedTile()
    {
        var mapData = new SimpleMapData
        {
            Grid = new bool[5, 5],
            Size = new Vector2I(5, 5)
        };
        mapData.Grid[1, 2] = false;

        AssertBool(mapData.IsPassable(new Vector2I(2, 1))).IsFalse();
    }

    [TestCase]
    public void TestIsPassableNegativeX()
    {
        var mapData = new SimpleMapData
        {
            Grid = new bool[5, 5],
            Size = new Vector2I(5, 5)
        };

        AssertBool(mapData.IsPassable(new Vector2I(-1, 2))).IsFalse();
    }

    [TestCase]
    public void TestIsPassableNegativeY()
    {
        var mapData = new SimpleMapData
        {
            Grid = new bool[5, 5],
            Size = new Vector2I(5, 5)
        };

        AssertBool(mapData.IsPassable(new Vector2I(2, -1))).IsFalse();
    }

    [TestCase]
    public void TestIsPassableExceedsMaxX()
    {
        var mapData = new SimpleMapData
        {
            Grid = new bool[5, 5],
            Size = new Vector2I(5, 5)
        };

        AssertBool(mapData.IsPassable(new Vector2I(5, 2))).IsFalse();
        AssertBool(mapData.IsPassable(new Vector2I(10, 2))).IsFalse();
    }

    [TestCase]
    public void TestIsPassableExceedsMaxY()
    {
        var mapData = new SimpleMapData
        {
            Grid = new bool[5, 5],
            Size = new Vector2I(5, 5)
        };

        AssertBool(mapData.IsPassable(new Vector2I(2, 5))).IsFalse();
        AssertBool(mapData.IsPassable(new Vector2I(2, 10))).IsFalse();
    }

    [TestCase]
    public void TestIsPassableBoundaryPositions()
    {
        var mapData = new SimpleMapData
        {
            Grid = new bool[5, 5],
            Size = new Vector2I(5, 5)
        };
        mapData.Grid[0, 0] = true;
        mapData.Grid[0, 4] = true;
        mapData.Grid[4, 0] = true;
        mapData.Grid[4, 4] = true;

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
            Grid = new bool[3, 4],
            Size = new Vector2I(4, 3)
        };
        mapData.Grid[1, 2] = true;

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
            Grid = new bool[7, 12],
            Size = new Vector2I(12, 7)
        };
        mapData.Grid[3, 8] = true;
        mapData.Grid[6, 11] = true;

        AssertBool(mapData.IsPassable(new Vector2I(8, 3))).IsTrue();
        AssertBool(mapData.IsPassable(new Vector2I(11, 6))).IsTrue();
        AssertBool(mapData.IsPassable(new Vector2I(12, 6))).IsFalse();
        AssertBool(mapData.IsPassable(new Vector2I(11, 7))).IsFalse();
    }

    [TestCase]
    public void TestMinimalGridSize()
    {
        var mapData = new SimpleMapData
        {
            Grid = new bool[1, 1],
            Size = new Vector2I(1, 1)
        };
        mapData.Grid[0, 0] = true;

        AssertBool(mapData.IsPassable(new Vector2I(0, 0))).IsTrue();
        AssertBool(mapData.IsPassable(new Vector2I(1, 0))).IsFalse();
        AssertBool(mapData.IsPassable(new Vector2I(0, 1))).IsFalse();
    }

    [TestCase]
    public void TestLargeGridSize()
    {
        var mapData = new SimpleMapData
        {
            Grid = new bool[100, 100],
            Size = new Vector2I(100, 100)
        };
        mapData.Grid[99, 99] = true;

        AssertBool(mapData.IsPassable(new Vector2I(99, 99))).IsTrue();
        AssertBool(mapData.IsPassable(new Vector2I(100, 100))).IsFalse();
    }
}
