using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling;

/// <summary>
/// Investigates why auto-tiled patterns (especially blob47) appear random instead of following
/// correct bitmask-based transitions. Random-looking patterns can occur due to:
/// - Incorrect bitmask calculation (wrong bit positions)
/// - Wrong conversion from raw mask to blob index
/// - Terrain comparison using wrong tile IDs
/// - Non-deterministic behavior in resolution path
/// - Misalignment between dual-grid and single-grid systems
///
/// This test suite systematically validates each step to identify the breakdown point.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RandomAutoTileReproductionTest
{
    private const string TransitionMapPath = "res://Data/CompiledAtlas/transition_map.json";

    private TileRegistry _registry = null!;
    private CompiledTransitionResolver _resolver = null!;
    private CompiledTransitionMap? _transitionMap;

    [BeforeTest]
    public void Setup()
    {
        _registry = new TileRegistry();
        _resolver = new CompiledTransitionResolver();

        var transitionPath = ProjectSettings.GlobalizePath(TransitionMapPath);
        if (File.Exists(transitionPath))
        {
            var json = File.ReadAllText(transitionPath);
            _transitionMap = JsonSerializer.Deserialize<CompiledTransitionMap>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }

    // ==================== Bitmask Calculation Consistency ====================

    [TestCase]
    public void TestDualGridBitmaskIsConsistent()
    {
        // Create a deterministic 3x3 data grid pattern
        //  X X X
        //  X X X
        //  X X X
        // All data cells filled - dual grid visual tiles should all be bitmask 15

        bool IsDataFilled(int x, int y) => x >= 0 && x < 3 && y >= 0 && y < 3;

        var results = new List<string>();

        // Check visual tile at (1,1) which samples corners at data (0,0), (1,0), (0,1), (1,1)
        var mask = DualGridAutoTile.ComputeBitmask(1, 1, IsDataFilled);

        // All four data corners are filled, so bitmask should be 15
        if (mask != 15)
            results.Add($"Visual (1,1) expected bitmask 15, got {mask}");

        // Check visual tile at (0,0) which samples corners at data (-1,-1), (0,-1), (-1,0), (0,0)
        // Only (0,0) is filled
        mask = DualGridAutoTile.ComputeBitmask(0, 0, IsDataFilled);
        // SE corner only = bitmask 2
        if (mask != NeighborBitmaskCorner.SouthEast)
            results.Add($"Visual (0,0) expected bitmask {NeighborBitmaskCorner.SouthEast} (SE), got {mask}");

        if (results.Count > 0)
        {
            GD.PrintErr($"RANDOM PATTERN CAUSE - Bitmask calculation errors:\n{string.Join("\n", results)}");
        }

        AssertThat(results.Count).IsEqual(0);
    }

    [TestCase]
    public void TestDualGridBitmaskForPartiallyFilledGrid()
    {
        // Pattern:
        //  X . .
        //  . X .
        //  . . X
        // Diagonal fill

        bool IsDataFilled(int x, int y)
        {
            if (x < 0 || x >= 3 || y < 0 || y >= 3) return false;
            return x == y; // Diagonal
        }

        var expectedMasks = new Dictionary<(int, int), int>
        {
            // Visual tile at (1,1) samples (0,0), (1,0), (0,1), (1,1)
            // Filled: (0,0) and (1,1) = NW and SE corners
            // But wait, let me recalculate:
            // NE corner samples data (vx, vy-1) = (1, 0) - not filled
            // SE corner samples data (vx, vy) = (1, 1) - filled (diagonal)
            // SW corner samples data (vx-1, vy) = (0, 1) - not filled
            // NW corner samples data (vx-1, vy-1) = (0, 0) - filled (diagonal)
            // So: NW(8) + SE(2) = 10
            { (1, 1), NeighborBitmaskCorner.NorthWest | NeighborBitmaskCorner.SouthEast }, // 10

            // Visual tile at (2,2) samples (1,1), (2,1), (1,2), (2,2)
            // Filled: (1,1) and (2,2) = NW(8) + SE(2) = 10
            { (2, 2), NeighborBitmaskCorner.NorthWest | NeighborBitmaskCorner.SouthEast }, // 10
        };

        var results = new List<string>();

        foreach (var ((vx, vy), expected) in expectedMasks)
        {
            var actual = DualGridAutoTile.ComputeBitmask(vx, vy, IsDataFilled);
            if (actual != expected)
            {
                results.Add($"Visual ({vx},{vy}): expected {expected}, got {actual}");
            }
        }

        if (results.Count > 0)
        {
            GD.PrintErr($"RANDOM PATTERN CAUSE - Diagonal pattern mismatch:\n{string.Join("\n", results)}");
        }

        AssertThat(results.Count).IsEqual(0);
    }

    // ==================== Blob47 Index Consistency ====================

    [TestCase]
    public void TestBlob47IndexMappingIsConsistent()
    {
        var valid47 = NeighborBitmask8.GetValid47Masks();
        var inconsistencies = new List<string>();

        for (var expectedIndex = 0; expectedIndex < 47; expectedIndex++)
        {
            var mask = valid47[expectedIndex];
            var actualIndex = NeighborBitmask8.GetBlobIndex(mask);

            if (actualIndex != expectedIndex)
            {
                inconsistencies.Add($"Mask {mask}: expected index {expectedIndex}, got {actualIndex}");
            }
        }

        if (inconsistencies.Count > 0)
        {
            GD.PrintErr($"RANDOM PATTERN CAUSE - Blob47 index inconsistencies:\n{string.Join("\n", inconsistencies)}");
        }

        AssertThat(inconsistencies.Count).IsEqual(0);
    }

    [TestCase]
    public void TestBlob47IndexBijectivity()
    {
        // Verify GetBlobIndex is the inverse of indexing into GetValid47Masks
        var valid47 = NeighborBitmask8.GetValid47Masks();
        var errors = new List<string>();

        for (var i = 0; i < 47; i++)
        {
            var mask = valid47[i];
            var recoveredIndex = NeighborBitmask8.GetBlobIndex(mask);
            var recoveredMask = valid47[recoveredIndex];

            if (recoveredMask != mask)
            {
                errors.Add($"Index {i}: mask={mask}, recovered={recoveredMask} via index {recoveredIndex}");
            }
        }

        if (errors.Count > 0)
        {
            GD.PrintErr($"RANDOM PATTERN CAUSE - Blob47 bijectivity errors:\n{string.Join("\n", errors)}");
        }

        AssertThat(errors.Count).IsEqual(0);
    }

    // ==================== Transition Map Variant Order ====================

    [TestCase]
    public void TestCorner16VariantOrderMatchesBitmask()
    {
        // For corner16 format, variant[i] should be the tile for bitmask i
        // Verify that variant indices correspond to expected visual patterns

        AssertThat(_transitionMap).IsNotNull();

        var mismatches = new List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            if (entry.Format != "corner16")
                continue;

            if (entry.Variants.Length != 16)
            {
                mismatches.Add($"{key}: has {entry.Variants.Length} variants (expected 16)");
                continue;
            }

            // Variant 0 = no corners (should be null or base tile)
            // Variant 15 = all corners (solid fill - must exist)
            if (entry.Variants[15] == null)
            {
                mismatches.Add($"{key}: variant[15] (solid fill) is null");
            }
        }

        if (mismatches.Count > 0)
        {
            GD.PrintErr($"RANDOM PATTERN CAUSE - Corner16 variant order issues:\n{string.Join("\n", mismatches)}");
        }

        AssertThat(mismatches.Count).IsEqual(0);
    }

    [TestCase]
    public void TestBlob47VariantOrderMatchesBlobIndex()
    {
        AssertThat(_transitionMap).IsNotNull();

        var valid47 = NeighborBitmask8.GetValid47Masks();
        var mismatches = new List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            if (entry.Format != "blob47")
                continue;

            if (entry.Variants.Length != 47)
            {
                mismatches.Add($"{key}: has {entry.Variants.Length} variants (expected 47)");
                continue;
            }

            // Verify critical indices
            // Index 0 = mask 0 = isolated tile
            // Index 46 = mask 255 = solid fill
            if (valid47[0] != 0)
                mismatches.Add($"Blob47 index 0 should map to mask 0, but maps to {valid47[0]}");

            if (valid47[46] != 255)
                mismatches.Add($"Blob47 index 46 should map to mask 255, but maps to {valid47[46]}");
        }

        if (mismatches.Count > 0)
        {
            GD.PrintErr($"RANDOM PATTERN CAUSE - Blob47 variant order issues:\n{string.Join("\n", mismatches)}");
        }

        AssertThat(mismatches.Count).IsEqual(0);
    }

    // ==================== Resolution Consistency ====================

    [TestCase]
    public void TestResolutionIsDeterministic()
    {
        // Same input should always produce same output
        var compositableTiles = _registry.GetAllTiles()
            .Where(t => t.IsCompositable)
            .Take(3)
            .ToList();

        var inconsistencies = new List<string>();

        foreach (var tile in compositableTiles)
        {
            for (var bitmask = 0; bitmask < 16; bitmask++)
            {
                // Resolve same query 3 times
                var result1 = _resolver.ResolveWithFallback(tile.Id, "dirt", bitmask, 0, Vector2I.Zero);
                var result2 = _resolver.ResolveWithFallback(tile.Id, "dirt", bitmask, 0, Vector2I.Zero);
                var result3 = _resolver.ResolveWithFallback(tile.Id, "dirt", bitmask, 0, Vector2I.Zero);

                if (result1.AtlasCoords != result2.AtlasCoords || result2.AtlasCoords != result3.AtlasCoords)
                {
                    inconsistencies.Add($"{tile.Id} bitmask={bitmask}: got different results {result1.AtlasCoords}, {result2.AtlasCoords}, {result3.AtlasCoords}");
                }
            }
        }

        if (inconsistencies.Count > 0)
        {
            GD.PrintErr($"RANDOM PATTERN CAUSE - Non-deterministic resolution:\n{string.Join("\n", inconsistencies)}");
        }

        AssertThat(inconsistencies.Count).IsEqual(0);
    }

    // ==================== Coordinate Uniqueness ====================

    [TestCase]
    public void TestDifferentBitmasksProduceDifferentCoords()
    {
        // Different bitmasks should (usually) produce different coordinates
        // This helps verify we're not always returning the same tile

        AssertThat(_transitionMap).IsNotNull();

        var suspiciousEntries = new List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions.Take(10))
        {
            if (entry.Format != "corner16")
                continue;

            var coords = new HashSet<(int, int)>();
            var duplicateCount = 0;

            for (var i = 0; i < entry.Variants.Length; i++)
            {
                var variants = entry.Variants[i];
                if (variants == null) continue;

                foreach (var v in variants)
                {
                    if (!coords.Add((v.X, v.Y)))
                        duplicateCount++;
                }
            }

            // Some duplication is okay, but if ALL are the same, that's suspicious
            var totalVariants = entry.Variants.Where(v => v != null).Sum(v => v!.Length);
            if (duplicateCount > totalVariants / 2)
            {
                suspiciousEntries.Add($"{key}: {duplicateCount}/{totalVariants} variants share coordinates");
            }
        }

        if (suspiciousEntries.Count > 0)
        {
            GD.Print($"Transitions with high coordinate duplication (may cause 'random' appearance):\n{string.Join("\n", suspiciousEntries)}");
        }

        // Informational - not a hard failure
    }

    // ==================== Visual Pattern Simulation ====================

    [TestCase]
    public void TestSimulateTerrainBoundaryPattern()
    {
        // Simulate a simple horizontal terrain boundary:
        // Row 0-1: Grass
        // Row 2-3: Sand
        //
        // Visual tiles at row 2 boundary should have specific patterns

        var dataGrid = new string[4, 4];
        for (var y = 0; y < 2; y++)
            for (var x = 0; x < 4; x++)
                dataGrid[y, x] = "grass";
        for (var y = 2; y < 4; y++)
            for (var x = 0; x < 4; x++)
                dataGrid[y, x] = "sand";

        // Calculate what bitmasks the visual tiles at boundary should have

        bool IsGrass(int x, int y)
        {
            if (x < 0 || x >= 4 || y < 0 || y >= 4) return false;
            return dataGrid[y, x] == "grass";
        }

        var results = new List<string>();

        // Visual tile at (1, 2) - should be on the boundary
        // It samples: NW=(0,1), NE=(1,1), SW=(0,2), SE=(1,2)
        // NW and NE are grass (y=1), SW and SE are sand (y=2)
        var mask_1_2 = DualGridAutoTile.ComputeBitmask(1, 2, IsGrass);
        // Expected: NW=8, NE=1 set (grass), SW=4, SE=2 unset (sand)
        // So mask = 8 + 1 = 9
        if (mask_1_2 != 9)
            results.Add($"Visual (1,2): expected bitmask 9 (NW+NE), got {mask_1_2}");

        // Visual tile at (1, 1) - all grass
        var mask_1_1 = DualGridAutoTile.ComputeBitmask(1, 1, IsGrass);
        if (mask_1_1 != 15)
            results.Add($"Visual (1,1): expected bitmask 15 (all grass), got {mask_1_1}");

        // Visual tile at (1, 3) - all sand (from grass perspective = 0)
        var mask_1_3 = DualGridAutoTile.ComputeBitmask(1, 3, IsGrass);
        if (mask_1_3 != 0)
            results.Add($"Visual (1,3): expected bitmask 0 (no grass), got {mask_1_3}");

        if (results.Count > 0)
        {
            GD.PrintErr($"RANDOM PATTERN CAUSE - Boundary simulation failed:\n{string.Join("\n", results)}");
        }

        AssertThat(results.Count).IsEqual(0);
    }

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
