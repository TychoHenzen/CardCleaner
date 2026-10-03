using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.SimpleMapGeneratorScenarios;

/// <summary>
///     SimpleMapGenerator map structure, connectivity and determinism scenarios split out of SimpleMapGeneratorTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SimpleMapGeneratorStructureTest : SimpleMapGeneratorTestBase
{
    [TestCase]
    public void TestGenerateMapReturnsValidMapData()
    {
        var size = new Vector2I(10, 10);
        var generator = CreateGenerator(size);

        var mapData = generator.GenerateMap(size);

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
        var generator = CreateGenerator(size);

        var mapData = generator.GenerateMap(size);

        AssertBool(mapData.IsPassable(mapData.PlayerStart)).IsTrue();
    }

    [TestCase]
    public void TestGenerateMapCreatesEnemies()
    {
        // Skip during TSX migration if tile registry lacks required tiles
        if (_tileRegistry.GetAllTiles().Count() < 10)
        {
            GD.Print("[Migration] Skipping enemy test: tile registry has <10 tiles");
            return;
        }

        var size = new Vector2I(10, 10);
        var generator = CreateGenerator(size);

        var mapData = generator.GenerateMap(size);

        AssertThat(mapData.EnemyPositions).IsNotNull();
        AssertThat(mapData.EnemyPositions.Count).IsBetween(1, 3);
    }

    [TestCase]
    public void TestGenerateMapEnemiesAreOnPassableTiles()
    {
        var size = new Vector2I(10, 10);
        var generator = CreateGenerator(size);

        var mapData = generator.GenerateMap(size);

        foreach (var enemyPos in mapData.EnemyPositions)
        {
            AssertBool(mapData.IsPassable(enemyPos)).IsTrue();
        }
    }

    [TestCase]
    public void TestGenerateMapEnsuresConnectivity()
    {
        var size = new Vector2I(10, 10);
        var generator = CreateGenerator(size);

        var mapData = generator.GenerateMap(size);

        var visited = new HashSet<Vector2I>();
        var toVisit = new Queue<Vector2I>();
        toVisit.Enqueue(mapData.PlayerStart);
        visited.Add(mapData.PlayerStart);

        while (toVisit.Count > 0)
        {
            var current = toVisit.Dequeue();
            var neighbors = new[]
            {
                new Vector2I(current.X + 1, current.Y), new Vector2I(current.X - 1, current.Y),
                new Vector2I(current.X, current.Y + 1), new Vector2I(current.X, current.Y - 1)
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
        var generator1 = CreateGenerator(size,
            new CardSignature(new[] { 0.5f, 0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f }));
        var map1 = generator1.GenerateMap(size);

        _rng.Seed = 200;
        var generator2 = CreateGenerator(size,
            new CardSignature(new[] { -0.5f, -0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f }));
        var map2 = generator2.GenerateMap(size);

        // Check for any difference: passable count, player start, or tile content
        var areDifferent = map1.PassableTiles.Count != map2.PassableTiles.Count ||
                           map1.PlayerStart != map2.PlayerStart;

        // Also check actual tile content if basic metrics match
        if (!areDifferent)
        {
            var differentTileCount = 0;
            for (var y = 0; y < size.Y; y++)
            {
                for (var x = 0; x < size.X; x++)
                {
                    if (map1.TileIds[y, x] != map2.TileIds[y, x])
                        differentTileCount++;
                }
            }
            areDifferent = differentTileCount > 0;
        }

        AssertBool(areDifferent).IsTrue();
    }

    [TestCase]
    public void TestGenerateMapIsDeterministicWithSameSeed()
    {
        var size = new Vector2I(8, 8);
        var seed = new CardSignature();

        _rng.Seed = 42;
        var generator1 = CreateGenerator(size, seed);
        var map1 = generator1.GenerateMap(size);

        _rng.Seed = 42;
        var generator2 = CreateGenerator(size, seed);
        var map2 = generator2.GenerateMap(size);

        AssertThat(map1.PlayerStart).IsEqual(map2.PlayerStart);
        AssertThat(map1.PassableTiles.Count).IsEqual(map2.PassableTiles.Count);
    }

    [TestCase]
    public void TestGenerateMapWithMinimalSize()
    {
        var size = new Vector2I(3, 3);
        var generator = CreateGenerator(size);

        var mapData = generator.GenerateMap(size);

        AssertThat(mapData.Size).IsEqual(size);
        AssertThat(mapData.PassableTiles.Count).IsGreater(0);
    }

    [TestCase]
    public void TestGenerateMapWithLargeSize()
    {
        var size = new Vector2I(50, 50);
        var generator = CreateGenerator(size);

        var mapData = generator.GenerateMap(size);

        AssertThat(mapData.Size).IsEqual(size);
        AssertBool(mapData.IsPassable(mapData.PlayerStart)).IsTrue();
    }

    [TestCase]
    public void TestGenerateMapPlayerStartNotOnEnemyPosition()
    {
        var size = new Vector2I(10, 10);
        var generator = CreateGenerator(size);

        var mapData = generator.GenerateMap(size);

        foreach (var enemyPos in mapData.EnemyPositions)
        {
            AssertBool(mapData.PlayerStart != enemyPos).IsTrue();
        }
    }

    [TestCase]
    public void TestGenerateMapPassableTilesMatchTileIds()
    {
        var size = new Vector2I(8, 8);
        var generator = CreateGenerator(size);

        var mapData = generator.GenerateMap(size);

        foreach (var tile in mapData.PassableTiles)
        {
            AssertBool(mapData.IsPassable(tile)).IsTrue();
        }
    }

    [TestCase]
    public void TestGenerateMapTileIdsDimensionsMatchSize()
    {
        var size = new Vector2I(12, 8);
        var generator = CreateGenerator(size);

        var mapData = generator.GenerateMap(size);

        AssertThat(mapData.TileIds.GetLength(0)).IsEqual(size.Y);
        AssertThat(mapData.TileIds.GetLength(1)).IsEqual(size.X);
    }
}
