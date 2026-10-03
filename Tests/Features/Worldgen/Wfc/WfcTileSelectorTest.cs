using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc;

[TestSuite]
[RequireGodotRuntime]
public class WfcTileSelectorTest
{
    private RandomNumberGenerator _rng = null!;
    private WfcTileSelector _selector = null!;

    [BeforeTest]
    public void Setup()
    {
        _rng = new RandomNumberGenerator();
        _rng.Seed = 12345;
        _selector = new WfcTileSelector();
    }

    [TestCase]
    public void TestEmptyTilesReturnsNull()
    {
        var result = _selector.SelectTile(new List<string>(), null, _rng);

        AssertThat(result).IsNull();
    }

    [TestCase]
    public void TestSingleTileAlwaysReturned()
    {
        var tiles = new List<string> { "grass" };

        for (var i = 0; i < 10; i++)
        {
            var result = _selector.SelectTile(tiles, null, _rng);
            AssertString(result).IsEqual("grass");
        }
    }

    [TestCase]
    public void TestBiomeTilesPreferred()
    {
        var biome = CreatePlainsBiome(new Dictionary<string, float> { { "grass", 1.0f } });

        var counts = SampleSelections(new List<string> { "grass", "water" }, biome, 100);

        // Grass (in biome) should be selected much more often than water (0.1x penalty)
        AssertThat(counts.GetValueOrDefault("grass")).IsGreater(counts.GetValueOrDefault("water") * 5);
    }

    [TestCase]
    public void TestNoBiomeUsesUniformSelection()
    {
        var tiles = new List<string> { "a", "b", "c" };

        var counts = new Dictionary<string, int> { { "a", 0 }, { "b", 0 }, { "c", 0 } };

        for (var i = 0; i < 300; i++)
        {
            var result = _selector.SelectTile(tiles, null, _rng)!;
            counts[result]++;
        }

        // With null biome and default penalty, all tiles should be roughly equal
        // (all get default weight * penalty since none are in biome)
        foreach (var count in counts.Values)
        {
            AssertThat(count).IsGreater(50);
            AssertThat(count).IsLess(200);
        }
    }

    [TestCase]
    public void TestBiomeWeightsRespected()
    {
        var biome = CreatePlainsBiome(new Dictionary<string, float> { { "common", 9.0f }, { "rare", 1.0f } });

        var counts = SampleSelections(new List<string> { "common", "rare" }, biome, 100);

        // Common should appear ~9x more than rare
        AssertThat(counts.GetValueOrDefault("common")).IsGreater(counts.GetValueOrDefault("rare") * 5);
    }

    [TestCase]
    public void TestSelectUniformIgnoresBiome()
    {
        var tiles = new List<string> { "a", "b" };

        var counts = new Dictionary<string, int> { { "a", 0 }, { "b", 0 } };

        for (var i = 0; i < 100; i++)
        {
            var result = _selector.SelectUniform(tiles, _rng)!;
            counts[result]++;
        }

        // Should be roughly 50/50
        AssertThat(counts["a"]).IsGreater(30);
        AssertThat(counts["b"]).IsGreater(30);
    }

    [TestCase]
    public void TestDeterministicWithSameSeed()
    {
        var tiles = new List<string> { "a", "b", "c" };

        _rng.Seed = 42;
        var results1 = new string[10];
        for (var i = 0; i < 10; i++)
            results1[i] = _selector.SelectTile(tiles, null, _rng)!;

        _rng.Seed = 42;
        var results2 = new string[10];
        for (var i = 0; i < 10; i++)
            results2[i] = _selector.SelectTile(tiles, null, _rng)!;

        for (var i = 0; i < 10; i++)
            AssertString(results1[i]).IsEqual(results2[i]);
    }

    [TestCase]
    public void TestCustomPenalty()
    {
        _selector.NonBiomeTilePenalty = 0.5f; // 50% instead of 10%

        var biome = CreatePlainsBiome(new Dictionary<string, float> { { "biome_tile", 1.0f } });

        var counts = SampleSelections(new List<string> { "biome_tile", "other_tile" }, biome, 100);
        var biomeCount = counts.GetValueOrDefault("biome_tile");
        var otherCount = counts.GetValueOrDefault("other_tile");

        // With 0.5 penalty, other should appear more often than with 0.1 penalty
        // biome_tile: 1.0, other_tile: 0.5, so ratio should be ~2:1
        AssertThat(biomeCount).IsGreater(otherCount);
        AssertThat(otherCount).IsGreater(20); // Should have meaningful count
    }

    private static BiomeDefinition CreatePlainsBiome(Dictionary<string, float> passableWeights)
    {
        var passable = new TilePool();
        foreach (var (tileId, weight) in passableWeights)
        {
            passable.Add(tileId, weight);
        }

        return new BiomeDefinition(
            "plains",
            new CardSignature(),
            passable,
            new TilePool(),
            0.15f);
    }

    private Dictionary<string, int> SampleSelections(List<string> tiles, BiomeDefinition biome, int samples)
    {
        var counts = new Dictionary<string, int>();
        for (var i = 0; i < samples; i++)
        {
            var result = _selector.SelectTile(tiles, biome, _rng)!;
            counts[result] = counts.GetValueOrDefault(result) + 1;
        }

        return counts;
    }
}
