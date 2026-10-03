using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling;

/// <summary>Tests four-corner sampling for correct Corner16 bitmasks.</summary>
[TestSuite]
[RequireGodotRuntime]
public class DualGridAutoTileTest
{
    // ==================== Single Corner Tests ====================

    /// <summary>
    /// Test bitmask 0: No corners filled (out of bounds or all false)
    /// </summary>
    [TestCase]
    public void TestBitmask0_NoCorners()
    {
        // 1x1 data grid with false
        var dataGrid = new[,] { { false } };

        // Visual position (0,0) is at the top-left, all 4 samples are out of bounds
        var mask = DualGridAutoTile.ComputeBitmask(0, 0, dataGrid);
        AssertThat(mask).IsEqual(0);
    }

    /// <summary>
    /// Test bitmask 1: Only NE corner filled
    /// </summary>
    [TestCase]
    public void TestBitmask1_NE()
    {
        // Visual tile (1,1) samples NE from data[0,1], others from data[1,0], data[0,0], data[1,1]
        var dataGrid = new[,]
        {
            { false, true },  // row 0: [0,0]=false, [0,1]=true (this is NE for visual 1,1)
            { false, false }  // row 1: [1,0]=false, [1,1]=false
        };

        var mask = DualGridAutoTile.ComputeBitmask(1, 1, dataGrid);
        AssertThat(mask).IsEqual(NeighborBitmaskCorner.NorthEast); // 1
    }

    /// <summary>
    /// Test bitmask 2: Only SE corner filled
    /// </summary>
    [TestCase]
    public void TestBitmask2_SE()
    {
        var dataGrid = new[,]
        {
            { false, false }, // row 0
            { false, true }   // row 1: [1,1]=true is SE for visual (1,1)
        };

        var mask = DualGridAutoTile.ComputeBitmask(1, 1, dataGrid);
        AssertThat(mask).IsEqual(NeighborBitmaskCorner.SouthEast); // 2
    }

    /// <summary>
    /// Test bitmask 4: Only SW corner filled
    /// </summary>
    [TestCase]
    public void TestBitmask4_SW()
    {
        var dataGrid = new[,]
        {
            { false, false }, // row 0
            { true, false }   // row 1: [1,0]=true is SW for visual (1,1)
        };

        var mask = DualGridAutoTile.ComputeBitmask(1, 1, dataGrid);
        AssertThat(mask).IsEqual(NeighborBitmaskCorner.SouthWest); // 4
    }

    /// <summary>
    /// Test bitmask 8: Only NW corner filled
    /// </summary>
    [TestCase]
    public void TestBitmask8_NW()
    {
        var dataGrid = new[,]
        {
            { true, false },  // row 0: [0,0]=true is NW for visual (1,1)
            { false, false }  // row 1
        };

        var mask = DualGridAutoTile.ComputeBitmask(1, 1, dataGrid);
        AssertThat(mask).IsEqual(NeighborBitmaskCorner.NorthWest); // 8
    }

    // ==================== Two Corner Combinations ====================

    /// <summary>
    /// Test bitmask 3: NE + SE (East side)
    /// </summary>
    [TestCase]
    public void TestBitmask3_NE_SE()
    {
        var dataGrid = new[,]
        {
            { false, true },  // NE
            { false, true }   // SE
        };

        var mask = DualGridAutoTile.ComputeBitmask(1, 1, dataGrid);
        AssertThat(mask).IsEqual(NeighborBitmaskCorner.NorthEast | NeighborBitmaskCorner.SouthEast); // 3
    }

    /// <summary>
    /// Test bitmask 5: NE + SW (Diagonal)
    /// </summary>
    [TestCase]
    public void TestBitmask5_NE_SW()
    {
        var dataGrid = new[,]
        {
            { false, true },  // NE
            { true, false }   // SW
        };

        var mask = DualGridAutoTile.ComputeBitmask(1, 1, dataGrid);
        AssertThat(mask).IsEqual(NeighborBitmaskCorner.NorthEast | NeighborBitmaskCorner.SouthWest); // 5
    }

    /// <summary>
    /// Test bitmask 6: SE + SW (South side)
    /// </summary>
    [TestCase]
    public void TestBitmask6_SE_SW()
    {
        var dataGrid = new[,]
        {
            { false, false },
            { true, true }    // SW + SE
        };

        var mask = DualGridAutoTile.ComputeBitmask(1, 1, dataGrid);
        AssertThat(mask).IsEqual(NeighborBitmaskCorner.SouthEast | NeighborBitmaskCorner.SouthWest); // 6
    }

    /// <summary>
    /// Test bitmask 9: NE + NW (North side)
    /// </summary>
    [TestCase]
    public void TestBitmask9_NE_NW()
    {
        var dataGrid = new[,]
        {
            { true, true },   // NW + NE
            { false, false }
        };

        var mask = DualGridAutoTile.ComputeBitmask(1, 1, dataGrid);
        AssertThat(mask).IsEqual(NeighborBitmaskCorner.NorthEast | NeighborBitmaskCorner.NorthWest); // 9
    }

    /// <summary>
    /// Test bitmask 10: SE + NW (Diagonal)
    /// </summary>
    [TestCase]
    public void TestBitmask10_SE_NW()
    {
        var dataGrid = new[,]
        {
            { true, false },  // NW
            { false, true }   // SE
        };

        var mask = DualGridAutoTile.ComputeBitmask(1, 1, dataGrid);
        AssertThat(mask).IsEqual(NeighborBitmaskCorner.SouthEast | NeighborBitmaskCorner.NorthWest); // 10
    }

    /// <summary>
    /// Test bitmask 12: SW + NW (West side)
    /// </summary>
    [TestCase]
    public void TestBitmask12_SW_NW()
    {
        var dataGrid = new[,]
        {
            { true, false },  // NW
            { true, false }   // SW
        };

        var mask = DualGridAutoTile.ComputeBitmask(1, 1, dataGrid);
        AssertThat(mask).IsEqual(NeighborBitmaskCorner.SouthWest | NeighborBitmaskCorner.NorthWest); // 12
    }

    // ==================== Three Corner Combinations ====================

    /// <summary>
    /// Test bitmask 7: NE + SE + SW (missing NW)
    /// </summary>
    [TestCase]
    public void TestBitmask7_NE_SE_SW()
    {
        var dataGrid = new[,]
        {
            { false, true },  // NE
            { true, true }    // SW + SE
        };

        var mask = DualGridAutoTile.ComputeBitmask(1, 1, dataGrid);
        AssertThat(mask).IsEqual(
            NeighborBitmaskCorner.NorthEast |
            NeighborBitmaskCorner.SouthEast |
            NeighborBitmaskCorner.SouthWest); // 7
    }

    /// <summary>
    /// Test bitmask 11: NE + SE + NW (missing SW)
    /// </summary>
    [TestCase]
    public void TestBitmask11_NE_SE_NW()
    {
        var dataGrid = new[,]
        {
            { true, true },   // NW + NE
            { false, true }   // SE
        };

        var mask = DualGridAutoTile.ComputeBitmask(1, 1, dataGrid);
        AssertThat(mask).IsEqual(
            NeighborBitmaskCorner.NorthEast |
            NeighborBitmaskCorner.SouthEast |
            NeighborBitmaskCorner.NorthWest); // 11
    }

    /// <summary>
    /// Test bitmask 13: NE + SW + NW (missing SE)
    /// </summary>
    [TestCase]
    public void TestBitmask13_NE_SW_NW()
    {
        var dataGrid = new[,]
        {
            { true, true },   // NW + NE
            { true, false }   // SW
        };

        var mask = DualGridAutoTile.ComputeBitmask(1, 1, dataGrid);
        AssertThat(mask).IsEqual(
            NeighborBitmaskCorner.NorthEast |
            NeighborBitmaskCorner.SouthWest |
            NeighborBitmaskCorner.NorthWest); // 13
    }

    /// <summary>
    /// Test bitmask 14: SE + SW + NW (missing NE)
    /// </summary>
    [TestCase]
    public void TestBitmask14_SE_SW_NW()
    {
        var dataGrid = new[,]
        {
            { true, false },  // NW
            { true, true }    // SW + SE
        };

        var mask = DualGridAutoTile.ComputeBitmask(1, 1, dataGrid);
        AssertThat(mask).IsEqual(
            NeighborBitmaskCorner.SouthEast |
            NeighborBitmaskCorner.SouthWest |
            NeighborBitmaskCorner.NorthWest); // 14
    }

    // ==================== Full Fill ====================

    /// <summary>
    /// Test bitmask 15: All corners filled
    /// </summary>
    [TestCase]
    public void TestBitmask15_AllCorners()
    {
        var dataGrid = new[,]
        {
            { true, true },
            { true, true }
        };

        var mask = DualGridAutoTile.ComputeBitmask(1, 1, dataGrid);
        AssertThat(mask).IsEqual(15); // All corners
    }

    // ==================== Boundary Conditions ====================

    [TestCase]
    public void TestBoundaryTopLeft_AllOutOfBounds()
    {
        var dataGrid = new[,]
        {
            { true, true },
            { true, true }
        };

        // Visual (0,0) samples: NW=(-1,-1), NE=(0,-1), SW=(-1,0), SE=(0,0)
        // Only SE is in bounds
        var mask = DualGridAutoTile.ComputeBitmask(0, 0, dataGrid);
        AssertThat(mask).IsEqual(NeighborBitmaskCorner.SouthEast); // 2 - only SE is in bounds and true
    }

    [TestCase]
    public void TestBoundaryTopRight()
    {
        var dataGrid = new[,]
        {
            { true, true },
            { true, true }
        };

        // Visual (2,0) samples: NW=(1,-1), NE=(2,-1), SW=(1,0), SE=(2,0)
        // Only SW is in bounds (col=1 valid, row=0 valid); SE col=2 is out of bounds
        var mask = DualGridAutoTile.ComputeBitmask(2, 0, dataGrid);
        AssertThat(mask).IsEqual(NeighborBitmaskCorner.SouthWest); // 4
    }

    [TestCase]
    public void TestBoundaryBottomLeft()
    {
        var dataGrid = new[,]
        {
            { true, true },
            { true, true }
        };

        // Visual (0,2) samples: NW=(-1,1), NE=(0,1), SW=(-1,2), SE=(0,2)
        // Only NE is in bounds (col=0 valid, row=1 valid); SE row=2 is out of bounds
        var mask = DualGridAutoTile.ComputeBitmask(0, 2, dataGrid);
        AssertThat(mask).IsEqual(NeighborBitmaskCorner.NorthEast); // 1
    }

    [TestCase]
    public void TestBoundaryBottomRight()
    {
        var dataGrid = new[,]
        {
            { true, true },
            { true, true }
        };

        // Visual (2,2) samples: NW=(1,1), NE=(2,1), SW=(1,2), SE=(2,2)
        // Only NW is in bounds (col=1, row=1 valid); others are out of bounds
        var mask = DualGridAutoTile.ComputeBitmask(2, 2, dataGrid);
        AssertThat(mask).IsEqual(NeighborBitmaskCorner.NorthWest); // 8
    }

    // ==================== Visual Grid Size ====================

    [TestCase]
    public void TestVisualGridSizeIsDataPlusOne()
    {
        var dataSize = new Vector2I(10, 8);
        var visualSize = DualGridAutoTile.GetVisualGridSize(dataSize);

        AssertThat(visualSize.X).IsEqual(11);
        AssertThat(visualSize.Y).IsEqual(9);
    }

    [TestCase]
    public void TestVisualGridSizeFromArray()
    {
        var dataGrid = new bool[5, 10]; // 5 rows, 10 cols
        var visualSize = DualGridAutoTile.GetVisualGridSize(dataGrid);

        // Godot convention: X = cols, Y = rows
        AssertThat(visualSize.X).IsEqual(11); // cols + 1
        AssertThat(visualSize.Y).IsEqual(6);  // rows + 1
    }

    // ==================== Compute All Bitmasks ====================

    [TestCase]
    public void TestComputeAllBitmasks_2x2DataGrid()
    {
        var dataGrid = new[,]
        {
            { true, true },
            { true, true }
        };

        var bitmasks = DualGridAutoTile.ComputeAllBitmasks(dataGrid);

        // Visual grid is 3x3
        AssertThat(bitmasks.GetLength(0)).IsEqual(3); // rows
        AssertThat(bitmasks.GetLength(1)).IsEqual(3); // cols

        // Check corners (bitmasks[row, col] = visual position (col, row))
        AssertThat(bitmasks[0, 0]).IsEqual(2);  // Visual (0,0): Only SE in bounds
        AssertThat(bitmasks[0, 2]).IsEqual(4);  // Visual (2,0): Only SW in bounds
        AssertThat(bitmasks[2, 0]).IsEqual(1);  // Visual (0,2): Only NE in bounds
        AssertThat(bitmasks[2, 2]).IsEqual(8);  // Visual (2,2): Only NW in bounds
    }

    [TestCase]
    public void TestComputeAllBitmasks_EmptyGrid()
    {
        var dataGrid = new[,]
        {
            { false, false },
            { false, false }
        };

        var bitmasks = DualGridAutoTile.ComputeAllBitmasks(dataGrid);

        // All should be 0
        for (var y = 0; y < bitmasks.GetLength(0); y++)
        for (var x = 0; x < bitmasks.GetLength(1); x++)
            AssertThat(bitmasks[y, x]).IsEqual(0);
    }

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
