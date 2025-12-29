using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using FsCheck;

namespace CardCleaner.Tests.Core.PropertyTesting.Generators;

/// <summary>
///     FsCheck generators for neighbor bitmask values used in auto-tiling.
///     Provides format-aware generators that only produce valid values for each auto-tile format.
/// </summary>
public static class NeighborBitmaskArbitrary
{
    /// <summary>
    ///     Generator for Corner16 (4-bit diagonal) bitmasks in range [0, 15].
    /// </summary>
    public static Gen<int> Corner16 =>
        Gen.Choose(0, 15);

    /// <summary>
    ///     Generator for Edge16 (4-bit cardinal) bitmasks in range [0, 15].
    /// </summary>
    public static Gen<int> Edge16 =>
        Gen.Choose(0, 15);

    /// <summary>
    ///     Generator for valid Blob47 bitmasks (only the 47 valid values).
    /// </summary>
    public static Gen<int> Blob47 =>
        Gen.Elements(NeighborBitmask8.GetValid47Masks().ToArray());

    /// <summary>
    ///     Generator for raw 8-bit bitmasks [0, 255] - useful for testing normalization.
    /// </summary>
    public static Gen<int> Blob47Raw =>
        Gen.Choose(0, 255);

    public static Arbitrary<int> Corner16Arbitrary =>
        Arb.From(Corner16, ShrinkBitmask4);

    public static Arbitrary<int> Edge16Arbitrary =>
        Arb.From(Edge16, ShrinkBitmask4);

    public static Arbitrary<int> Blob47Arbitrary =>
        Arb.From(Blob47, ShrinkBlob47);

    private static IEnumerable<int> ShrinkBitmask4(int bitmask)
    {
        if (bitmask == 0) yield break;

        yield return 0;

        for (var i = 0; i < 4; i++)
        {
            var bit = 1 << i;
            if ((bitmask & bit) != 0)
                yield return bitmask & ~bit;
        }
    }

    private static IEnumerable<int> ShrinkBlob47(int bitmask)
    {
        if (bitmask == 0) yield break;

        var validMasks = new HashSet<int>(NeighborBitmask8.GetValid47Masks());

        yield return 0;

        for (var i = 0; i < 8; i++)
        {
            var bit = 1 << i;
            if ((bitmask & bit) != 0)
            {
                var candidate = NeighborBitmask8.NormalizeToBlobMask(bitmask & ~bit);
                if (candidate != bitmask && validMasks.Contains(candidate))
                    yield return candidate;
            }
        }
    }

    public static void Register() => Arb.Register<NeighborBitmaskArbitraryProvider>();

    private sealed class NeighborBitmaskArbitraryProvider
    {
        public static Arbitrary<int> Corner16Bitmask() => Corner16Arbitrary;
        public static Arbitrary<int> Edge16Bitmask() => Edge16Arbitrary;
        public static Arbitrary<int> Blob47Bitmask() => Blob47Arbitrary;
    }
}

/// <summary>
///     Helper generators for specific neighbor bitmask scenarios.
/// </summary>
public static class NeighborBitmaskGenerators
{
    public static Gen<int> Corner16WithBitCount(int count) =>
        Gen.Elements(Enumerable.Range(0, 16).Where(m => CountSetBits(m) == count).ToArray());

    public static Gen<int> Edge16WithBitCount(int count) =>
        Corner16WithBitCount(count);

    public static Gen<int> Blob47WithEdgeCount(int count) =>
        Gen.Elements(NeighborBitmask8.GetValid47Masks()
            .Where(m => CountSetBits(m & NeighborBitmask8.AllEdges) == count)
            .ToArray());

    private static int CountSetBits(int mask)
    {
        var count = 0;
        while (mask != 0)
        {
            count += mask & 1;
            mask >>= 1;
        }
        return count;
    }
}
