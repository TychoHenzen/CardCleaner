using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Deckbuilder.Services;

[TestSuite]
[RequireGodotRuntime]
public class SimpleMapGeneratorTest
{
    private SimpleMapGenerator _generator = null!;
    private BiomeRegistry _registry = null!;
    private ITileRegistry _tileRegistry = null!;
    private RandomNumberGenerator _rng = null!;

    [BeforeTest]
    public void Setup()
    {
        _rng = new RandomNumberGenerator();
        _rng.Seed = 12345;
        _registry = new BiomeRegistry();
        _registry.RegisterDefaultBiomes();
        _tileRegistry = new TileRegistry();

        var gradient = new CardBasedGradient(new[] { new CardSignature() }, _rng);
        var biomeProvider = new BiomeMapGenerator(_registry, gradient, new Vector2I(10, 10));
        _generator = new SimpleMapGenerator(_rng, biomeProvider, _tileRegistry);
    }

    private SimpleMapGenerator CreateGenerator(Vector2I mapSize, CardSignature? signature = null)
    {
        var seed = signature ?? new CardSignature();
        var gradient = new CardBasedGradient(new[] { seed }, _rng);
        var biomeProvider = new BiomeMapGenerator(_registry, gradient, mapSize);
        return new SimpleMapGenerator(_rng, biomeProvider, _tileRegistry);
    }

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

    [TestCase]
    public void TestGenerateMapUsesBiomeTiles()
    {
        var size = new Vector2I(10, 10);
        var generator = CreateGenerator(size);

        var mapData = generator.GenerateMap(size);

        // Verify all tiles are from registered biome tiles
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            var tileId = mapData.TileIds[y, x];
            // Tile ID should not be null/empty
            AssertBool(!string.IsNullOrEmpty(tileId)).IsTrue();
            // Tile should be registered in the tile registry
            var tile = _tileRegistry.GetTile(tileId);
            AssertThat(tile).IsNotNull();
        }
    }

    [TestCase]
    public void TestDifferentBiomesProduceDifferentTileDistributions()
    {
        var size = new Vector2I(20, 20);

        // Hot signature should produce desert tiles
        _rng.Seed = 42;
        var hotGradient = new ConstantBiomeGradient(new CardSignature(new[] { 0.3f, 0.8f, 0.3f, 0f, 0f, 0f, 0f, 0f }));
        var hotProvider = new BiomeMapGenerator(_registry, hotGradient, size);
        var hotGenerator = new SimpleMapGenerator(_rng, hotProvider, _tileRegistry);
        var hotMap = hotGenerator.GenerateMap(size);

        // Cold signature should produce tundra tiles
        _rng.Seed = 42;
        var coldGradient = new ConstantBiomeGradient(new CardSignature(new[] { 0f, -0.8f, 0.4f, 0f, 0f, 0f, 0f, 0f }));
        var coldProvider = new BiomeMapGenerator(_registry, coldGradient, size);
        var coldGenerator = new SimpleMapGenerator(_rng, coldProvider, _tileRegistry);
        var coldMap = coldGenerator.GenerateMap(size);

        // Desert should have desert tiles, tundra should have tundra tiles
        var hotDesertTiles = CountTilesWithPrefix(hotMap, "desert_");
        var coldTundraTiles = CountTilesWithPrefix(coldMap, "tundra_");

        // Hot biome should produce desert tiles, cold biome should produce tundra tiles
        AssertThat(hotDesertTiles).IsGreater(0);
        AssertThat(coldTundraTiles).IsGreater(0);
    }

    private static int CountTilesWithPrefix(SimpleMapData map, string prefix)
    {
        var count = 0;
        for (var y = 0; y < map.Size.Y; y++)
        for (var x = 0; x < map.Size.X; x++)
            if (map.TileIds[y, x].StartsWith(prefix))
                count++;
        return count;
    }

    private static int CountTile(SimpleMapData map, string tileId)
    {
        var count = 0;
        for (var y = 0; y < map.Size.Y; y++)
        for (var x = 0; x < map.Size.X; x++)
            if (map.TileIds[y, x] == tileId)
                count++;
        return count;
    }

    [TestCase]
    public void TestBiomeCoherence_TilesMatchAssignedBiomes()
    {
        var size = new Vector2I(25, 25);
        _rng.Seed = 12345;

        // Use desert signature to get clear biome assignment
        var desertSignature = new CardSignature(new[] { 0.3f, 0.7f, 0.3f, 0.4f, 0f, -0.2f, -0.2f, 0.3f });
        var gradient = new CardBasedGradient(new[] { desertSignature }, _rng);
        var biomeProvider = new BiomeMapGenerator(_registry, gradient, size);
        var generator = new SimpleMapGenerator(_rng, biomeProvider, _tileRegistry);

        var mapData = generator.GenerateMap(size);

        // Validate that tiles match their assigned biome regions
        var orphanTiles = 0;
        var totalTiles = 0;

        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            var position = new Vector2I(x, y);
            var expectedBiome = biomeProvider.GetBiomeAt(position);
            var placedTileId = mapData.TileIds[y, x];
            totalTiles++;

            // Check if tile belongs to expected biome
            var tileInPassable = expectedBiome.PassableTiles.GetAllTileIds().Contains(placedTileId);
            var tileInBlocked = expectedBiome.BlockedTiles.GetAllTileIds().Contains(placedTileId);

            if (!tileInPassable && !tileInBlocked)
            {
                orphanTiles++;
            }
        }

        var coherencePercentage = (totalTiles - orphanTiles) * 100.0f / totalTiles;

        // After fix: BiomeAffinityConstraint (3.0x) should dominate continuity (2.0x)
        // Expect >90% coherence (allowing some edge case orphans at biome boundaries)
        AssertThat(coherencePercentage).IsGreaterEqual(90.0f);
        AssertThat(orphanTiles).IsLessEqual(totalTiles / 10); // Max 10% orphans
    }
}

internal sealed partial class ConstantBiomeGradient : BaselineGradient
{
    private readonly CardSignature _signature;

    public ConstantBiomeGradient(CardSignature signature)
    {
        _signature = signature;
    }

    public override CardSignature GetSignatureAt(Vector2I position, Vector2I mapSize) => _signature;
}
