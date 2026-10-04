using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.BlackTile;

/// <summary>
///     BlackTileRenderSimulationTest scenarios split out of BlackTileReproductionTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BlackTileRenderSimulationTest : BlackTileReproductionTestBase
{
    // ==================== Black Tile Simulation ====================

    [TestCase]
    public void TestSimulateUniformTerrainRendering()
    {
        // Simulate rendering a 5x5 area of uniform grass terrain
        // This should produce all bitmask 15 (solid fill) tiles

        var blackTileConditions = new List<string>();

        foreach (var tile in _registry.GetAllTiles().Where(t => t.IsCompositable))
        {
            // For uniform terrain, we'd render at bitmask 15 (all corners filled)
            var coords = _resolver.ResolveTransition(tile.Id, tile.Id, 15);
            if (!coords.HasValue)
            {
                // Try fallback
                coords = _resolver.ResolveAnyVariant(tile.Id, 15);
                if (!coords.HasValue)
                {
                    blackTileConditions.Add($"Uniform {tile.Id}: no valid coords for bitmask 15");
                }
            }
        }

        if (blackTileConditions.Count > 0)
        {
            GD.PrintErr($"BLACK TILE CONDITIONS (uniform terrain):\n{string.Join("\n", blackTileConditions)}");
        }

        GD.Print($"Uniform terrain black tile conditions: {blackTileConditions.Count}");
    }

    [TestCase]
    public void TestSimulateBoundaryTerrainRendering()
    {
        // Simulate rendering terrain boundaries
        // Get pairs of compositable tiles

        var compositableTiles = _registry.GetAllTiles()
            .Where(t => t.IsCompositable)
            .ToList();

        var blackTileConditions = new List<string>();

        // Test a few boundary bitmasks
        var boundaryBitmasks = new[] { 1, 2, 3, 4, 5, 8, 9, 10, 12, 14 };

        foreach (var inner in compositableTiles.Take(5)) // Limit for test speed
        {
            foreach (var outer in compositableTiles.Take(5).Where(o => o.Id != inner.Id))
            {
                foreach (var bitmask in boundaryBitmasks)
                {
                    var coords = _resolver.ResolveTransition(inner.Id, outer.Id, bitmask);
                    if (!coords.HasValue)
                    {
                        // Check if ANY variant exists
                        var anyVariant = _resolver.ResolveAnyVariant(inner.Id, bitmask);
                        if (!anyVariant.HasValue)
                        {
                            blackTileConditions.Add($"{inner.Id}|{outer.Id} bitmask={bitmask}: no coords");
                        }
                    }
                }
            }
        }

        if (blackTileConditions.Count > 0)
        {
            GD.Print(
                "Boundary rendering potential black tiles:\n" +
                $"{string.Join("\n", blackTileConditions.Take(20))}...");
        }

        GD.Print($"Boundary terrain black tile conditions: {blackTileConditions.Count}");
    }

    // ==================== Summary Statistics ====================

    [TestCase]
    public void TestBlackTileRiskSummary()
    {
        var totalTiles = _registry.GetAllTiles().Count();
        var compositableTiles = _registry.GetAllTiles().Count(t => t.IsCompositable);
        var totalTransitions = _transitionMap?.Transitions.Count ?? 0;

        var nullVariantCount = _transitionMap?.Transitions.Values
            .Sum(e => e.Variants.Count(v => v == null)) ?? 0;

        var totalVariantSlots = _transitionMap?.Transitions.Values
            .Sum(e => e.Variants.Length) ?? 0;

        var nullPercentage = totalVariantSlots > 0
            ? (nullVariantCount * 100.0 / totalVariantSlots)
            : 0;

        GD.Print("=== BLACK TILE RISK SUMMARY ===");
        GD.Print($"Total tiles in registry: {totalTiles}");
        GD.Print($"Compositable tiles: {compositableTiles}");
        GD.Print($"Total transitions in map: {totalTransitions}");
        GD.Print($"Null variant slots: {nullVariantCount}/{totalVariantSlots} ({nullPercentage:F1}%)");

        // The null percentage gives us an estimate of black tile risk
        // If close to 10%, this may explain the user's observation
        GD.Print($"\n>>> Estimated black tile risk from null variants: {nullPercentage:F1}%");

        AssertThat(totalTransitions).IsGreater(0);
    }
}
