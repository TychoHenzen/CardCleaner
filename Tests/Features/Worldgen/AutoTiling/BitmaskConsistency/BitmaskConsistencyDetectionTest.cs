using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.BitmaskConsistency;

/// <summary>
///     BitmaskConsistencyDetectionTest scenarios split out of BitmaskConsistencyValidatorTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BitmaskConsistencyDetectionTest
{
    // ==================== Consistent Configurations ====================

    /// <summary>
    /// Test that a uniform grid (all same topTerrain, all same bitmask) passes validation.
    /// </summary>
    [TestCase]
    public void TestUniformGrid_NoViolations()
    {
        var overlays = new Dictionary<Vector2I, (string, string, int)>
        {
            [new Vector2I(0, 0)] = ("base", "grass", 15), // All corners
            [new Vector2I(1, 0)] = ("base", "grass", 15),
            [new Vector2I(0, 1)] = ("base", "grass", 15),
            [new Vector2I(1, 1)] = ("base", "grass", 15)
        };

        var violations = BitmaskConsistencyValidator.ValidateConsistency(overlays, 2, 2);
        AssertThat(violations).HasSize(0);
    }

    /// <summary>
    /// Test that a consistent edge (one terrain region) passes validation.
    /// Adjacent tiles with same topTerrain must have matching bits for shared corners.
    /// </summary>
    [TestCase]
    public void TestConsistentHorizontalEdge_NoViolations()
    {
        // Visual grid 3x1:
        // [0,0] has grass at SE (bitmask 2), NE not set (0)
        // [1,0] has grass at NW and SW (bitmask 12)
        // Shared corners: [0,0].NE and [1,0].NW should match
        //                [0,0].SE and [1,0].SW should match
        // [0,0].NE = 0 (not set), [1,0].NW = 1 (bit 8 set) - MISMATCH? No wait...
        // Let me reconsider: [0,0] bitmask 2 = SE only. NE = bit 0 = 0.
        // [1,0] bitmask 12 = SW + NW. NW = bit 3 = 1.
        // They have DIFFERENT topTerrains? No, both are "grass".
        // If same topTerrain, shared corner bits must match.
        // [0,0].NE = 0, [1,0].NW = 1 -> VIOLATION!

        // For no violations, let's create a proper consistent configuration:
        // Both tiles have grass.
        // [0,0] has grass at NE and SE (right side) -> bitmask 3 (1+2)
        // [1,0] has grass at NW and SW (left side) -> bitmask 12 (4+8)
        // [0,0].NE = 1, [1,0].NW = 1 ✓
        // [0,0].SE = 1, [1,0].SW = 1 ✓
        var overlays = new Dictionary<Vector2I, (string, string, int)>
        {
            [new Vector2I(0, 0)] = ("base", "grass", 3), // NE + SE
            [new Vector2I(1, 0)] = ("base", "grass", 12) // SW + NW
        };

        var violations = BitmaskConsistencyValidator.ValidateConsistency(overlays, 2, 1);
        AssertThat(violations).HasSize(0);
    }

    /// <summary>
    /// Test that a consistent vertical edge passes validation.
    /// </summary>
    [TestCase]
    public void TestConsistentVerticalEdge_NoViolations()
    {
        // [0,0] has grass at SW and SE (bottom) -> bitmask 6 (2+4)
        // [0,1] has grass at NW and NE (top) -> bitmask 9 (1+8)
        // Shared: [0,0].SW = 1, [0,1].NW = 1 ✓
        //         [0,0].SE = 1, [0,1].NE = 1 ✓
        var overlays = new Dictionary<Vector2I, (string, string, int)>
        {
            [new Vector2I(0, 0)] = ("base", "grass", 6), // SE + SW
            [new Vector2I(0, 1)] = ("base", "grass", 9)  // NE + NW
        };

        var violations = BitmaskConsistencyValidator.ValidateConsistency(overlays, 1, 2);
        AssertThat(violations).HasSize(0);
    }

    // ==================== Inconsistent Configurations ====================

    /// <summary>
    /// Test detection of horizontal edge violation.
    /// Adjacent tiles with same topTerrain but mismatched shared corner bits.
    /// </summary>
    [TestCase]
    public void TestHorizontalEdgeViolation_Detected()
    {
        // [0,0] bitmask 2 = SE only (NE=0, SE=1, SW=0, NW=0)
        // [1,0] bitmask 15 = all corners (NE=1, SE=1, SW=1, NW=1)
        // Same topTerrain "grass"
        // Shared: [0,0].NE = 0, [1,0].NW = 1 -> VIOLATION!
        //         [0,0].SE = 1, [1,0].SW = 1 -> OK
        var overlays = new Dictionary<Vector2I, (string, string, int)>
        {
            [new Vector2I(0, 0)] = ("base", "grass", 2),
            [new Vector2I(1, 0)] = ("base", "grass", 15)
        };

        var violations = BitmaskConsistencyValidator.ValidateConsistency(overlays, 2, 1);
        AssertThat(violations).HasSize(1);
        AssertThat(violations[0].CornerNameA).IsEqual("NE");
        AssertThat(violations[0].CornerNameB).IsEqual("NW");
    }

    /// <summary>
    /// Test detection of vertical edge violation.
    /// </summary>
    [TestCase]
    public void TestVerticalEdgeViolation_Detected()
    {
        // [0,0] bitmask 1 = NE only (NE=1, SE=0, SW=0, NW=0)
        // [0,1] bitmask 15 = all corners (NE=1, SE=1, SW=1, NW=1)
        // Same topTerrain "grass"
        // Shared: [0,0].SW = 0, [0,1].NW = 1 -> VIOLATION!
        //         [0,0].SE = 0, [0,1].NE = 1 -> VIOLATION!
        var overlays = new Dictionary<Vector2I, (string, string, int)>
        {
            [new Vector2I(0, 0)] = ("base", "grass", 1),
            [new Vector2I(0, 1)] = ("base", "grass", 15)
        };

        var violations = BitmaskConsistencyValidator.ValidateConsistency(overlays, 1, 2);
        AssertThat(violations).HasSize(2); // Two shared corners, both violated
    }
}
