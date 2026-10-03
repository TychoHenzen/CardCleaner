using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using FsCheck;

namespace CardCleaner.Tests.Core.PropertyTesting.Generators;

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
