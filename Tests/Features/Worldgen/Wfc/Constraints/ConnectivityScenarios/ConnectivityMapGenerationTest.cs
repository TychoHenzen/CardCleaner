using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
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
        var generator = new WfcMapGenerator(new WfcAdjacencyRules(new[]
        {
            ("grass", "grass"),
            ("grass", "water"),
            ("grass", "wall"),
            ("water", "water"),
            ("water", "wall"),
            ("wall", "wall")
        }));
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

        // All successfully generated maps should have connected passable regions
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

        var generator = new WfcMapGenerator(customRules);
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

            if (!result.Success)
            {
                GD.Print($"Seed {seed}: Generation failed - {result.ErrorMessage}");
                continue;
            }

            var isConnected = VerifyPassableConnectivity(result.MapData!, passable);
            AssertBool(isConnected).IsTrue();
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

            if (!result.Success)
            {
                GD.Print($"Map {i} generation failed: {result.ErrorMessage}");
                continue;
            }

            successCount++;

            // Check connectivity using flood fill
            if (VerifyPassableConnectivity(result.MapData!, passable))
            {
                connectedCount++;
                continue;
            }

            disconnectedMaps.Add(i);
            GD.Print($"Map {i} (seed {seed}) has disconnected passable regions!");
        }

        return new ConnectivitySweep(successCount, connectedCount, disconnectedMaps);
    }

    private static bool VerifyPassableConnectivity(SimpleMapData mapData, TilePool passableTilePool)
    {
        var passablePositions = new HashSet<Vector2I>(mapData.PassableTiles);

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
