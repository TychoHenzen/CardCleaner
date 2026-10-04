using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.DualGrid;

/// <summary>
///     DualGridRenderingUtilityTest scenarios split out of DualGridAutoTileTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DualGridRenderingUtilityTest
{
    // ==================== Should Render Tests ====================

    [TestCase]
    public void TestShouldRenderVisualTile_AnyCornerFilled()
    {
        var dataGrid = new[,]
        {
            { true, false },
            { false, false }
        };

        // Visual (1,1) has NW corner filled -> should render
        AssertBool(DualGridAutoTile.ShouldRenderVisualTile(1, 1, dataGrid)).IsTrue();
    }

    [TestCase]
    public void TestShouldRenderVisualTile_NoCornersFilled()
    {
        var dataGrid = new[,]
        {
            { true, false },
            { false, false }
        };

        // Visual (2,2) has no corners in a position where data exists
        // But the data is only at (0,0) which is NW for (1,1)
        // For (2,2), corners are: NW=(1,1), NE=(2,1), SW=(1,2), SE=(2,2) - all out of bounds or false
        AssertBool(DualGridAutoTile.ShouldRenderVisualTile(2, 2, dataGrid)).IsFalse();
    }

    // ==================== Filled Corner Count ====================

    [TestCase]
    public void TestGetFilledCornerCount()
    {
        var dataGrid = new[,]
        {
            { true, true },
            { true, false }
        };

        // Visual (1,1) samples all 4 data cells
        var count = DualGridAutoTile.GetFilledCornerCount(1, 1, dataGrid);
        AssertThat(count).IsEqual(3); // NW, NE, SW are true
    }

    // ==================== Exhaustive All 16 Bitmasks ====================

    [TestCase]
    public void TestAllSixteenBitmasks()
    {
        // Test all 16 possible bitmask values (0-15)
        for (var expected = 0; expected < 16; expected++)
        {
            var hasNW = (expected & NeighborBitmaskCorner.NorthWest) != 0;
            var hasNE = (expected & NeighborBitmaskCorner.NorthEast) != 0;
            var hasSW = (expected & NeighborBitmaskCorner.SouthWest) != 0;
            var hasSE = (expected & NeighborBitmaskCorner.SouthEast) != 0;

            var dataGrid = new[,]
            {
                { hasNW, hasNE },
                { hasSW, hasSE }
            };

            var actual = DualGridAutoTile.ComputeBitmask(1, 1, dataGrid);

            if (actual != expected)
            {
                GD.PrintErr(
                    $"Bitmask {expected}: expected={expected}, actual={actual}, " +
                    $"grid=[NW={hasNW},NE={hasNE},SW={hasSW},SE={hasSE}]");
            }

            AssertThat(actual).IsEqual(expected);
        }
    }

    // ==================== Function-based API ====================

    [TestCase]
    public void TestComputeBitmaskWithFunction()
    {
        // Test the function-based overload
        bool IsDataCellFilled(int x, int y)
        {
            // Simple function: filled if both coords are even
            return x >= 0 && y >= 0 && x % 2 == 0 && y % 2 == 0;
        }

        // Visual (1,1) samples: NW=(0,0), NE=(1,0), SW=(0,1), SE=(1,1)
        // Only (0,0) satisfies x%2==0 && y%2==0
        var mask = DualGridAutoTile.ComputeBitmask(1, 1, IsDataCellFilled);
        AssertThat(mask).IsEqual(NeighborBitmaskCorner.NorthWest); // 8
    }

    // ==================== Visual Tile Position ====================

    [TestCase]
    public void TestGetVisualTilePosition()
    {
        var tileSize = new Vector2I(16, 16);

        // Visual tile (0,0) should be at (-8, -8) pixels (half tile offset)
        var pos00 = DualGridAutoTile.GetVisualTilePosition(0, 0, tileSize);
        AssertThat(pos00.X).IsEqual(-8f);
        AssertThat(pos00.Y).IsEqual(-8f);

        // Visual tile (1,1) should be at (8, 8) pixels
        var pos11 = DualGridAutoTile.GetVisualTilePosition(1, 1, tileSize);
        AssertThat(pos11.X).IsEqual(8f);
        AssertThat(pos11.Y).IsEqual(8f);

        // Visual tile (2,2) should be at (24, 24) pixels
        var pos22 = DualGridAutoTile.GetVisualTilePosition(2, 2, tileSize);
        AssertThat(pos22.X).IsEqual(24f);
        AssertThat(pos22.Y).IsEqual(24f);
    }
}
