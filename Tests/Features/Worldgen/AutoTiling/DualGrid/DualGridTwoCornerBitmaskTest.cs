using CardCleaner.Scripts.Features.Worldgen.AutoTiling;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.DualGrid;

/// <summary>
///     DualGridTwoCornerBitmaskTest scenarios split out of DualGridAutoTileTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DualGridTwoCornerBitmaskTest
{
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
}
