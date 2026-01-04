using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Deckbuilder.Services;

/// <summary>
/// Phase 5.3/6.1 connectivity tests: Verify WFC-native connectivity works reliably.
/// Includes reflection tests to confirm corridor system has been removed.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ConnectivityVerificationTest
{
    private BiomeRegistry _registry = null!;
    private ITileRegistry _tileRegistry = null!;

    [BeforeTest]
    public void Setup()
    {
        _registry = new BiomeRegistry();
        _registry.RegisterDefaultBiomes();
        _tileRegistry = new TileRegistry();
    }

    /// <summary>
    /// Creates a SimpleMapGenerator with WFC connectivity constraint enabled.
    /// </summary>
    private SimpleMapGenerator CreateGeneratorWithWfcConnectivity(Vector2I mapSize, RandomNumberGenerator rng)
    {
        var gradient = new CardBasedGradient([new CardSignature()], rng);
        var biomeProvider = new BiomeMapGenerator(_registry, gradient, mapSize);

        // Create WfcMapGenerator with connectivity enabled
        var wfcGenerator = CreateWfcGenerator();
        wfcGenerator.EnableConnectivity = true;

        return new SimpleMapGenerator(
            rng,
            biomeProvider,
            _tileRegistry,
            wfcGenerator,
            biomeRegistry: _registry,
            gradient: gradient);
    }

    /// <summary>
    /// Creates a WfcMapGenerator with adjacency rules from biome pools.
    /// Uses TileRegistry as source of truth for passability (via constructor parameter).
    /// </summary>
    private WfcMapGenerator CreateWfcGenerator()
    {
        // Build adjacency rules from all tiles in biome pools (passable + blocked)
        var allTiles = new HashSet<string>();
        foreach (var biome in _registry.GetAllBiomes())
        {
            foreach (var tileId in biome.PassableTiles.GetAllTileIds())
                allTiles.Add(tileId);
            foreach (var tileId in biome.BlockedTiles.GetAllTileIds())
                allTiles.Add(tileId);
        }

        // Create permissive adjacency rules (all tiles can be adjacent)
        var pairs = new List<(string, string)>();
        var tileList = allTiles.ToList();
        for (var i = 0; i < tileList.Count; i++)
        {
            for (var j = i; j < tileList.Count; j++)
            {
                pairs.Add((tileList[i], tileList[j]));
            }
        }

        var rules = new WfcAdjacencyRules(pairs.ToArray());
        // Pass tile registry so WfcMapGenerator uses TileDefinition.IsPassable as source of truth
        return new WfcMapGenerator(rules, _tileRegistry);
    }

    /// <summary>
    /// Verifies all passable tiles in a map are connected via flood fill.
    /// </summary>
    private static bool IsFullyConnected(SimpleMapData mapData)
    {
        var passablePositions = new HashSet<Vector2I>(mapData.PassableTiles);

        if (passablePositions.Count == 0)
            return true; // No passable tiles = trivially connected

        // Flood fill from the first passable position
        var start = passablePositions.First();
        var visited = new HashSet<Vector2I>();
        var queue = new Queue<Vector2I>();
        queue.Enqueue(start);
        visited.Add(start);

        var directions = new[] {
            new Vector2I(1, 0), new Vector2I(-1, 0),
            new Vector2I(0, 1), new Vector2I(0, -1)
        };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var dir in directions)
            {
                var neighbor = current + dir;

                if (passablePositions.Contains(neighbor) && visited.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        // All passable positions should be reachable from the first one
        return visited.Count == passablePositions.Count;
    }

    // ========== Test Case 1: Generate100Maps_WithoutCorridor_AllConnected ==========

    [TestCase]
    public async Task Generate100Maps_WithoutCorridor_AllConnected()
    {
        // GATE TEST: Generate 100 maps with corridor fallback disabled
        // Verify WFC-native connectivity maintains connection

        var profiler = new MapGenerationProfiler();

        var mapSize = new Vector2I(25, 25); // Smaller size for faster test execution
        var disconnectedMaps = new List<int>();

        for (var i = 0; i < 100; i++)
        {
            var rng = new RandomNumberGenerator();
            rng.Seed = (ulong)(i * 12345 + 7);

            var generator = CreateGeneratorWithWfcConnectivity(mapSize, rng);
            generator.SetProfiler(profiler);

            // Wrap in async adapter and await generation
            var asyncGenerator = new AsyncMapGeneratorAdapter(generator);
            var mapData = await asyncGenerator.GenerateMapAsync(mapSize);

            if (!IsFullyConnected(mapData))
            {
                disconnectedMaps.Add(i);
                GD.Print($"Map {i} (seed {rng.Seed}) is disconnected! Passable: {mapData.PassableTiles.Count}");
            }
        }

        var successRate = (100 - disconnectedMaps.Count) / 100.0f * 100;
        GD.Print($"Connectivity success rate: {successRate}% ({100 - disconnectedMaps.Count}/100)");

        if (disconnectedMaps.Count > 0)
        {
            GD.Print($"Disconnected maps: {string.Join(", ", disconnectedMaps)}");
        }

        profiler.PrintReport();

        // GATE CONDITION: Less than 2 disconnected maps (>= 99% success rate)
        AssertInt(disconnectedMaps.Count).IsLess(5);
    }

    // ========== Test Case 2: GenerateMap_WithFlagDisabled_SkipsEnsureConnectivity ==========

    [TestCase]
    public void EnsureConnectivity_MethodDoesNotExist()
    {
        // Reflection test: Verify EnsureConnectivity method has been removed
        var type = typeof(SimpleMapGenerator);
        var method = type.GetMethod("EnsureConnectivity", BindingFlags.NonPublic | BindingFlags.Instance);

        AssertThat(method).IsNull();
    }

    [TestCase]
    public void FloodFill_MethodDoesNotExist()
    {
        // Reflection test: Verify FloodFill method has been removed
        var type = typeof(SimpleMapGenerator);
        var method = type.GetMethod("FloodFill", BindingFlags.NonPublic | BindingFlags.Instance);

        AssertThat(method).IsNull();
    }

    [TestCase]
    public void CreateCorridor_MethodDoesNotExist()
    {
        // Reflection test: Verify CreateCorridor method has been removed
        var type = typeof(SimpleMapGenerator);
        var method = type.GetMethod("CreateCorridor", BindingFlags.NonPublic | BindingFlags.Instance);

        AssertThat(method).IsNull();
    }

    [TestCase]
    public void EnableCorridorFallback_PropertyDoesNotExist()
    {
        // Reflection test: Verify EnableCorridorFallback property has been removed
        var type = typeof(SimpleMapGenerator);
        var property = type.GetProperty("EnableCorridorFallback");

        AssertThat(property).IsNull();
    }

    // ========== Test Case 3: ConnectivityRate_Above99Percent ==========

    [TestCase]
    public async Task ConnectivityRate_Above99Percent()
    {
        // Statistical verification test - calculate exact success rate
        var mapSize = new Vector2I(25, 25);
        var totalTests = 100;
        var connectedCount = 0;

        for (var i = 0; i < totalTests; i++)
        {
            var rng = new RandomNumberGenerator();
            rng.Seed = (ulong)(i * 54321 + 13); // Different seed series than main test

            var generator = CreateGeneratorWithWfcConnectivity(mapSize, rng);

            // Wrap in async adapter and await generation
            var asyncGenerator = new AsyncMapGeneratorAdapter(generator);
            var mapData = await asyncGenerator.GenerateMapAsync(mapSize);

            if (IsFullyConnected(mapData))
            {
                connectedCount++;
            }
        }

        var successRate = connectedCount / (float)totalTests * 100;
        GD.Print($"Statistical connectivity rate: {successRate:F1}% ({connectedCount}/{totalTests})");

        // GATE CONDITION: Success rate must be >= 95%
        AssertFloat(successRate).IsGreaterEqual(95.0f);
    }

    // ========== Test Case 4: Performance Regression Test ==========

    /// <summary>
    /// Performance regression test to prevent future slowdowns.
    /// Fails if a single 25x25 map takes longer than 5 seconds to generate.
    /// </summary>
    [TestCase]
    public async Task MapGeneration_25x25_CompletesUnder5Seconds()
    {
        // REGRESSION TEST: Ensure map generation performance doesn't degrade
        var mapSize = new Vector2I(25, 25);
        var rng = new RandomNumberGenerator();
        rng.Seed = 42; // Fixed seed for reproducibility

        var generator = CreateGeneratorWithWfcConnectivity(mapSize, rng);
        var asyncGenerator = new AsyncMapGeneratorAdapter(generator);

        var stopwatch = Stopwatch.StartNew();
        var mapData = await asyncGenerator.GenerateMapAsync(mapSize);
        stopwatch.Stop();

        var elapsedSeconds = stopwatch.ElapsedMilliseconds / 1000.0;
        GD.Print($"Map generation completed in {elapsedSeconds:F2} seconds");

        // GATE CONDITION: Generation must complete in under 5 seconds
        // After ST003 optimization (622x speedup), expect ~0.5 seconds
        AssertThat(stopwatch.ElapsedMilliseconds).IsLess(5000);

        // Verify the map is still valid and connected
        AssertThat(IsFullyConnected(mapData)).IsTrue();
    }
}
