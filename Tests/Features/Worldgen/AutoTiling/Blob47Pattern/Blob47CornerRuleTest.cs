using CardCleaner.Scripts.Features.Worldgen.AutoTiling;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.Blob47Pattern;

/// <summary>
///     Blob47CornerRuleTest scenarios split out of Blob47PatternValidationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Blob47CornerRuleTest
{
    // ==================== Valid Mask Count ====================

    [TestCase]
    public void TestExactly47ValidMasks()
    {
        var validMasks = NeighborBitmask8.GetValid47Masks();
        AssertThat(validMasks.Count).IsEqual(47);
    }

    // ==================== Corner Constraint Validation ====================

    [TestCase]
    public void TestNorthEastCornerRequiresNorthAndEast()
    {
        // NE corner alone is invalid
        var neOnly = NeighborBitmask8.NorthEast;
        AssertBool(NeighborBitmask8.IsValidBlobMask(neOnly)).IsFalse();

        // NE with N but not E is invalid
        var neN = NeighborBitmask8.NorthEast | NeighborBitmask8.North;
        AssertBool(NeighborBitmask8.IsValidBlobMask(neN)).IsFalse();

        // NE with E but not N is invalid
        var neE = NeighborBitmask8.NorthEast | NeighborBitmask8.East;
        AssertBool(NeighborBitmask8.IsValidBlobMask(neE)).IsFalse();

        // NE with both N and E is valid
        var neNE = NeighborBitmask8.NorthEast | NeighborBitmask8.North | NeighborBitmask8.East;
        AssertBool(NeighborBitmask8.IsValidBlobMask(neNE)).IsTrue();
    }

    [TestCase]
    public void TestSouthEastCornerRequiresSouthAndEast()
    {
        var seOnly = NeighborBitmask8.SouthEast;
        AssertBool(NeighborBitmask8.IsValidBlobMask(seOnly)).IsFalse();

        var seSE = NeighborBitmask8.SouthEast | NeighborBitmask8.South | NeighborBitmask8.East;
        AssertBool(NeighborBitmask8.IsValidBlobMask(seSE)).IsTrue();
    }

    [TestCase]
    public void TestSouthWestCornerRequiresSouthAndWest()
    {
        var swOnly = NeighborBitmask8.SouthWest;
        AssertBool(NeighborBitmask8.IsValidBlobMask(swOnly)).IsFalse();

        var swSW = NeighborBitmask8.SouthWest | NeighborBitmask8.South | NeighborBitmask8.West;
        AssertBool(NeighborBitmask8.IsValidBlobMask(swSW)).IsTrue();
    }

    [TestCase]
    public void TestNorthWestCornerRequiresNorthAndWest()
    {
        var nwOnly = NeighborBitmask8.NorthWest;
        AssertBool(NeighborBitmask8.IsValidBlobMask(nwOnly)).IsFalse();

        var nwNW = NeighborBitmask8.NorthWest | NeighborBitmask8.North | NeighborBitmask8.West;
        AssertBool(NeighborBitmask8.IsValidBlobMask(nwNW)).IsTrue();
    }

    // ==================== Normalization ====================

    [TestCase]
    public void TestNormalizationClearsInvalidCorners()
    {
        // Raw mask with all corners but no edges
        var raw = NeighborBitmask8.AllCorners; // 170
        var normalized = NeighborBitmask8.NormalizeToBlobMask(raw);

        // All corners should be cleared since no edges
        AssertThat(normalized).IsEqual(0);
    }

    [TestCase]
    public void TestNormalizationPreservesValidCorners()
    {
        // Raw mask with N+E edges and NE corner
        var raw = NeighborBitmask8.North | NeighborBitmask8.East | NeighborBitmask8.NorthEast;
        var normalized = NeighborBitmask8.NormalizeToBlobMask(raw);

        // All bits should be preserved
        AssertThat(normalized).IsEqual(raw);
    }

    [TestCase]
    public void TestNormalizationClearsPartiallyValidCorners()
    {
        // N + NE (NE invalid because no E)
        var raw = NeighborBitmask8.North | NeighborBitmask8.NorthEast;
        var normalized = NeighborBitmask8.NormalizeToBlobMask(raw);

        // Only N should remain
        AssertThat(normalized).IsEqual(NeighborBitmask8.North);
    }

    [TestCase]
    public void TestNormalizationIsIdempotent()
    {
        // Normalizing a valid mask should return the same mask
        var validMasks = NeighborBitmask8.GetValid47Masks();

        foreach (var mask in validMasks)
        {
            var normalized = NeighborBitmask8.NormalizeToBlobMask(mask);
            AssertThat(normalized).IsEqual(mask);
        }
    }
}
