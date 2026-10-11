using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Tests.Features.Worldgen.Support;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.Constraints.ConnectivityScenarios;

/// <summary>
///     ConnectivityMapGenerationTest scenarios split out of ConnectivityConstraintTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ConnectivityMapGenerationTest : ConnectivityConstraintTestBase
{
    // ========== Test Case 6: MapGeneration_10x10_AlwaysConnected ==========

    [TestCase]
    public static void MapGeneration_10x10_AlwaysConnected()
    {
        // Integration test: Generate 100 10x10 maps and verify all have connected passable regions
        var (generator, biome) = CreateSweepSetup();
        var passable = CreateSweepPassable();

        const int mapCount = 100;
        var sweep = GenerateAndCheckConnectivity(generator, biome, passable, mapCount);

        GD.Print($"Generated {sweep.SuccessCount}/{mapCount} maps successfully");
        GD.Print($"Connected: {sweep.ConnectedCount}/{sweep.SuccessCount}");

        if (sweep.DisconnectedMaps.Count > 0)
        {
            GD.Print($"Disconnected maps: {string.Join(", ", sweep.DisconnectedMaps)}");
        }

        // Every map must generate, and every generated map must have connected passable regions
        AssertInt(sweep.SuccessCount).IsEqual(mapCount);
        AssertInt(sweep.ConnectedCount).IsEqual(sweep.SuccessCount);
    }

    [TestCase]
    public static void MapGeneration_WithConnectivityEnabled_HasConnectedRegions()
    {
        // Simpler test case: generate a few maps and verify connectivity
        var customRules = new WfcAdjacencyRules(new[]
        {
            ("A", "A"), ("A", "B"), ("A", "X"),
            ("B", "B"), ("B", "X"),
            ("X", "X")
        });

        var catalog = CreateCatalog(
            "test",
            ("A", TilePassability.Passable),
            ("B", TilePassability.Passable),
            ("X", TilePassability.Solid));
        var generator = new WfcMapGenerator(customRules, catalog);
        generator.EnableConnectivity = true;

        var passable = new TilePool();
        passable.Add("A", 1.0f);
        passable.Add("B", 1.0f);

        var blocked = new TilePool();
        blocked.Add("X", 1.0f);

        var biome = new BiomeDefinition("test", new CardSignature(), passable, blocked, 0.0f);

        for (var seed = 1; seed <= 10; seed++)
        {
            var result = generator.Generate(biome, new Vector2I(8, 8), (ulong)seed * 100);
            WfcTestFixtures.AssertSucceeded(result, $"Seed {seed}");

            var isConnected = VerifyPassableConnectivity(result.TileIds!, passable);
            AssertBool(isConnected).IsTrue();
        }
    }

    [TestCase]
    public static void DisconnectedFirstAttempt_RetryYieldsConnectedMap()
    {
        // Seed 49387 is sweep index 4. Its first attempt is disconnected: a single attempt must fail,
        // and the default attempt cap must recover a connected map from a derived seed.
        const ulong seed = 49387;
        var passable = CreateSweepPassable();

        var (singleAttempt, biome) = CreateSweepSetup(maxConnectivityAttempts: 1);
        var failed = singleAttempt.Generate(biome, new Vector2I(10, 10), seed);
        AssertBool(failed.Success).IsFalse();
        AssertString(failed.ErrorMessage).Contains("disconnected");

        var (retrying, _) = CreateSweepSetup();
        var result = retrying.Generate(biome, new Vector2I(10, 10), seed);
        WfcTestFixtures.AssertSucceeded(result, "Seed 49387 with the default attempt cap");
        AssertBool(VerifyPassableConnectivity(result.TileIds!, passable)).IsTrue();
    }

    [TestCase]
    public static void ConnectedFirstAttempt_IsReturnedUnchanged()
    {
        // Seed 7 is sweep index 0. Its first attempt is connected, so the retry loop
        // must return that attempt unchanged.
        var (singleAttempt, biome) = CreateSweepSetup(maxConnectivityAttempts: 1);
        var (retrying, _) = CreateSweepSetup();

        var first = singleAttempt.Generate(biome, new Vector2I(10, 10), 7);
        var withRetries = retrying.Generate(biome, new Vector2I(10, 10), 7);

        WfcTestFixtures.AssertSucceeded(first, "Seed 7 with a single attempt");
        WfcTestFixtures.AssertSucceeded(withRetries, "Seed 7 with the default attempt cap");
        AssertSameTileIds(first.TileIds!, withRetries.TileIds!);
    }

    [TestCase]
    public static void ConnectivityRetry_IsDeterministic()
    {
        var (firstGenerator, biome) = CreateSweepSetup();
        var (secondGenerator, _) = CreateSweepSetup();

        var first = firstGenerator.Generate(biome, new Vector2I(10, 10), 49387);
        var second = secondGenerator.Generate(biome, new Vector2I(10, 10), 49387);

        WfcTestFixtures.AssertSucceeded(first, "Seed 49387 on the first generator");
        WfcTestFixtures.AssertSucceeded(second, "Seed 49387 on the second generator");
        AssertSameTileIds(first.TileIds!, second.TileIds!);
    }

    /// <summary>
    ///     A catalog over a fresh registry holding only the given tiles, each allowed in the given biome.
    /// </summary>
    private static TileRegistryWfcCatalog CreateCatalog(
        string biomeId,
        params (string Id, TilePassability Passability)[] tiles) =>
        new(WfcTestFixtures.CreateTestTileRegistry(biomeId, tiles));

    /// <summary>
    ///     The passable pool of the sweep biome. Wall is not in it.
    /// </summary>
    private static TilePool CreateSweepPassable()
    {
        var passable = new TilePool();
        passable.Add("grass", 1.0f);
        passable.Add("water", 1.0f);
        return passable;
    }

    /// <summary>
    ///     Generator and biome for the 10x10 sweep. A null cap keeps the generator's default attempt cap.
    /// </summary>
    private sealed record SweepSetup(WfcMapGenerator Generator, BiomeDefinition Biome);

    private static SweepSetup CreateSweepSetup(
        int? maxConnectivityAttempts = null)
    {
        var blocked = new TilePool();
        blocked.Add("wall", 1.0f);

        var biome = new BiomeDefinition("test_biome", new CardSignature(), CreateSweepPassable(), blocked, 0.0f);

        // Custom adjacency rules that include both passable and impassable tiles
        var catalog = CreateCatalog(
            "test_biome",
            ("grass", TilePassability.Passable),
            ("water", TilePassability.Passable),
            ("wall", TilePassability.Solid));
        var generator = new WfcMapGenerator(new WfcAdjacencyRules(new[]
        {
            ("grass", "grass"),
            ("grass", "water"),
            ("grass", "wall"),
            ("water", "water"),
            ("water", "wall"),
            ("wall", "wall")
        }), catalog);
        generator.EnableConnectivity = true;
        generator.MaxRetries = 5;
        if (maxConnectivityAttempts.HasValue)
        {
            generator.MaxConnectivityAttempts = maxConnectivityAttempts.Value;
        }

        return new SweepSetup(generator, biome);
    }

    private static void AssertSameTileIds(string[,] expected, string[,] actual)
    {
        AssertInt(actual.GetLength(0)).IsEqual(expected.GetLength(0));
        AssertInt(actual.GetLength(1)).IsEqual(expected.GetLength(1));
        for (var y = 0; y < expected.GetLength(0); y++)
        {
            for (var x = 0; x < expected.GetLength(1); x++)
            {
                AssertString(actual[y, x]).IsEqual(expected[y, x]);
            }
        }
    }

    private readonly record struct ConnectivitySweep(int SuccessCount, int ConnectedCount, List<int> DisconnectedMaps);

    private static ConnectivitySweep GenerateAndCheckConnectivity(
        WfcMapGenerator generator,
        BiomeDefinition biome,
        TilePool passable,
        int mapCount)
    {
        var successCount = 0;
        var connectedCount = 0;
        var disconnectedMaps = new List<int>();

        for (var i = 0; i < mapCount; i++)
        {
            var seed = (ulong)(i * 12345 + 7);
            var result = generator.Generate(biome, new Vector2I(10, 10), seed);
            WfcTestFixtures.AssertSucceeded(result, $"Map {i} (seed {seed})");

            successCount++;

            // Check connectivity using flood fill
            if (VerifyPassableConnectivity(result.TileIds!, passable))
            {
                connectedCount++;
                continue;
            }

            disconnectedMaps.Add(i);
            GD.Print($"Map {i} (seed {seed}) has disconnected passable regions!");
        }

        return new ConnectivitySweep(successCount, connectedCount, disconnectedMaps);
    }

    private static bool VerifyPassableConnectivity(string[,] tileIds, TilePool passableTilePool)
    {
        var passableIds = new HashSet<string>(passableTilePool.GetAllTileIds());
        var passablePositions = new HashSet<Vector2I>();
        for (var y = 0; y < tileIds.GetLength(0); y++)
        {
            for (var x = 0; x < tileIds.GetLength(1); x++)
            {
                if (passableIds.Contains(tileIds[y, x]))
                    passablePositions.Add(new Vector2I(x, y));
            }
        }

        if (passablePositions.Count == 0)
            return true; // No passable tiles = trivially connected

        // Flood fill from the first passable position
        var start = passablePositions.GetEnumerator();
        if (!start.MoveNext())
            return true;

        var firstPosition = start.Current;
        var visited = new HashSet<Vector2I>();
        var queue = new Queue<Vector2I>();
        queue.Enqueue(firstPosition);
        visited.Add(firstPosition);

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

                if (passablePositions.Contains(neighbor) && !visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        // All passable positions should be reachable from the first one
        return visited.Count == passablePositions.Count;
    }
}
