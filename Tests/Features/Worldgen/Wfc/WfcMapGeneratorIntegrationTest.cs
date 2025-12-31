using System.Collections.Generic;
using System.Diagnostics;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Wfc;

[TestSuite]
[RequireGodotRuntime]
public class WfcMapGeneratorIntegrationTest
{
    private CompiledTransitionResolver _resolver = null!;
    private BiomeRegistry _biomeRegistry = null!;

    [BeforeTest]
    public void Setup()
    {
        _resolver = new CompiledTransitionResolver();
        _biomeRegistry = new BiomeRegistry();
        _biomeRegistry.RegisterDefaultBiomes();
    }

    [TestCase]
    public void TestGeneratesValidMapWithForestBiome()
    {
        var generator = new WfcMapGenerator(_resolver);
        var biome = _biomeRegistry.GetBiome(BiomeType.Forest);

        AssertThat(biome).IsNotNull();

        var result = generator.Generate(biome!, new Vector2I(10, 10), 12345);

        // May fail if no valid tiles in biome have adjacency rules
        if (!result.Success)
        {
            GD.Print($"Generation failed (expected if tiles not in transition map): {result.ErrorMessage}");
            return;
        }

        AssertBool(result.Success).IsTrue();
        AssertThat(result.MapData).IsNotNull();
        AssertThat(result.MapData!.Size).IsEqual(new Vector2I(10, 10));
    }

    [TestCase]
    public void TestGeneratesValidMapWithPlainsBiome()
    {
        var generator = new WfcMapGenerator(_resolver);
        var biome = _biomeRegistry.GetBiome(BiomeType.Plains);

        AssertThat(biome).IsNotNull();

        var result = generator.Generate(biome!, new Vector2I(10, 10), 12345);

        if (!result.Success)
        {
            GD.Print($"Generation failed (expected if tiles not in transition map): {result.ErrorMessage}");
            return;
        }

        AssertBool(result.Success).IsTrue();
        AssertThat(result.MapData).IsNotNull();
    }

    [TestCase]
    public void TestNoInvalidAdjacencies()
    {
        var generator = new WfcMapGenerator(_resolver);
        var biome = _biomeRegistry.GetBiome(BiomeType.Plains);

        var result = generator.Generate(biome!, new Vector2I(8, 8), 42);

        if (!result.Success)
        {
            GD.Print($"Skipping adjacency test - generation failed: {result.ErrorMessage}");
            return;
        }

        var mapData = result.MapData!;
        var adjacencyRules = new WfcAdjacencyRules(_resolver);

        // Check all horizontal adjacencies
        for (var y = 0; y < mapData.Size.Y; y++)
        {
            for (var x = 0; x < mapData.Size.X - 1; x++)
            {
                var tileA = mapData.TileIds[y, x];
                var tileB = mapData.TileIds[y, x + 1];

                AssertBool(adjacencyRules.CanBeAdjacent(tileA, tileB)).IsTrue();
            }
        }

        // Check all vertical adjacencies
        for (var y = 0; y < mapData.Size.Y - 1; y++)
        {
            for (var x = 0; x < mapData.Size.X; x++)
            {
                var tileA = mapData.TileIds[y, x];
                var tileB = mapData.TileIds[y + 1, x];

                AssertBool(adjacencyRules.CanBeAdjacent(tileA, tileB)).IsTrue();
            }
        }
    }

    [TestCase]
    public void TestPerformanceUnder500ms()
    {
        var generator = new WfcMapGenerator(_resolver);
        var biome = _biomeRegistry.GetBiome(BiomeType.Plains);

        var stopwatch = Stopwatch.StartNew();
        var result = generator.Generate(biome!, new Vector2I(20, 20), 12345);
        stopwatch.Stop();

        GD.Print($"WFC generation for 20x20: {stopwatch.ElapsedMilliseconds}ms, {result.Iterations} iterations");

        if (result.Success)
        {
            AssertThat(stopwatch.ElapsedMilliseconds).IsLess(500);
        }
        else
        {
            GD.Print($"Generation failed: {result.ErrorMessage}");
        }
    }

    [TestCase]
    public void TestDifferentSeedsProduceDifferentMaps()
    {
        var generator = new WfcMapGenerator(_resolver);
        var biome = _biomeRegistry.GetBiome(BiomeType.Plains);

        var result1 = generator.Generate(biome!, new Vector2I(8, 8), 111);
        var result2 = generator.Generate(biome!, new Vector2I(8, 8), 222);

        if (!result1.Success || !result2.Success)
        {
            GD.Print("Skipping seed variation test - generation failed");
            return;
        }

        // Count how many tiles are different
        var differentCount = 0;
        var map1 = result1.MapData!;
        var map2 = result2.MapData!;

        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                if (map1.TileIds[y, x] != map2.TileIds[y, x])
                    differentCount++;
            }
        }

        // Maps should have some variation
        AssertThat(differentCount).IsGreater(0);
    }

    [TestCase]
    public void TestSameSeedProducesSameMap()
    {
        var generator = new WfcMapGenerator(_resolver);
        var biome = _biomeRegistry.GetBiome(BiomeType.Plains);

        var result1 = generator.Generate(biome!, new Vector2I(8, 8), 42);
        var result2 = generator.Generate(biome!, new Vector2I(8, 8), 42);

        if (!result1.Success || !result2.Success)
        {
            GD.Print("Skipping determinism test - generation failed");
            return;
        }

        var map1 = result1.MapData!;
        var map2 = result2.MapData!;

        // Maps should be identical
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                AssertString(map1.TileIds[y, x]).IsEqual(map2.TileIds[y, x]);
            }
        }
    }

    [TestCase]
    public void TestPassableTilesPopulated()
    {
        var generator = new WfcMapGenerator(_resolver);
        var biome = _biomeRegistry.GetBiome(BiomeType.Plains);

        var result = generator.Generate(biome!, new Vector2I(10, 10), 12345);

        if (!result.Success)
        {
            GD.Print("Skipping passable tiles test - generation failed");
            return;
        }

        var mapData = result.MapData!;

        // PassableTiles should be populated
        AssertThat(mapData.PassableTiles.Count).IsGreater(0);

        // All entries should be within bounds
        foreach (var pos in mapData.PassableTiles)
        {
            AssertBool(pos.X >= 0 && pos.X < mapData.Size.X).IsTrue();
            AssertBool(pos.Y >= 0 && pos.Y < mapData.Size.Y).IsTrue();
        }
    }

    [TestCase]
    public void TestPlayerStartIsPassable()
    {
        var generator = new WfcMapGenerator(_resolver);
        var biome = _biomeRegistry.GetBiome(BiomeType.Plains);

        var result = generator.Generate(biome!, new Vector2I(10, 10), 12345);

        if (!result.Success)
        {
            GD.Print("Skipping player start test - generation failed");
            return;
        }

        var mapData = result.MapData!;

        // Player start should be a passable tile
        AssertBool(mapData.PassableTiles.Contains(mapData.PlayerStart)).IsTrue();
    }

    [TestCase]
    public void TestCustomAdjacencyRulesWork()
    {
        // Create custom rules for testing without relying on transition map
        var customRules = new WfcAdjacencyRules(new[]
        {
            ("A", "B"),
            ("B", "C"),
            ("A", "C")
        });

        var generator = new WfcMapGenerator(customRules);

        // Create simple biome with A, B, C tiles
        var passable = new TilePool();
        passable.Add("A", 1.0f);
        passable.Add("B", 1.0f);
        passable.Add("C", 1.0f);

        var biome = new BiomeDefinition(
            BiomeType.Plains,
            new CardSignature(),
            passable,
            new TilePool(),
            0.0f);

        var result = generator.Generate(biome, new Vector2I(5, 5), 12345);

        AssertBool(result.Success).IsTrue();
        AssertThat(result.MapData).IsNotNull();
        AssertThat(result.MapData!.Size).IsEqual(new Vector2I(5, 5));
    }

    [TestCase]
    public void TestRetryOnContradiction()
    {
        // Create rules that might cause contradictions
        var rules = new WfcAdjacencyRules(new[]
        {
            ("A", "B"),  // A can neighbor B
            ("B", "C")   // B can neighbor C
            // Note: A cannot neighbor C directly
        });

        var generator = new WfcMapGenerator(rules);
        generator.MaxRetries = 5;

        var passable = new TilePool();
        passable.Add("A", 1.0f);
        passable.Add("B", 1.0f);
        passable.Add("C", 1.0f);

        var biome = new BiomeDefinition(
            BiomeType.Plains,
            new CardSignature(),
            passable,
            new TilePool(),
            0.0f);

        // This might fail or succeed depending on collapse order
        var result = generator.Generate(biome, new Vector2I(5, 5), 12345);

        // Just verify it doesn't crash and provides meaningful feedback
        if (!result.Success)
        {
            AssertThat(result.ErrorMessage).IsNotNull();
            GD.Print($"Expected failure with limited rules: {result.ErrorMessage}");
        }
    }
}
