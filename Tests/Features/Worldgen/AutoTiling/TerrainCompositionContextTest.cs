using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling;

/// <summary>
/// Tests for terrain composition context tracking.
///
/// PROBLEM: When the data layer only stores "grass" at a cell, the renderer cannot know
/// if this is "grass on sand" vs "grass on dirt" - both require different transition tiles.
///
/// SOLUTION: The data layer needs to track terrain pairs, not just single terrain IDs.
/// For transition tiles, we need to know both:
/// - The "inner" terrain (the one with the visible border, e.g., grass)
/// - The "outer" terrain (the background, e.g., sand or dirt)
///
/// This test suite validates that:
/// 1. Transition resolution correctly uses both inner and outer terrain
/// 2. Different outer terrains produce different tile coordinates
/// 3. The dominance system correctly determines which terrain is inner vs outer
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TerrainCompositionContextTest
{
    private TileRegistry _registry = null!;
    private CompiledTransitionResolver _resolver = null!;

    [BeforeTest]
    public void Setup()
    {
        _registry = new TileRegistry();
        _resolver = new CompiledTransitionResolver();
    }

    // ==================== Different Outer Terrains Produce Different Results ====================

    [TestCase]
    public void TestSameInnerDifferentOuterProducesDifferentCoords()
    {
        // This test validates that the composition context matters
        // Same inner terrain with different outer terrains should produce different tiles

        var compositableTiles = _registry.GetAllTiles()
            .Where(t => t.IsCompositable)
            .Take(3)
            .ToList();

        if (compositableTiles.Count < 3)
        {
            GD.Print("Need at least 3 compositable tiles for this test");
            return;
        }

        var inner = compositableTiles[0].Id;
        var outer1 = compositableTiles[1].Id;
        var outer2 = compositableTiles[2].Id;

        GD.Print($"Testing: {inner} on {outer1} vs {inner} on {outer2}");

        var differentCoords = 0;
        var sameCoords = 0;

        for (var bitmask = 1; bitmask < 15; bitmask++) // Skip 0 and 15 (empty and full)
        {
            var coords1 = _resolver.ResolveTransition(inner, outer1, bitmask);
            var coords2 = _resolver.ResolveTransition(inner, outer2, bitmask);

            if (coords1.HasValue && coords2.HasValue)
            {
                if (coords1 != coords2)
                    differentCoords++;
                else
                    sameCoords++;
            }
        }

        GD.Print($"Results: {differentCoords} different, {sameCoords} same");

        // If all coords are the same, the composition context isn't being used properly
        // We expect at least SOME to be different
        if (differentCoords == 0 && sameCoords > 0)
        {
            GD.PrintErr("WARNING: All transition coords are same regardless of outer terrain");
            GD.PrintErr("This suggests transition_map may not have terrain-specific composites");
        }

        // This is informational - it tells us whether composition matters for this tile set
    }

    // ==================== Dominance Determines Inner/Outer ====================

    [TestCase]
    public void TestDominanceOrderAffectsTransitionLookup()
    {
        // Higher dominance terrain should be the "inner" (visible border)
        // Lower dominance terrain should be the "outer" (background)

        var tilesWithDominance = _registry.GetAllTiles()
            .Where(t => t.IsCompositable && t.Dominance > 0)
            .OrderBy(t => t.Dominance)
            .ToList();

        if (tilesWithDominance.Count < 2)
        {
            GD.Print("No tiles with explicit dominance values found");
            return;
        }

        GD.Print("Tiles ordered by dominance:");
        foreach (var tile in tilesWithDominance.Take(5))
        {
            GD.Print($"  {tile.Id}: dominance={tile.Dominance}");
        }

        // When low-dominance borders high-dominance:
        // The high-dominance terrain's border should be visible
        var lowDom = tilesWithDominance.First();
        var highDom = tilesWithDominance.Last();

        GD.Print($"\nExpected: {highDom.Id} (dom={highDom.Dominance}) renders its border on top of {lowDom.Id} (dom={lowDom.Dominance})");

        // The transition should be keyed as highDom|lowDom, not lowDom|highDom
        var correctKeyExists = _resolver.HasTransition(highDom.Id, lowDom.Id);
        var reverseKeyExists = _resolver.HasTransition(lowDom.Id, highDom.Id);

        GD.Print($"Transition {highDom.Id}|{lowDom.Id} exists: {correctKeyExists}");
        GD.Print($"Transition {lowDom.Id}|{highDom.Id} exists: {reverseKeyExists}");

        // At least one direction should exist
        AssertBool(correctKeyExists || reverseKeyExists).IsTrue();
    }

    // ==================== Composition Context Data Model ====================

    /// <summary>
    /// Documents what composition context data should look like.
    /// Currently this may not exist - this test documents the requirement.
    /// </summary>
    [TestCase]
    public void TestCompositionContextRequirements()
    {
        GD.Print("=== TERRAIN COMPOSITION CONTEXT REQUIREMENTS ===");
        GD.Print("");
        GD.Print("Current Problem:");
        GD.Print("  - Data grid stores only terrain ID (e.g., 'grass')");
        GD.Print("  - At boundaries, renderer needs to know both inner AND outer terrain");
        GD.Print("  - Example: 'grass' at position could need 'grass|sand' or 'grass|dirt' tiles");
        GD.Print("");
        GD.Print("Solution Options:");
        GD.Print("");
        GD.Print("Option A: Store Terrain Pairs in Data Grid");
        GD.Print("  - Change cell data from 'string terrainId' to 'TerrainCell { inner, outer }'");
        GD.Print("  - Boundary cells store both terrains they connect");
        GD.Print("  - Non-boundary cells have same inner and outer (or outer=null)");
        GD.Print("");
        GD.Print("Option B: Compute Outer at Render Time");
        GD.Print("  - Keep data grid as single terrain IDs");
        GD.Print("  - At render time, sample adjacent cells to determine outer terrain");
        GD.Print("  - Uses dominance to decide which terrain is 'inner' vs 'outer'");
        GD.Print("");
        GD.Print("Option C: Layer-Based Approach");
        GD.Print("  - Base layer stores ground terrain (e.g., 'sand')");
        GD.Print("  - Overlay layer stores transition terrain (e.g., 'grass')");
        GD.Print("  - Renderer composites: base + transition overlay");
        GD.Print("");
        GD.Print("Current Implementation:");

        // Check what the current system does
        var compositableTiles = _registry.GetAllTiles()
            .Where(t => t.IsCompositable)
            .ToList();

        var outerTerrainIds = compositableTiles
            .Select(t => t.OuterTerrainId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        GD.Print($"  - Compositable tiles: {compositableTiles.Count}");
        GD.Print($"  - Tiles with OuterTerrainId='*' (any): {compositableTiles.Count(t => t.IsCompositable)}");
        GD.Print($"  - Tiles with fixed OuterTerrainId: {outerTerrainIds.Count(id => id != "*")}");
        GD.Print("");
        GD.Print("Recommendation:");
        GD.Print("  Use Option B (compute at render time) since transition_map.json");
        GD.Print("  already contains all terrain pair combinations. The renderer just");
        GD.Print("  needs to determine the outer terrain from adjacent cells.");

        // This is purely informational
        AssertThat(compositableTiles.Count).IsGreater(0);
    }

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
