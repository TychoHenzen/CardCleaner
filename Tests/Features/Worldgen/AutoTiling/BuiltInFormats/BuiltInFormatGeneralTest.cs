using System.Linq;
using CardCleaner.Features.Worldgen.AutoTiling;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.BuiltInFormats;

/// <summary>
///     BuiltInFormatGeneralTest scenarios split out of BuiltInAutoTileFormatsTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BuiltInFormatGeneralTest
{
    // ==================== Factory Consistency ====================

    /// <summary>
    /// Test each factory creates fresh instance.
    /// </summary>
    [TestCase]
    public void TestFactories_CreateFreshInstances()
    {
        var corner1 = BuiltInAutoTileFormats.CreateCorner16();
        var corner2 = BuiltInAutoTileFormats.CreateCorner16();

        // Should be different instances (not cached)
        AssertBool(ReferenceEquals(corner1, corner2)).IsFalse();
    }

    /// <summary>
    /// Test all built-in formats have correct expected variant counts.
    /// </summary>
    [TestCase]
    public void TestAllBuiltIns_HaveCorrectExpectedVariantCount()
    {
        var corner16 = BuiltInAutoTileFormats.CreateCorner16();
        var edge16 = BuiltInAutoTileFormats.CreateEdge16();
        var blob47 = BuiltInAutoTileFormats.CreateBlob47();

        AssertThat(corner16.GetExpectedVariantCount()).IsEqual(16);
        AssertThat(edge16.GetExpectedVariantCount()).IsEqual(16);
        AssertThat(blob47.GetExpectedVariantCount()).IsEqual(47);
    }

    /// <summary>
    /// Test variant mappings use sequential indices (for built-in placeholder coords).
    /// </summary>
    [TestCase]
    public void TestBlob47_VariantMappingsUseSequentialIndices()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        // Built-in formats use placeholder coords (index, 0)
        var indices = format.VariantMappings.Values
            .Select(v => v.AtlasCoords.X)
            .OrderBy(x => x)
            .ToList();

        // Should be 0, 1, 2, ... 46
        for (var i = 0; i < 47; i++)
        {
            AssertThat(indices[i]).IsEqual(i);
        }
    }

    // ==================== BitmaskType Enum Values ====================

    /// <summary>
    /// Test BitmaskType enum has expected values.
    /// </summary>
    [TestCase]
    public void TestBitmaskType_HasExpectedValues()
    {
        AssertThat((int)BitmaskType.Corner4).IsEqual(0);
        AssertThat((int)BitmaskType.Edge4).IsEqual(1);
        AssertThat((int)BitmaskType.Full8).IsEqual(2);
    }

    /// <summary>
    /// Test BitmaskType enum has exactly 3 values.
    /// </summary>
    [TestCase]
    public void TestBitmaskType_HasThreeValues()
    {
        var values = System.Enum.GetValues<BitmaskType>();

        AssertThat(values.Length).IsEqual(3);
    }
}
