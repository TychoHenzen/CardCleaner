using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Tests.Core.PropertyTesting;
using CardCleaner.Tests.Core.PropertyTesting.Generators;
using FsCheck;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Properties;

/// <summary>
///     Property-based tests for auto-tiling bitmask computation.
///     Validates Corner16, Edge16, and Blob47 formats.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class AutoTilingProperties : PropertyTestBase
{
    [BeforeTest]
    public new void SetupPropertyTest()
    {
        base.SetupPropertyTest();
    }

    #region Corner16 Properties

    [TestCase]
    public void Corner16_BitmaskIsInValidRange()
    {
        Property(p => p
            .ForAll(
                Arb.From(NeighborBitmaskArbitrary.Corner16),
                expectedMask =>
                {
                    var predicate = CreatePredicateFromBitmask(expectedMask, NeighborBitmaskCorner.Directions);
                    var computed = NeighborBitmaskCorner.Compute(Vector2I.Zero, predicate);
                    return computed >= 0 && computed <= 15;
                })
            .Iterations(100));
    }

    [TestCase]
    public void Corner16_ComputeMatchesExpectedBitmask()
    {
        Property(p => p
            .ForAll(
                Arb.From(NeighborBitmaskArbitrary.Corner16),
                expectedMask =>
                {
                    var predicate = CreatePredicateFromBitmask(expectedMask, NeighborBitmaskCorner.Directions);
                    var computed = NeighborBitmaskCorner.Compute(Vector2I.Zero, predicate);
                    return computed == expectedMask;
                })
            .Iterations(100));
    }

    [TestCase]
    public void Corner16_IsolatedTileHasZeroMask()
    {
        Property(p => p
            .ForAll(
                Arb.From(TilePositionArbitrary.Small),
                position =>
                {
                    var mask = NeighborBitmaskCorner.Compute(position, _ => false);
                    return mask == 0;
                })
            .Iterations(50));
    }

    [TestCase]
    public void Corner16_SurroundedTileHasMaxMask()
    {
        Property(p => p
            .ForAll(
                Arb.From(TilePositionArbitrary.Small),
                position =>
                {
                    var mask = NeighborBitmaskCorner.Compute(position, _ => true);
                    return mask == 15;
                })
            .Iterations(50));
    }

    [TestCase]
    public void Corner16_IsDeterministic()
    {
        var combinedGen =
            from mask in NeighborBitmaskArbitrary.Corner16
            from pos in TilePositionArbitrary.Small
            select (Mask: mask, Pos: pos);

        Property(p => p
            .ForAll(
                Arb.From(combinedGen),
                args =>
                {
                    var predicate = CreatePredicateFromBitmask(args.Mask, NeighborBitmaskCorner.Directions);
                    var mask1 = NeighborBitmaskCorner.Compute(args.Pos, predicate);
                    var mask2 = NeighborBitmaskCorner.Compute(args.Pos, predicate);
                    return mask1 == mask2;
                })
            .Iterations(100));
    }

    #endregion

    #region Edge16 Properties

    [TestCase]
    public void Edge16_BitmaskIsInValidRange()
    {
        Property(p => p
            .ForAll(
                Arb.From(NeighborBitmaskArbitrary.Edge16),
                expectedMask =>
                {
                    var predicate = CreatePredicateFromBitmask(expectedMask, NeighborBitmask.Directions);
                    var computed = NeighborBitmask.Compute(Vector2I.Zero, predicate);
                    return computed >= 0 && computed <= 15;
                })
            .Iterations(100));
    }

    [TestCase]
    public void Edge16_ComputeMatchesExpectedBitmask()
    {
        Property(p => p
            .ForAll(
                Arb.From(NeighborBitmaskArbitrary.Edge16),
                expectedMask =>
                {
                    var predicate = CreatePredicateFromBitmask(expectedMask, NeighborBitmask.Directions);
                    var computed = NeighborBitmask.Compute(Vector2I.Zero, predicate);
                    return computed == expectedMask;
                })
            .Iterations(100));
    }

    #endregion

    #region Blob47 Properties

    [TestCase]
    public void Blob47_RawComputeIsInValidRange()
    {
        Property(p => p
            .ForAll(
                Arb.From(NeighborBitmaskArbitrary.Blob47Raw),
                expectedMask =>
                {
                    var predicate = CreatePredicateFromBitmask(expectedMask, NeighborBitmask8.Directions);
                    var computed = NeighborBitmask8.ComputeRaw(Vector2I.Zero, predicate);
                    return computed >= 0 && computed <= 255;
                })
            .Iterations(100));
    }

    [TestCase]
    public void Blob47_NormalizationProducesValidMask()
    {
        Property(p => p
            .ForAll(
                Arb.From(NeighborBitmaskArbitrary.Blob47Raw),
                rawMask =>
                {
                    var normalized = NeighborBitmask8.NormalizeToBlobMask(rawMask);
                    return NeighborBitmask8.IsValidBlobMask(normalized);
                })
            .Iterations(200));
    }

    [TestCase]
    public void Blob47_NormalizationIsIdempotent()
    {
        Property(p => p
            .ForAll(
                Arb.From(NeighborBitmaskArbitrary.Blob47Raw),
                rawMask =>
                {
                    var normalized1 = NeighborBitmask8.NormalizeToBlobMask(rawMask);
                    var normalized2 = NeighborBitmask8.NormalizeToBlobMask(normalized1);
                    return normalized1 == normalized2;
                })
            .Iterations(200));
    }

    [TestCase]
    public void Blob47_ValidMasksArePreservedByNormalization()
    {
        Property(p => p
            .ForAll(
                Arb.From(NeighborBitmaskArbitrary.Blob47),
                validMask =>
                {
                    var normalized = NeighborBitmask8.NormalizeToBlobMask(validMask);
                    return normalized == validMask;
                })
            .Iterations(100));
    }

    [TestCase]
    public void Blob47_CornerRequiresBothAdjacentEdges()
    {
        Property(p => p
            .ForAll(
                Arb.From(NeighborBitmaskArbitrary.Blob47),
                mask => CornersHaveAdjacentEdges(mask))
            .Iterations(100));
    }

    [TestCase]
    public void Blob47_Exactly47ValidMasksExist()
    {
        Property(p => p
            .ForAll(
                Arb.From(Gen.Constant(0)),
                _ =>
                {
                    var validMasks = NeighborBitmask8.GetValid47Masks();
                    return validMasks.Count == 47;
                })
            .Iterations(1));
    }

    [TestCase]
    public void Blob47_IsolatedTileHasZeroMask()
    {
        Property(p => p
            .ForAll(
                Arb.From(TilePositionArbitrary.Small),
                position =>
                {
                    var mask = NeighborBitmask8.Compute(position, _ => false);
                    return mask == 0;
                })
            .Iterations(50));
    }

    [TestCase]
    public void Blob47_SurroundedTileHasMaxMask()
    {
        Property(p => p
            .ForAll(
                Arb.From(TilePositionArbitrary.Small),
                position =>
                {
                    var mask = NeighborBitmask8.Compute(position, _ => true);
                    return mask == 255; // All 8 neighbors = 0xFF
                })
            .Iterations(50));
    }

    #endregion

    #region Helper Methods

    private static bool CornersHaveAdjacentEdges(int mask)
    {
        var north = NeighborBitmask8.HasNorth(mask);
        var east = NeighborBitmask8.HasEast(mask);
        var south = NeighborBitmask8.HasSouth(mask);
        var west = NeighborBitmask8.HasWest(mask);

        // NE needs N+E, SE needs E+S, SW needs S+W, NW needs W+N
        return CornerRequiresEdges(NeighborBitmask8.HasNorthEast(mask), north, east)
               && CornerRequiresEdges(NeighborBitmask8.HasSouthEast(mask), east, south)
               && CornerRequiresEdges(NeighborBitmask8.HasSouthWest(mask), south, west)
               && CornerRequiresEdges(NeighborBitmask8.HasNorthWest(mask), west, north);
    }

    private static bool CornerRequiresEdges(bool hasCorner, bool firstEdge, bool secondEdge)
    {
        return !hasCorner || (firstEdge && secondEdge);
    }

    private static System.Func<Vector2I, bool> CreatePredicateFromBitmask(int bitmask, Vector2I[] directions)
    {
        var matchingPositions = new HashSet<Vector2I>();
        for (var i = 0; i < directions.Length; i++)
        {
            if ((bitmask & (1 << i)) != 0)
            {
                matchingPositions.Add(Vector2I.Zero + directions[i]);
            }
        }

        return pos => matchingPositions.Contains(pos);
    }

    #endregion
}
