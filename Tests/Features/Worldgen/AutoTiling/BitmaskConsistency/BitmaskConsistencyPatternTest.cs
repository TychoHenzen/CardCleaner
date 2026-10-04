using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.BitmaskConsistency;

/// <summary>
///     BitmaskConsistencyPatternTest scenarios split out of BitmaskConsistencyValidatorTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BitmaskConsistencyPatternTest
{
    /// <summary>
    /// Test that different topTerrains don't trigger violations.
    /// Violations only apply when same topTerrain is selected.
    /// </summary>
    [TestCase]
    public void TestDifferentTopTerrains_NoViolations()
    {
        // Different topTerrains: we don't require matching bits
        var overlays = new Dictionary<Vector2I, (string, string, int)>
        {
            [new Vector2I(0, 0)] = ("base", "grass", 2),  // SE only
            [new Vector2I(1, 0)] = ("base", "stone", 15)  // All corners
        };

        var violations = BitmaskConsistencyValidator.ValidateConsistency(overlays, 2, 1);
        AssertThat(violations).HasSize(0);
    }

    // ==================== Terrain Conflict Detection ====================

    /// <summary>
    /// Test detection of adjacent tiles with different topTerrains.
    /// </summary>
    [TestCase]
    public void TestTopTerrainConflicts_Detected()
    {
        var overlays = new Dictionary<Vector2I, (string, string, int)>
        {
            [new Vector2I(0, 0)] = ("base", "grass", 15),
            [new Vector2I(1, 0)] = ("base", "stone", 15),
            [new Vector2I(0, 1)] = ("base", "grass", 15),
            [new Vector2I(1, 1)] = ("base", "water", 15)
        };

        var conflicts = BitmaskConsistencyValidator.DetectTopTerrainConflicts(overlays, 2, 2);

        // Expected conflicts are grass/stone, grass/water, and stone/water pairs.
        AssertThat(conflicts).HasSize(3);
    }

    // ==================== Analysis Function ====================

    /// <summary>
    /// Test the Analyze function returns correct statistics.
    /// </summary>
    [TestCase]
    public void TestAnalyze_ReturnsCorrectStats()
    {
        var overlays = new Dictionary<Vector2I, (string, string, int)>
        {
            [new Vector2I(0, 0)] = ("base", "grass", 15),
            [new Vector2I(1, 0)] = ("base", "stone", 15),
            [new Vector2I(0, 1)] = ("base", "grass", 2),  // Intentional violation with [0,0]
            [new Vector2I(1, 1)] = ("base", "grass", 15)
        };

        var (total, unique, conflicts, violations) = BitmaskConsistencyValidator.Analyze(overlays, 2, 2);

        AssertThat(total).IsEqual(4);
        AssertThat(unique).IsEqual(2); // grass, stone
        // [0,0] grass vs [1,0] stone = conflict
        // [0,1] grass vs [1,1] grass = no conflict (same terrain)
        // [0,0] grass vs [0,1] grass = no conflict (same terrain)
        // [1,0] stone vs [1,1] grass = conflict
        AssertThat(conflicts).IsEqual(2);
        // Same-terrain neighbors still report bitmask edge violations.
        //   - Check NE/NW: (0,1).NE=0 vs (1,1).NW=1 -> VIOLATION
        //   - Check SE/SW: (0,1).SE=1 vs (1,1).SW=1 -> match
        AssertThat(violations).IsEqual(3);
    }

    // ==================== User-Reported Bug Pattern ====================

    /// <summary>
    /// Test reproducing the user's reported 5x5 bitmask pattern.
    /// Pattern: [[2,6,4,0,0],[6,4,12,2,4],[13,2,14,3,12],[12,13,9,4,8],[8,12,0,3,12]]
    /// All tiles have same topTerrain. Should detect violations if adjacent bits mismatch.
    /// </summary>
    [TestCase]
    public void TestUserReportedPattern_DetectsViolations()
    {
        // User's 5x5 bitmask pattern (all same topTerrain for testing violations)
        var bitmasks = new[,]
        {
            { 2, 6, 4, 0, 0 },
            { 6, 4, 12, 2, 4 },
            { 13, 2, 14, 3, 12 },
            { 12, 13, 9, 4, 8 },
            { 8, 12, 0, 3, 12 }
        };

        var overlays = new Dictionary<Vector2I, (string, string, int)>();
        for (var y = 0; y < 5; y++)
        for (var x = 0; x < 5; x++)
        {
            overlays[new Vector2I(x, y)] = ("base", "terrain", bitmasks[y, x]);
        }

        var violations = BitmaskConsistencyValidator.ValidateConsistency(overlays, 5, 5);

        // Check [0,0] (mask 2 = SE) and [0,1] (mask 6 = SE+SW)
        // [0,0].SE = 1, [0,1].NE = 0 (mask 6: NE=0) -> VIOLATION
        // [0,0].SW = 0, [0,1].NW = 0 (mask 6: NW=0) -> OK

        // This pattern should have violations if bitmasks are inconsistent
        // Let's verify at least one exists
        AssertThat(violations.Count).IsGreater(0);
    }

    /// <summary>
    /// Test that a properly consistent 5x5 pattern passes validation.
    /// This is what we'd expect after the fix is applied.
    /// </summary>
    [TestCase]
    public void TestConsistent5x5Pattern_NoViolations()
    {
        // Create a consistent checkerboard-like pattern
        // All tiles same topTerrain, bitmasks reflect actual data
        var overlays = new Dictionary<Vector2I, (string, string, int)>();

        // Simple pattern: full fill everywhere (bitmask 15)
        for (var y = 0; y < 5; y++)
        for (var x = 0; x < 5; x++)
        {
            overlays[new Vector2I(x, y)] = ("base", "terrain", 15);
        }

        var violations = BitmaskConsistencyValidator.ValidateConsistency(overlays, 5, 5);
        AssertThat(violations).HasSize(0);
    }

    /// <summary>
    /// Test a consistent gradient pattern (terrain fading from left to right).
    /// </summary>
    [TestCase]
    public void TestConsistentGradientPattern()
    {
        // Consistent gradient: terrain present on left, absent on right
        // Visual 0: SE+SW = 6 (terrain at left data cells)
        // Visual 1: NW+SW = 12 (terrain at left data cells)
        // Visual 2: no terrain = 0
        // Check: [0].SE = 1, [1].SW = 1 ✓
        //        [0].NE = 0, [1].NW = 1... wait, need to be careful
        // Actually for gradient: let's do corners properly
        // Data grid: T T F (3 cells)
        // Visual 0: samples (-1,-1), (0,-1), (-1,0), (0,0) -> OOB, OOB, OOB, T = SE only (2)
        // Visual 1: samples (0,-1), (1,-1), (0,0), (1,0) -> OOB, OOB, T, T = SE+SW (6)
        // Visual 2: samples (1,-1), (2,-1), (1,0), (2,0) -> OOB, OOB, T, F = SW (4)
        // Visual 3: samples (2,-1), (3,-1), (2,0), (3,0) -> all OOB or F = 0

        // Check adjacencies (all same terrain):
        // [0]-[1]: [0].NE = 0, [1].NW = 0 ✓; [0].SE = 1, [1].SW = 1 ✓
        // [1]-[2]: [1].NE = 0, [2].NW = 0 ✓; [1].SE = 1, [2].SW = 1 ✓
        // [2]-[3]: [2].NE = 0, [3].NW = 0 ✓; [2].SE = 0, [3].SW = 0 ✓

        var overlays = new Dictionary<Vector2I, (string, string, int)>
        {
            [new Vector2I(0, 0)] = ("base", "terrain", 2),  // SE
            [new Vector2I(1, 0)] = ("base", "terrain", 6),  // SE+SW
            [new Vector2I(2, 0)] = ("base", "terrain", 4),  // SW
            [new Vector2I(3, 0)] = ("base", "terrain", 0)   // nothing
        };

        var violations = BitmaskConsistencyValidator.ValidateConsistency(overlays, 4, 1);
        AssertThat(violations).HasSize(0);
    }
}
