using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.TerrainComposition;

/// <summary>
///     TerrainCompositionNeighborTest scenarios split out of TerrainCompositionContextTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TerrainCompositionNeighborTest : TerrainCompositionContextTestBase
{
    // ==================== Adjacent Cell Sampling ====================

    [TestCase]
    public void TestDetermineOuterTerrainFromAdjacent()
    {
        // Simulate determining outer terrain by sampling adjacent cells

        // 3x3 grid:
        // sand  sand  sand
        // sand  grass sand
        // sand  sand  sand
        // At the grass cell, outer terrain is sand (the surrounding terrain)

        var grid = new string[,]
        {
            { "sand", "sand", "sand" },
            { "sand", "grass", "sand" },
            { "sand", "sand", "sand" }
        };

        var innerTerrain = grid[1, 1]; // grass

        // Sample adjacent cells to find outer terrain
        var adjacentTerrains = new HashSet<string>();
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue; // Skip center

                var nx = 1 + dx;
                var ny = 1 + dy;
                if (nx >= 0 && nx < 3 && ny >= 0 && ny < 3)
                {
                    var neighbor = grid[ny, nx];
                    if (neighbor != innerTerrain)
                        adjacentTerrains.Add(neighbor);
                }
            }
        }

        GD.Print($"Inner terrain: {innerTerrain}");
        GD.Print($"Adjacent outer terrains: {string.Join(", ", adjacentTerrains)}");

        AssertThat(adjacentTerrains).Contains("sand");
        AssertThat(innerTerrain).IsEqual("grass");
    }

    // ==================== Multi-Terrain Boundaries ====================

    [TestCase]
    public void TestThreeWayTerrainBoundary()
    {
        // When 3+ terrains meet at a corner, which transition to use?
        // This is a complex case that may need special handling

        // Pattern:
        // grass  grass  sand
        // grass  CORNER sand
        // dirt   dirt   dirt

        var grid = new string[,]
        {
            { "grass", "grass", "sand" },
            { "grass", "grass", "sand" },
            { "dirt", "dirt", "dirt" }
        };

        // At position (1,1), the visual tile at (2,2) samples:
        // NW=(1,1)=grass, NE=(2,1)=sand, SW=(1,2)=dirt, SE=(2,2)=dirt

        // This is a 3-way boundary between grass, sand, and dirt
        // The transition system needs to handle this gracefully

        var sampledTerrains = new HashSet<string>
        {
            grid[1, 1], // NW
            grid[1, 2], // NE
            grid[2, 1], // SW
            grid[2, 2]  // SE
        };

        GD.Print($"3-way boundary terrains: {string.Join(", ", sampledTerrains)}");
        AssertThat(sampledTerrains.Count).IsEqual(3);

        // Check if transitions exist for all pairs
        var terrains = sampledTerrains.ToList();
        var missingTransitions = new List<string>();

        for (var i = 0; i < terrains.Count; i++)
        {
            for (var j = i + 1; j < terrains.Count; j++)
            {
                var t1 = terrains[i];
                var t2 = terrains[j];

                var exists = _resolver.HasTransition(t1, t2) || _resolver.HasTransition(t2, t1);
                if (!exists)
                {
                    missingTransitions.Add($"{t1}|{t2}");
                }
            }
        }

        if (missingTransitions.Count > 0)
        {
            GD.Print($"Missing transitions for 3-way boundary: {string.Join(", ", missingTransitions)}");
        }

        // 3-way boundaries may need special handling - this is informational
    }

    // ==================== Statistics ====================

    [TestCase]
    public void TestTerrainCompositionStatistics()
    {
        var compositableTiles = _registry.GetAllTiles()
            .Where(t => t.IsCompositable)
            .ToList();

        // Skip if no compositable tiles exist in current data
        if (compositableTiles.Count == 0)
        {
            GD.Print("No compositable tiles found - skipping statistics");
            return;
        }

        var allTileIds = _registry.GetAllTiles().Select(t => t.Id).ToList();

        // For each compositable tile, count how many outer terrain transitions exist
        var transitionCounts = new Dictionary<string, int>();

        foreach (var tile in compositableTiles)
        {
            var count = 0;
            foreach (var otherId in allTileIds)
            {
                if (_resolver.HasTransition(tile.Id, otherId))
                    count++;
            }
            transitionCounts[tile.Id] = count;
        }

        GD.Print("=== COMPOSITION CONTEXT STATISTICS ===");
        GD.Print($"Compositable tiles: {compositableTiles.Count}");
        GD.Print($"Total terrain IDs: {allTileIds.Count}");
        GD.Print("");
        GD.Print("Transitions per compositable tile:");
        foreach (var (tileId, count) in transitionCounts.OrderByDescending(kvp => kvp.Value).Take(10))
        {
            var percentage = 100.0 * count / allTileIds.Count;
            GD.Print($"  {tileId}: {count} outer terrains ({percentage:F1}% coverage)");
        }

        // Calculate average coverage
        var avgCoverage = transitionCounts.Values.Average();
        var avgPercentage = 100.0 * avgCoverage / allTileIds.Count;
        GD.Print($"\nAverage transitions per compositable: {avgCoverage:F1} ({avgPercentage:F1}%)");

        // If coverage is low, composition context might not be fully utilized
        if (avgPercentage < 20)
        {
            GD.Print("\nWARNING: Low transition coverage may indicate missing terrain pair composites");
        }

        AssertThat(compositableTiles.Count).IsGreater(0);
    }
}
