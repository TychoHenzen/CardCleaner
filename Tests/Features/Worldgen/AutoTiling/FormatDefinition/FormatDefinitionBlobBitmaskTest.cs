using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.FormatDefinition;

/// <summary>
///     FormatDefinitionBlobBitmaskTest scenarios split out of AutoTileFormatDefinitionTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FormatDefinitionBlobBitmaskTest : AutoTileFormatDefinitionTestBase
{
    // ==================== Blob Validation ====================

    /// <summary>
    /// Test that blob bitmasks satisfy corner-requires-adjacent-edges rule.
    /// NE corner (bit 2) requires N (bit 1) and E (bit 4).
    /// </summary>
    [TestCase]
    public void TestBlobBitmasks_NECornerRequiresEdges()
    {
        var bitmasks = AutoTileFormatDefinition.AllBitmasksFor(BitmaskType.Full8);

        const int NE = 2, N = 1, E = 4;

        foreach (var mask in bitmasks)
        {
            if ((mask & NE) != 0)
            {
                // If NE is set, both N and E must be set
                AssertBool((mask & N) != 0).IsTrue();
                AssertBool((mask & E) != 0).IsTrue();
            }
        }
    }

    /// <summary>
    /// Test that blob bitmasks satisfy corner-requires-adjacent-edges rule.
    /// SE corner (bit 8) requires E (bit 4) and S (bit 16).
    /// </summary>
    [TestCase]
    public void TestBlobBitmasks_SECornerRequiresEdges()
    {
        var bitmasks = AutoTileFormatDefinition.AllBitmasksFor(BitmaskType.Full8);

        const int SE = 8, E = 4, S = 16;

        foreach (var mask in bitmasks)
        {
            if ((mask & SE) != 0)
            {
                AssertBool((mask & E) != 0).IsTrue();
                AssertBool((mask & S) != 0).IsTrue();
            }
        }
    }

    /// <summary>
    /// Test that blob bitmasks satisfy corner-requires-adjacent-edges rule.
    /// SW corner (bit 32) requires S (bit 16) and W (bit 64).
    /// </summary>
    [TestCase]
    public void TestBlobBitmasks_SWCornerRequiresEdges()
    {
        var bitmasks = AutoTileFormatDefinition.AllBitmasksFor(BitmaskType.Full8);

        const int SW = 32, S = 16, W = 64;

        foreach (var mask in bitmasks)
        {
            if ((mask & SW) != 0)
            {
                AssertBool((mask & S) != 0).IsTrue();
                AssertBool((mask & W) != 0).IsTrue();
            }
        }
    }

    /// <summary>
    /// Test that blob bitmasks satisfy corner-requires-adjacent-edges rule.
    /// NW corner (bit 128) requires W (bit 64) and N (bit 1).
    /// </summary>
    [TestCase]
    public void TestBlobBitmasks_NWCornerRequiresEdges()
    {
        var bitmasks = AutoTileFormatDefinition.AllBitmasksFor(BitmaskType.Full8);

        const int NW = 128, W = 64, N = 1;

        foreach (var mask in bitmasks)
        {
            if ((mask & NW) != 0)
            {
                AssertBool((mask & W) != 0).IsTrue();
                AssertBool((mask & N) != 0).IsTrue();
            }
        }
    }

    /// <summary>
    /// Test specific known valid blob masks are included.
    /// </summary>
    [TestCase]
    public void TestBlobBitmasks_KnownValidMasksIncluded()
    {
        var bitmasks = AutoTileFormatDefinition.AllBitmasksFor(BitmaskType.Full8);

        // 0 = isolated (no neighbors)
        AssertBool(bitmasks.Contains(0)).IsTrue();

        // 1 = N only (edge only, no corners)
        AssertBool(bitmasks.Contains(1)).IsTrue();

        // 5 = N + E (two edges, no corners - valid)
        AssertBool(bitmasks.Contains(5)).IsTrue();

        // 7 = N + NE + E (N and E present, so NE is valid)
        AssertBool(bitmasks.Contains(7)).IsTrue();

        // 255 = all bits set (all edges and all corners valid)
        AssertBool(bitmasks.Contains(255)).IsTrue();
    }

    /// <summary>
    /// Test specific known invalid blob masks are excluded.
    /// </summary>
    [TestCase]
    public void TestBlobBitmasks_KnownInvalidMasksExcluded()
    {
        var bitmasks = AutoTileFormatDefinition.AllBitmasksFor(BitmaskType.Full8);

        // 2 = NE only (corner without required edges N and E)
        AssertBool(bitmasks.Contains(2)).IsFalse();

        // 8 = SE only (corner without required edges E and S)
        AssertBool(bitmasks.Contains(8)).IsFalse();

        // 32 = SW only (corner without required edges S and W)
        AssertBool(bitmasks.Contains(32)).IsFalse();

        // 128 = NW only (corner without required edges W and N)
        AssertBool(bitmasks.Contains(128)).IsFalse();

        // 3 = N + NE (NE requires E, but E is not set)
        AssertBool(bitmasks.Contains(3)).IsFalse();
    }

    // ==================== SimpleVariant Helper ====================

    /// <summary>
    /// Test SimpleVariant creates correct variant definition.
    /// </summary>
    [TestCase]
    public void TestSimpleVariant_CreatesCorrectDefinition()
    {
        var variant = AutoTileFormatDefinition.SimpleVariant(5, 10);

        AssertThat(variant.AtlasCoords).IsEqual(new Vector2I(5, 10));
        AssertThat(variant.Size).IsEqual(Vector2I.One);
        AssertThat(variant.Offset).IsEqual(Vector2I.Zero);
        AssertThat(variant.AtlasRegionSize).IsNull();
    }

    // ==================== Read-Only Collections ====================

    /// <summary>
    /// Test AllowedBitmasks returns read-only view.
    /// </summary>
    [TestCase]
    public void TestAllowedBitmasks_IsReadOnly()
    {
        var format = CreateTestFormat(new[] { 0, 5, 10 });

        var bitmasks = format.AllowedBitmasks;

        // IReadOnlySet doesn't have Add method, so this verifies it's read-only
        AssertThat(bitmasks.Count).IsEqual(3);
        AssertBool(bitmasks.Contains(5)).IsTrue();
    }

    /// <summary>
    /// Test VariantMappings returns read-only view.
    /// </summary>
    [TestCase]
    public void TestVariantMappings_IsReadOnly()
    {
        var format = CreateTestFormat(new[] { 0, 5, 10 });

        var mappings = format.VariantMappings;

        AssertThat(mappings.Count).IsEqual(3);
        AssertBool(mappings.ContainsKey(5)).IsTrue();
    }
}
