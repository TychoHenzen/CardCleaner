using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.DualGrid;

/// <summary>
///     DualGridBoundaryAndSizeTest scenarios split out of DualGridAutoTileTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DualGridBoundaryAndSizeTest
{
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
}
