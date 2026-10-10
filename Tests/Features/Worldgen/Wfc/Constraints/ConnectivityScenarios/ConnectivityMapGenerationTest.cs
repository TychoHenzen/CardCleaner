using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Services;
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
    public void MapGeneration_10x10_AlwaysConnected()
    {
        // Integration test: Generate 100 10x10 maps and verify all have connected passable regions

        // Biome with a mix of passable and impassable tiles (wall is NOT in the passable pool)
        var passable = new TilePool();
        passable.Add("grass", 1.0f);
        passable.Add("water", 1.0f);

        var blocked = new TilePool();
        blocked.Add("wall", 1.0f);

        var biome = new BiomeDefinition("test_biome", new CardSignature(), passable, blocked, 0.0f);

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
    public void MapGeneration_WithConnectivityEnabled_HasConnectedRegions()
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

    /// <summary>
    ///     A catalog over a fresh registry holding only the given tiles, each allowed in the given biome.
    /// </summary>
    private static TileRegistryWfcCatalog CreateCatalog(
        string biomeId,
        params (string Id, TilePassability Passability)[] tiles)
    {
        var registry = new TileRegistry();
        registry.Clear(); // Clear production tiles loaded by constructor
        foreach (var (id, passability) in tiles)
        {
            registry.RegisterTile(new TileDefinition(
                id,
                id,
                passability,
                Vector2I.Zero,
                new TileDefinitionOptions
                {
                    AllowedBiomes = new HashSet<string> { biomeId }
                }));
        }

        return new TileRegistryWfcCatalog(registry);
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
