using CardCleaner.Scripts.Features.Worldgen.AutoTiling;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.DualGrid;

/// <summary>
///     DualGridSingleCornerBitmaskTest scenarios split out of DualGridAutoTileTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DualGridSingleCornerBitmaskTest
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
}
