using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.RandomReproduction;

/// <summary>
///     RandomDualGridBitmaskTest scenarios split out of RandomAutoTileReproductionTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RandomDualGridBitmaskTest : RandomAutoTileReproductionTestBase
{
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
            // Diagonal data cells should produce the NW+SE mask (10).
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
}
