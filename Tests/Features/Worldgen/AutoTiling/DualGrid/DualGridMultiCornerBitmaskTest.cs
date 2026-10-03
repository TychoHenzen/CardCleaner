using CardCleaner.Scripts.Features.Worldgen.AutoTiling;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.DualGrid;

/// <summary>
///     DualGridMultiCornerBitmaskTest scenarios split out of DualGridAutoTileTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DualGridMultiCornerBitmaskTest
{
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
}
