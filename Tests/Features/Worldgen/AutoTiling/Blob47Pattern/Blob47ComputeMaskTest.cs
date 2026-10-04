using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.Blob47Pattern;

/// <summary>
///     Blob47ComputeMaskTest scenarios split out of Blob47PatternValidationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Blob47ComputeMaskTest
{
    // ==================== Compute Tests ====================

    [TestCase]
    public void TestComputeWithIsolatedTile()
    {
        var tileIds = new string[3, 3];
        tileIds[1, 1] = "test";

        var mask = NeighborBitmask8.Compute(new Vector2I(1, 1), "test", tileIds, new Vector2I(3, 3));

        // No neighbors match, so mask should be 0
        AssertThat(mask).IsEqual(0);
    }

    [TestCase]
    public void TestComputeWithSurroundedTile()
    {
        var tileIds = new string[3, 3];
        for (var y = 0; y < 3; y++)
            for (var x = 0; x < 3; x++)
                tileIds[y, x] = "test";

        var mask = NeighborBitmask8.Compute(new Vector2I(1, 1), "test", tileIds, new Vector2I(3, 3));

        // All neighbors match, so mask should be 255
        AssertThat(mask).IsEqual(255);
    }

    [TestCase]
    public void TestComputeWithHorizontalNeighbors()
    {
        var tileIds = new string[3, 3];
        tileIds[1, 0] = "test"; // West
        tileIds[1, 1] = "test"; // Center
        tileIds[1, 2] = "test"; // East

        var mask = NeighborBitmask8.Compute(new Vector2I(1, 1), "test", tileIds, new Vector2I(3, 3));

        // W + E
        var expected = NeighborBitmask8.West | NeighborBitmask8.East;
        AssertThat(mask).IsEqual(expected);
    }

    [TestCase]
    public void TestComputeWithCornerNeighbors()
    {
        var tileIds = new string[3, 3];
        tileIds[0, 0] = "test"; // NW
        tileIds[0, 1] = "test"; // N
        tileIds[1, 0] = "test"; // W
        tileIds[1, 1] = "test"; // Center

        var mask = NeighborBitmask8.Compute(new Vector2I(1, 1), "test", tileIds, new Vector2I(3, 3));

        // N + W + NW (all valid because N and W are present)
        var expected = NeighborBitmask8.North | NeighborBitmask8.West | NeighborBitmask8.NorthWest;
        AssertThat(mask).IsEqual(expected);
    }

    [TestCase]
    public void TestComputeNormalizesInvalidCorners()
    {
        var tileIds = new string[3, 3];
        tileIds[0, 0] = "test"; // NW (but no N or W edges!)
        tileIds[1, 1] = "test"; // Center

        var mask = NeighborBitmask8.Compute(new Vector2I(1, 1), "test", tileIds, new Vector2I(3, 3));

        // NW corner should be cleared because N and W edges aren't set
        AssertThat(mask).IsEqual(0);
    }

    // ==================== Statistics ====================

    [TestCase]
    public void TestValid47MasksStatistics()
    {
        var validMasks = NeighborBitmask8.GetValid47Masks();

        var edgesOnlyCount = validMasks.Count(m => (m & NeighborBitmask8.AllCorners) == 0);
        var fullCornersCount = validMasks.Count(m => (m & NeighborBitmask8.AllCorners) == NeighborBitmask8.AllCorners);

        GD.Print("Blob47 Mask Statistics:");
        GD.Print($"  Total valid masks: {validMasks.Count}");
        GD.Print($"  Edges-only masks: {edgesOnlyCount}"); // Should be 16 (2^4)
        GD.Print($"  Full corners masks: {fullCornersCount}"); // Only mask 255

        AssertThat(edgesOnlyCount).IsEqual(16);
        AssertThat(fullCornersCount).IsEqual(1);
    }

    // ==================== Visual Reference ====================

    [TestCase]
    public void TestLogAllValidMasksWithDescriptions()
    {
        var validMasks = NeighborBitmask8.GetValid47Masks();

        GD.Print("All 47 Valid Blob Masks:");
        for (var i = 0; i < validMasks.Count; i++)
        {
            var mask = validMasks[i];
            var description = NeighborBitmask8.GetDescription(mask);
            GD.Print($"  Index {i,2}: {description}");
        }

        // Informational - just verify we logged 47
        AssertThat(validMasks.Count).IsEqual(47);
    }
}
