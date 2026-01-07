using System.Linq;
using CardCleaner.Features.Worldgen.AutoTiling;
using GdUnit4;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling;

/// <summary>
/// Tests for BuiltInAutoTileFormats factory methods.
/// Validates that each built-in format is correctly configured.
/// </summary>
[TestSuite]
public class BuiltInAutoTileFormatsTest
{
    // ==================== CreateCorner16 ====================

    /// <summary>
    /// Test CreateCorner16 returns format with correct name.
    /// </summary>
    [TestCase]
    public void TestCreateCorner16_HasCorrectName()
    {
        var format = BuiltInAutoTileFormats.CreateCorner16();

        AssertString(format.Name).IsEqual("corner16");
    }

    /// <summary>
    /// Test CreateCorner16 uses Corner4 bitmask type.
    /// </summary>
    [TestCase]
    public void TestCreateCorner16_UsesCorner4BitmaskType()
    {
        var format = BuiltInAutoTileFormats.CreateCorner16();

        AssertThat(format.BitmaskType).IsEqual(BitmaskType.Corner4);
    }

    /// <summary>
    /// Test CreateCorner16 has exactly 16 allowed bitmasks.
    /// </summary>
    [TestCase]
    public void TestCreateCorner16_Has16AllowedBitmasks()
    {
        var format = BuiltInAutoTileFormats.CreateCorner16();

        AssertThat(format.AllowedBitmasks.Count).IsEqual(16);
    }

    /// <summary>
    /// Test CreateCorner16 allows bitmasks 0-15.
    /// </summary>
    [TestCase]
    public void TestCreateCorner16_AllowsBitmasks0To15()
    {
        var format = BuiltInAutoTileFormats.CreateCorner16();

        for (var i = 0; i < 16; i++)
        {
            AssertBool(format.IsBitmaskAllowed(i)).IsTrue();
        }

        AssertBool(format.IsBitmaskAllowed(16)).IsFalse();
    }

    /// <summary>
    /// Test CreateCorner16 is marked as built-in.
    /// </summary>
    [TestCase]
    public void TestCreateCorner16_IsBuiltIn()
    {
        var format = BuiltInAutoTileFormats.CreateCorner16();

        AssertBool(format.IsBuiltIn).IsTrue();
    }

    /// <summary>
    /// Test CreateCorner16 has 16 variant mappings.
    /// </summary>
    [TestCase]
    public void TestCreateCorner16_Has16VariantMappings()
    {
        var format = BuiltInAutoTileFormats.CreateCorner16();

        AssertThat(format.VariantMappings.Count).IsEqual(16);
    }

    // ==================== CreateEdge16 ====================

    /// <summary>
    /// Test CreateEdge16 returns format with correct name.
    /// </summary>
    [TestCase]
    public void TestCreateEdge16_HasCorrectName()
    {
        var format = BuiltInAutoTileFormats.CreateEdge16();

        AssertString(format.Name).IsEqual("edge16");
    }

    /// <summary>
    /// Test CreateEdge16 uses Edge4 bitmask type.
    /// </summary>
    [TestCase]
    public void TestCreateEdge16_UsesEdge4BitmaskType()
    {
        var format = BuiltInAutoTileFormats.CreateEdge16();

        AssertThat(format.BitmaskType).IsEqual(BitmaskType.Edge4);
    }

    /// <summary>
    /// Test CreateEdge16 has exactly 16 allowed bitmasks.
    /// </summary>
    [TestCase]
    public void TestCreateEdge16_Has16AllowedBitmasks()
    {
        var format = BuiltInAutoTileFormats.CreateEdge16();

        AssertThat(format.AllowedBitmasks.Count).IsEqual(16);
    }

    /// <summary>
    /// Test CreateEdge16 allows bitmasks 0-15.
    /// </summary>
    [TestCase]
    public void TestCreateEdge16_AllowsBitmasks0To15()
    {
        var format = BuiltInAutoTileFormats.CreateEdge16();

        for (var i = 0; i < 16; i++)
        {
            AssertBool(format.IsBitmaskAllowed(i)).IsTrue();
        }

        AssertBool(format.IsBitmaskAllowed(16)).IsFalse();
    }

    /// <summary>
    /// Test CreateEdge16 is marked as built-in.
    /// </summary>
    [TestCase]
    public void TestCreateEdge16_IsBuiltIn()
    {
        var format = BuiltInAutoTileFormats.CreateEdge16();

        AssertBool(format.IsBuiltIn).IsTrue();
    }

    /// <summary>
    /// Test CreateEdge16 has 16 variant mappings.
    /// </summary>
    [TestCase]
    public void TestCreateEdge16_Has16VariantMappings()
    {
        var format = BuiltInAutoTileFormats.CreateEdge16();

        AssertThat(format.VariantMappings.Count).IsEqual(16);
    }

    // ==================== CreateBlob47 ====================

    /// <summary>
    /// Test CreateBlob47 returns format with correct name.
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_HasCorrectName()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        AssertString(format.Name).IsEqual("blob47");
    }

    /// <summary>
    /// Test CreateBlob47 uses Full8 bitmask type.
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_UsesFull8BitmaskType()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        AssertThat(format.BitmaskType).IsEqual(BitmaskType.Full8);
    }

    /// <summary>
    /// Test CreateBlob47 has exactly 47 allowed bitmasks.
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_HasExactly47AllowedBitmasks()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        AssertThat(format.AllowedBitmasks.Count).IsEqual(47);
    }

    /// <summary>
    /// Test CreateBlob47 is marked as built-in.
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_IsBuiltIn()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        AssertBool(format.IsBuiltIn).IsTrue();
    }

    /// <summary>
    /// Test CreateBlob47 has 47 variant mappings.
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_Has47VariantMappings()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        AssertThat(format.VariantMappings.Count).IsEqual(47);
    }

    /// <summary>
    /// Test blob47 includes bitmask 0 (isolated tile).
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_IncludesIsolatedTile()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        AssertBool(format.IsBitmaskAllowed(0)).IsTrue();
    }

    /// <summary>
    /// Test blob47 includes bitmask 255 (fully surrounded).
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_IncludesFullySurrounded()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        // 255 = all 8 neighbors present (valid because all corners have their edges)
        AssertBool(format.IsBitmaskAllowed(255)).IsTrue();
    }

    /// <summary>
    /// Test blob47 excludes invalid corner-only masks.
    /// NE=2 alone is invalid (requires N=1 and E=4).
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_ExcludesInvalidNEOnly()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        AssertBool(format.IsBitmaskAllowed(2)).IsFalse(); // NE only
    }

    /// <summary>
    /// Test blob47 excludes invalid SE corner only.
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_ExcludesInvalidSEOnly()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        AssertBool(format.IsBitmaskAllowed(8)).IsFalse(); // SE only
    }

    /// <summary>
    /// Test blob47 excludes invalid SW corner only.
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_ExcludesInvalidSWOnly()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        AssertBool(format.IsBitmaskAllowed(32)).IsFalse(); // SW only
    }

    /// <summary>
    /// Test blob47 excludes invalid NW corner only.
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_ExcludesInvalidNWOnly()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        AssertBool(format.IsBitmaskAllowed(128)).IsFalse(); // NW only
    }

    /// <summary>
    /// Test blob47 allows valid edge-only masks.
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_AllowsEdgeOnlyMasks()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        // N=1, E=4, S=16, W=64 are all valid alone
        AssertBool(format.IsBitmaskAllowed(1)).IsTrue();  // N only
        AssertBool(format.IsBitmaskAllowed(4)).IsTrue();  // E only
        AssertBool(format.IsBitmaskAllowed(16)).IsTrue(); // S only
        AssertBool(format.IsBitmaskAllowed(64)).IsTrue(); // W only
    }

    /// <summary>
    /// Test blob47 allows valid corner with adjacent edges.
    /// NE (2) + N (1) + E (4) = 7 is valid.
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_AllowsValidCornerWithEdges()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        // N + NE + E = 1 + 2 + 4 = 7
        AssertBool(format.IsBitmaskAllowed(7)).IsTrue();

        // E + SE + S = 4 + 8 + 16 = 28
        AssertBool(format.IsBitmaskAllowed(28)).IsTrue();

        // S + SW + W = 16 + 32 + 64 = 112
        AssertBool(format.IsBitmaskAllowed(112)).IsTrue();

        // W + NW + N = 64 + 128 + 1 = 193
        AssertBool(format.IsBitmaskAllowed(193)).IsTrue();
    }

    /// <summary>
    /// Test all blob47 masks satisfy corner-requires-edges rule.
    /// </summary>
    [TestCase]
    public void TestCreateBlob47_AllMasksSatisfyCornerEdgeRule()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        const int N = 1, NE = 2, E = 4, SE = 8, S = 16, SW = 32, W = 64, NW = 128;

        foreach (var mask in format.AllowedBitmasks)
        {
            // NE requires N and E
            if ((mask & NE) != 0)
            {
                AssertBool((mask & N) != 0).IsTrue();
                AssertBool((mask & E) != 0).IsTrue();
            }

            // SE requires E and S
            if ((mask & SE) != 0)
            {
                AssertBool((mask & E) != 0).IsTrue();
                AssertBool((mask & S) != 0).IsTrue();
            }

            // SW requires S and W
            if ((mask & SW) != 0)
            {
                AssertBool((mask & S) != 0).IsTrue();
                AssertBool((mask & W) != 0).IsTrue();
            }

            // NW requires W and N
            if ((mask & NW) != 0)
            {
                AssertBool((mask & W) != 0).IsTrue();
                AssertBool((mask & N) != 0).IsTrue();
            }
        }
    }

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
