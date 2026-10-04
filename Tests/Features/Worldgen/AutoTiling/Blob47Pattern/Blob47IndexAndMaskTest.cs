using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.Blob47Pattern;

/// <summary>
///     Blob47IndexAndMaskTest scenarios split out of Blob47PatternValidationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Blob47IndexAndMaskTest
{
    // ==================== GetBlobIndex Tests ====================

    [TestCase]
    public void TestGetBlobIndexReturnsUniqueIndices()
    {
        var validMasks = NeighborBitmask8.GetValid47Masks();
        var indices = new HashSet<int>();

        foreach (var mask in validMasks)
        {
            var index = NeighborBitmask8.GetBlobIndex(mask);
            AssertThat(index).IsGreaterEqual(0);
            AssertThat(index).IsLess(47);
            AssertBool(indices.Add(index)).IsTrue(); // Should be unique
        }

        AssertThat(indices.Count).IsEqual(47);
    }

    [TestCase]
    public void TestGetBlobIndexReturnsNegativeForInvalid()
    {
        // Mask with NE corner but no N or E edges
        var invalidMask = NeighborBitmask8.NorthEast;
        var index = NeighborBitmask8.GetBlobIndex(invalidMask);

        AssertThat(index).IsEqual(-1);
    }

    [TestCase]
    public void TestGetBlobIndexZeroForEmptyMask()
    {
        var index = NeighborBitmask8.GetBlobIndex(0);
        AssertThat(index).IsEqual(0); // 0 is always the first valid mask
    }

    [TestCase]
    public void TestGetBlobIndex46ForFullMask()
    {
        var index = NeighborBitmask8.GetBlobIndex(NeighborBitmask8.All);
        AssertThat(index).IsEqual(46); // 255 (all bits) is the last valid mask
    }

    // ==================== Edge-Only Masks ====================

    [TestCase]
    public void TestAllEdgeOnlyMasksAreValid()
    {
        // All 16 combinations of edges (without corners) should be valid
        for (var i = 0; i < 16; i++)
        {
            var mask = 0;
            if ((i & 1) != 0) mask |= NeighborBitmask8.North;
            if ((i & 2) != 0) mask |= NeighborBitmask8.East;
            if ((i & 4) != 0) mask |= NeighborBitmask8.South;
            if ((i & 8) != 0) mask |= NeighborBitmask8.West;

            AssertBool(NeighborBitmask8.IsValidBlobMask(mask)).IsTrue();
        }
    }

    // ==================== Symmetry Tests ====================

    [TestCase]
    public void TestHorizontallySymmetricMasks()
    {
        // N alone vs S alone should both be valid
        AssertBool(NeighborBitmask8.IsValidBlobMask(NeighborBitmask8.North)).IsTrue();
        AssertBool(NeighborBitmask8.IsValidBlobMask(NeighborBitmask8.South)).IsTrue();

        // E alone vs W alone should both be valid
        AssertBool(NeighborBitmask8.IsValidBlobMask(NeighborBitmask8.East)).IsTrue();
        AssertBool(NeighborBitmask8.IsValidBlobMask(NeighborBitmask8.West)).IsTrue();
    }

    [TestCase]
    public void TestDiagonallyOppositeCornerMasks()
    {
        // NE with N+E vs SW with S+W should both be valid
        var neValid = NeighborBitmask8.NorthEast | NeighborBitmask8.North | NeighborBitmask8.East;
        var swValid = NeighborBitmask8.SouthWest | NeighborBitmask8.South | NeighborBitmask8.West;

        AssertBool(NeighborBitmask8.IsValidBlobMask(neValid)).IsTrue();
        AssertBool(NeighborBitmask8.IsValidBlobMask(swValid)).IsTrue();
    }

    // ==================== Common Patterns ====================

    [TestCase]
    public void TestSolidFillMaskIsValid()
    {
        // All edges + all corners = solid fill = 255
        var solidFill = NeighborBitmask8.All;
        AssertBool(NeighborBitmask8.IsValidBlobMask(solidFill)).IsTrue();
        AssertThat(NeighborBitmask8.GetBlobIndex(solidFill)).IsGreaterEqual(0);
    }

    [TestCase]
    public void TestIsolatedTileMaskIsValid()
    {
        // No neighbors = isolated tile = 0
        AssertBool(NeighborBitmask8.IsValidBlobMask(0)).IsTrue();
        AssertThat(NeighborBitmask8.GetBlobIndex(0)).IsEqual(0);
    }

    [TestCase]
    public void TestHorizontalStripMask()
    {
        // W + E (horizontal strip)
        var hStrip = NeighborBitmask8.West | NeighborBitmask8.East;
        AssertBool(NeighborBitmask8.IsValidBlobMask(hStrip)).IsTrue();
    }

    [TestCase]
    public void TestVerticalStripMask()
    {
        // N + S (vertical strip)
        var vStrip = NeighborBitmask8.North | NeighborBitmask8.South;
        AssertBool(NeighborBitmask8.IsValidBlobMask(vStrip)).IsTrue();
    }

    [TestCase]
    public void TestCornerPieceMask()
    {
        // Bottom-right corner: N + W + NW
        var blCorner = NeighborBitmask8.North | NeighborBitmask8.West | NeighborBitmask8.NorthWest;
        AssertBool(NeighborBitmask8.IsValidBlobMask(blCorner)).IsTrue();
    }
}
