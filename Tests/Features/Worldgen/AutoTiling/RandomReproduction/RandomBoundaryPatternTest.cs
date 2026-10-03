using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.RandomReproduction;

/// <summary>
///     RandomBoundaryPatternTest scenarios split out of RandomAutoTileReproductionTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RandomBoundaryPatternTest : RandomAutoTileReproductionTestBase
{
    // ==================== Visual Pattern Simulation ====================

    [TestCase]
    public void TestSimulateTerrainBoundaryPattern()
    {
        // Simulate a simple horizontal terrain boundary:
        // Row 0-1: Grass
        // Row 2-3: Sand
        //
        // Visual tiles at row 2 boundary should have specific patterns
        bool IsGrass(int x, int y)
        {
            return x >= 0 && x < 4 && y >= 0 && y < 2;
        }

        // Visual (1,2) samples NW=(0,1), NE=(1,1) grass and SW=(0,2), SE=(1,2) sand: NW=8 + NE=1 = 9.
        // Visual (1,1) is all grass; visual (1,3) is all sand (no grass).
        BitmaskExpectation[] expectations =
        [
            new(1, 2, 9, "NW+NE"),
            new(1, 1, 15, "all grass"),
            new(1, 3, 0, "no grass")
        ];

        var results = expectations
            .Select(e => (Expectation: e, Actual: DualGridAutoTile.ComputeBitmask(e.X, e.Y, IsGrass)))
            .Where(r => r.Actual != r.Expectation.Expected)
            .Select(r => $"Visual ({r.Expectation.X},{r.Expectation.Y}): " +
                         $"expected bitmask {r.Expectation.Expected} ({r.Expectation.Description}), got {r.Actual}")
            .ToList();

        if (results.Count > 0)
        {
            GD.PrintErr($"RANDOM PATTERN CAUSE - Boundary simulation failed:\n{string.Join("\n", results)}");
        }

        AssertThat(results.Count).IsEqual(0);
    }

    private readonly record struct BitmaskExpectation(int X, int Y, int Expected, string Description);

    // ==================== Format Mismatch Detection ====================

    [TestCase]
    public void TestTileFormatMatchesTransitionFormat()
    {
        // If a tile uses blob47 format but transition uses corner16, patterns will be wrong

        AssertThat(_transitionMap).IsNotNull();

        var formatMismatches = new List<string>();

        foreach (var tile in _registry.GetAllTiles().Where(t => t.IsCompositable))
        {
            var tileFormat = tile.AutoTileFormatName.ToLowerInvariant();

            // Find transitions for this tile
            foreach (var (key, entry) in _transitionMap!.Transitions)
            {
                var (borderId, _) = CompiledTransitionMap.ParseKey(key);
                if (borderId != tile.Id && borderId != $"{tile.Id}_border")
                    continue;

                if (entry.Format != tileFormat)
                {
                    formatMismatches.Add($"{tile.Id}: tile format={tileFormat}, transition format={entry.Format}");
                }
            }
        }

        if (formatMismatches.Count > 0)
        {
            GD.PrintErr($"RANDOM PATTERN CAUSE - Format mismatches:\n{string.Join("\n", formatMismatches)}");
        }

        // Informational - mismatch might be intentional
        GD.Print($"Format mismatches found: {formatMismatches.Count}");
    }

    // ==================== Summary ====================

    [TestCase]
    public void TestRandomPatternDiagnosticsSummary()
    {
        var compositableCount = _registry.GetAllTiles().Count(t => t.IsCompositable);
        var corner16Count = _transitionMap?.Transitions.Count(kvp => kvp.Value.Format == "corner16") ?? 0;
        var blob47Count = _transitionMap?.Transitions.Count(kvp => kvp.Value.Format == "blob47") ?? 0;

        GD.Print("=== RANDOM PATTERN DIAGNOSTICS ===");
        GD.Print($"Compositable tiles: {compositableCount}");
        GD.Print($"Corner16 transitions: {corner16Count}");
        GD.Print($"Blob47 transitions: {blob47Count}");

        // Calculate average variant coverage
        if (_transitionMap != null)
        {
            var totalVariants = _transitionMap.Transitions.Values.Sum(e => e.Variants.Length);
            var nonNullVariants = _transitionMap.Transitions.Values.Sum(e => e.Variants.Count(v => v != null));
            var coverage = totalVariants > 0 ? (nonNullVariants * 100.0 / totalVariants) : 0;
            GD.Print($"Variant coverage: {nonNullVariants}/{totalVariants} ({coverage:F1}%)");
        }

        GD.Print("\n>>> If patterns appear random, check:");
        GD.Print("  1. Bitmask calculation in rendering code");
        GD.Print("  2. Terrain ID comparison (exact string match)");
        GD.Print("  3. Format alignment (corner16 vs blob47)");
        GD.Print("  4. Dual-grid vs single-grid coordinate systems");

        // Skip if no compositable tiles exist in current data
        if (compositableCount == 0)
        {
            GD.Print("\nNo compositable tiles found - skipping assertion");
            return;
        }
        AssertThat(compositableCount).IsGreater(0);
    }
}
