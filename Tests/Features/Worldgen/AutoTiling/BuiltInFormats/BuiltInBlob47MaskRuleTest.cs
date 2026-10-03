using CardCleaner.Features.Worldgen.AutoTiling;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.BuiltInFormats;

/// <summary>
///     BuiltInBlob47MaskRuleTest scenarios split out of BuiltInAutoTileFormatsTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BuiltInBlob47MaskRuleTest
{
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
}
