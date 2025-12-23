using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Deckbuilder.Services;

[TestSuite]
[RequireGodotRuntime]
public class SimpleMapGeneratorTest
{
    private RandomNumberGenerator _rng = null!;
    private SimpleMapGenerator _generator = null!;

    [BeforeTest]
    public void Setup()
    {
        _rng = new RandomNumberGenerator();
        _rng.Seed = 12345;
        _generator = new SimpleMapGenerator(_rng);
    }

    [TestCase]
    public void TestGenerateMapReturnsValidMapData()
    {
        var size = new Vector2I(10, 10);
        var seed = new CardSignature();

        var mapData = _generator.GenerateMap(size, seed);

        AssertThat(mapData).IsNotNull();
        AssertThat(mapData.TileIds).IsNotNull();
        AssertThat(mapData.Size).IsEqual(size);
        AssertThat(mapData.PassableTiles).IsNotNull();
        AssertThat(mapData.PassableTiles.Count).IsGreater(0);
    }

    [TestCase]
    public void TestGenerateMapSetsPlayerStart()
    {
        var size = new Vector2I(10, 10);
        var seed = new CardSignature();

        var mapData = _generator.GenerateMap(size, seed);

        AssertBool(mapData.IsPassable(mapData.PlayerStart)).IsTrue();
    }

    [TestCase]
    public void TestGenerateMapCreatesEnemies()
    {
        var size = new Vector2I(10, 10);
        var seed = new CardSignature();

        var mapData = _generator.GenerateMap(size, seed);

        AssertThat(mapData.EnemyPositions).IsNotNull();
        AssertThat(mapData.EnemyPositions.Count).IsBetween(1, 3);
    }

    [TestCase]
    public void TestGenerateMapEnemiesAreOnPassableTiles()
    {
        var size = new Vector2I(10, 10);
        var seed = new CardSignature();

        var mapData = _generator.GenerateMap(size, seed);

        foreach (var enemyPos in mapData.EnemyPositions)
        {
            AssertBool(mapData.IsPassable(enemyPos)).IsTrue();
        }
    }

    [TestCase]
    public void TestGenerateMapEnsuresConnectivity()
    {
        var size = new Vector2I(10, 10);
        var seed = new CardSignature();

        var mapData = _generator.GenerateMap(size, seed, 0.5f);

        var visited = new HashSet<Vector2I>();
        var toVisit = new Queue<Vector2I>();
        toVisit.Enqueue(mapData.PlayerStart);
        visited.Add(mapData.PlayerStart);

        while (toVisit.Count > 0)
        {
            var current = toVisit.Dequeue();
            var neighbors = new[]
            {
                new Vector2I(current.X + 1, current.Y),
                new Vector2I(current.X - 1, current.Y),
                new Vector2I(current.X, current.Y + 1),
                new Vector2I(current.X, current.Y - 1)
            };

            foreach (var neighbor in neighbors)
            {
                if (mapData.IsPassable(neighbor) && !visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    toVisit.Enqueue(neighbor);
                }
            }
        }

        AssertThat(visited.Count).IsEqual(mapData.PassableTiles.Count);
    }

    [TestCase]
    public void TestGenerateMapWithDifferentSeeds()
    {
        var size = new Vector2I(8, 8);

        _rng.Seed = 100;
        var generator1 = new SimpleMapGenerator(_rng);
        var seed1 = new CardSignature(new[] { 0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        var map1 = generator1.GenerateMap(size, seed1);

        _rng.Seed = 200;
        var generator2 = new SimpleMapGenerator(_rng);
        var seed2 = new CardSignature(new[] { -0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        var map2 = generator2.GenerateMap(size, seed2);

        var areDifferent = map1.PassableTiles.Count != map2.PassableTiles.Count ||
                           map1.PlayerStart != map2.PlayerStart;
        AssertBool(areDifferent).IsTrue();
    }

    [TestCase]
    public void TestGenerateMapIsDeterministicWithSameSeed()
    {
        var size = new Vector2I(8, 8);
        var seed = new CardSignature();

        _rng.Seed = 42;
        var generator1 = new SimpleMapGenerator(_rng);
        var map1 = generator1.GenerateMap(size, seed);

        _rng.Seed = 42;
        var generator2 = new SimpleMapGenerator(_rng);
        var map2 = generator2.GenerateMap(size, seed);

        AssertThat(map1.PlayerStart).IsEqual(map2.PlayerStart);
        AssertThat(map1.PassableTiles.Count).IsEqual(map2.PassableTiles.Count);
    }

    [TestCase]
    public void TestGenerateMapWithLowBlockedPercentage()
    {
        var size = new Vector2I(10, 10);
        var seed = new CardSignature();

        var mapData = _generator.GenerateMap(size, seed, 0.1f);

        var expectedMinPassable = (int)(size.X * size.Y * 0.7f);
        AssertThat(mapData.PassableTiles.Count).IsGreaterEqual(expectedMinPassable);
    }

    [TestCase]
    public void TestGenerateMapWithHighBlockedPercentage()
    {
        var size = new Vector2I(10, 10);
        var seed = new CardSignature();

        var mapData = _generator.GenerateMap(size, seed, 0.8f);

        AssertThat(mapData.PassableTiles.Count).IsGreater(0);
    }

    [TestCase]
    public void TestGenerateMapWithMinimalSize()
    {
        var size = new Vector2I(3, 3);
        var seed = new CardSignature();

        var mapData = _generator.GenerateMap(size, seed);

        AssertThat(mapData.Size).IsEqual(size);
        AssertThat(mapData.PassableTiles.Count).IsGreater(0);
    }

    [TestCase]
    public void TestGenerateMapWithLargeSize()
    {
        var size = new Vector2I(50, 50);
        var seed = new CardSignature();

        var mapData = _generator.GenerateMap(size, seed);

        AssertThat(mapData.Size).IsEqual(size);
        AssertBool(mapData.IsPassable(mapData.PlayerStart)).IsTrue();
    }

    [TestCase]
    public void TestGenerateMapPlayerStartNotOnEnemyPosition()
    {
        var size = new Vector2I(10, 10);
        var seed = new CardSignature();

        var mapData = _generator.GenerateMap(size, seed);

        foreach (var enemyPos in mapData.EnemyPositions)
        {
            AssertBool(mapData.PlayerStart != enemyPos).IsTrue();
        }
    }

    [TestCase]
    public void TestGenerateMapHandlesFullyBlockedInitially()
    {
        _rng.Seed = 99999;
        var generator = new SimpleMapGenerator(_rng);
        var size = new Vector2I(5, 5);
        var seed = new CardSignature();

        var mapData = generator.GenerateMap(size, seed, 1.0f);

        AssertThat(mapData.PassableTiles.Count).IsGreater(0);
    }

    [TestCase]
    public void TestGenerateMapPassableTilesMatchTileIds()
    {
        var size = new Vector2I(8, 8);
        var seed = new CardSignature();

        var mapData = _generator.GenerateMap(size, seed);

        foreach (var tile in mapData.PassableTiles)
        {
            AssertBool(mapData.IsPassable(tile)).IsTrue();
        }
    }

    [TestCase]
    public void TestGenerateMapTileIdsDimensionsMatchSize()
    {
        var size = new Vector2I(12, 8);
        var seed = new CardSignature();

        var mapData = _generator.GenerateMap(size, seed);

        AssertThat(mapData.TileIds.GetLength(0)).IsEqual(size.Y);
        AssertThat(mapData.TileIds.GetLength(1)).IsEqual(size.X);
    }

    [TestCase]
    public void TestGenerateMapUsesCorrectTileIds()
    {
        var size = new Vector2I(5, 5);
        var seed = new CardSignature();

        var mapData = _generator.GenerateMap(size, seed);

        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            var tileId = mapData.TileIds[y, x];
            AssertBool(tileId == SimpleMapGenerator.FloorTileId || tileId == SimpleMapGenerator.WallTileId).IsTrue();
        }
    }
}
