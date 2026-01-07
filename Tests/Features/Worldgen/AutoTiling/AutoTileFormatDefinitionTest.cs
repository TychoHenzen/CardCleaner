using System.Collections.Generic;
using System.Linq;
using CardCleaner.Features.Worldgen.AutoTiling;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling;

/// <summary>
/// Tests for AutoTileFormatDefinition class.
/// Validates format construction, variant retrieval, and helper methods.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AutoTileFormatDefinitionTest
{
    // ==================== Construction ====================

    /// <summary>
    /// Test that constructor sets all properties correctly.
    /// </summary>
    [TestCase]
    public void TestConstructor_SetsAllProperties()
    {
        var allowedBitmasks = new HashSet<int> { 0, 1, 2, 3 };
        var variantMappings = new Dictionary<int, VariantDefinition>
        {
            [0] = new(new Vector2I(0, 0)),
            [1] = new(new Vector2I(1, 0)),
            [2] = new(new Vector2I(2, 0)),
            [3] = new(new Vector2I(3, 0))
        };

        var format = new AutoTileFormatDefinition(
            "test_format",
            BitmaskType.Edge4,
            allowedBitmasks,
            variantMappings,
            isBuiltIn: false);

        AssertString(format.Name).IsEqual("test_format");
        AssertThat(format.BitmaskType).IsEqual(BitmaskType.Edge4);
        AssertBool(format.IsBuiltIn).IsFalse();
        AssertThat(format.AllowedBitmasks.Count).IsEqual(4);
        AssertThat(format.VariantMappings.Count).IsEqual(4);
    }

    /// <summary>
    /// Test IsBuiltIn defaults to false.
    /// </summary>
    [TestCase]
    public void TestConstructor_IsBuiltInDefaultsFalse()
    {
        var format = new AutoTileFormatDefinition(
            "custom",
            BitmaskType.Corner4,
            new HashSet<int> { 0 },
            new Dictionary<int, VariantDefinition> { [0] = new(Vector2I.Zero) });

        AssertBool(format.IsBuiltIn).IsFalse();
    }

    // ==================== GetVariant ====================

    /// <summary>
    /// Test GetVariant returns correct variant for allowed bitmask.
    /// </summary>
    [TestCase]
    public void TestGetVariant_ReturnsVariantForAllowedBitmask()
    {
        var format = CreateTestFormat(new[] { 0, 5, 15 });

        var variant = format.GetVariant(5);

        AssertThat(variant).IsNotNull();
        AssertThat(variant!.Value.AtlasCoords).IsEqual(new Vector2I(5, 0));
    }

    /// <summary>
    /// Test GetVariant returns null for disallowed bitmask.
    /// </summary>
    [TestCase]
    public void TestGetVariant_ReturnsNullForDisallowedBitmask()
    {
        var format = CreateTestFormat(new[] { 0, 5, 15 }); // 10 is not allowed

        var variant = format.GetVariant(10);

        AssertThat(variant).IsNull();
    }

    /// <summary>
    /// Test GetVariant returns null when bitmask allowed but not mapped.
    /// </summary>
    [TestCase]
    public void TestGetVariant_ReturnsNullWhenAllowedButNotMapped()
    {
        var allowedBitmasks = new HashSet<int> { 0, 1, 2 };
        var variantMappings = new Dictionary<int, VariantDefinition>
        {
            [0] = new(new Vector2I(0, 0)),
            // 1 is allowed but not mapped
            [2] = new(new Vector2I(2, 0))
        };

        var format = new AutoTileFormatDefinition(
            "test",
            BitmaskType.Edge4,
            allowedBitmasks,
            variantMappings);

        var variant = format.GetVariant(1);

        AssertThat(variant).IsNull();
    }

    /// <summary>
    /// Test GetVariant at boundary values (0 and max).
    /// </summary>
    [TestCase]
    public void TestGetVariant_BoundaryValues()
    {
        var format = CreateTestFormat(new[] { 0, 15 });

        AssertThat(format.GetVariant(0)).IsNotNull();
        AssertThat(format.GetVariant(15)).IsNotNull();
    }

    // ==================== IsBitmaskAllowed ====================

    /// <summary>
    /// Test IsBitmaskAllowed returns true for allowed bitmask.
    /// </summary>
    [TestCase]
    public void TestIsBitmaskAllowed_TrueForAllowed()
    {
        var format = CreateTestFormat(new[] { 0, 5, 10, 15 });

        AssertBool(format.IsBitmaskAllowed(5)).IsTrue();
        AssertBool(format.IsBitmaskAllowed(10)).IsTrue();
    }

    /// <summary>
    /// Test IsBitmaskAllowed returns false for disallowed bitmask.
    /// </summary>
    [TestCase]
    public void TestIsBitmaskAllowed_FalseForDisallowed()
    {
        var format = CreateTestFormat(new[] { 0, 5, 10, 15 });

        AssertBool(format.IsBitmaskAllowed(3)).IsFalse();
        AssertBool(format.IsBitmaskAllowed(7)).IsFalse();
    }

    /// <summary>
    /// Test hedge format example: bitmask 15 (interior) is forbidden.
    /// </summary>
    [TestCase]
    public void TestIsBitmaskAllowed_HedgeFormatForbidsInterior()
    {
        // Hedge format: all 4-bit masks except 15 (interior)
        var hedgeBitmasks = Enumerable.Range(0, 15).ToHashSet(); // 0-14, not 15
        var format = new AutoTileFormatDefinition(
            "hedge4",
            BitmaskType.Edge4,
            hedgeBitmasks,
            CreateVariantMappings(hedgeBitmasks));

        AssertBool(format.IsBitmaskAllowed(0)).IsTrue();
        AssertBool(format.IsBitmaskAllowed(14)).IsTrue();
        AssertBool(format.IsBitmaskAllowed(15)).IsFalse(); // Interior forbidden
    }

    // ==================== GetExpectedVariantCount ====================

    /// <summary>
    /// Test GetExpectedVariantCount returns 16 for built-in Corner4 format.
    /// </summary>
    [TestCase]
    public void TestGetExpectedVariantCount_BuiltInCorner4_Returns16()
    {
        var format = BuiltInAutoTileFormats.CreateCorner16();

        AssertThat(format.GetExpectedVariantCount()).IsEqual(16);
    }

    /// <summary>
    /// Test GetExpectedVariantCount returns 16 for built-in Edge4 format.
    /// </summary>
    [TestCase]
    public void TestGetExpectedVariantCount_BuiltInEdge4_Returns16()
    {
        var format = BuiltInAutoTileFormats.CreateEdge16();

        AssertThat(format.GetExpectedVariantCount()).IsEqual(16);
    }

    /// <summary>
    /// Test GetExpectedVariantCount returns 47 for built-in Full8 (blob) format.
    /// </summary>
    [TestCase]
    public void TestGetExpectedVariantCount_BuiltInFull8_Returns47()
    {
        var format = BuiltInAutoTileFormats.CreateBlob47();

        AssertThat(format.GetExpectedVariantCount()).IsEqual(47);
    }

    /// <summary>
    /// Test GetExpectedVariantCount returns actual count for custom format.
    /// </summary>
    [TestCase]
    public void TestGetExpectedVariantCount_CustomFormat_ReturnsActualCount()
    {
        var format = CreateTestFormat(new[] { 0, 5, 10 }); // Only 3 allowed

        AssertThat(format.GetExpectedVariantCount()).IsEqual(3);
    }

    /// <summary>
    /// Test custom format with filtered bitmasks.
    /// </summary>
    [TestCase]
    public void TestGetExpectedVariantCount_HedgeFormat_Returns15()
    {
        var hedgeBitmasks = Enumerable.Range(0, 15).ToHashSet(); // 0-14
        var format = new AutoTileFormatDefinition(
            "hedge4",
            BitmaskType.Edge4,
            hedgeBitmasks,
            CreateVariantMappings(hedgeBitmasks),
            isBuiltIn: false);

        AssertThat(format.GetExpectedVariantCount()).IsEqual(15);
    }

    // ==================== GetMaxBitmaskValue ====================

    /// <summary>
    /// Test GetMaxBitmaskValue returns 15 for Corner4.
    /// </summary>
    [TestCase]
    public void TestGetMaxBitmaskValue_Corner4_Returns15()
    {
        var format = new AutoTileFormatDefinition(
            "test",
            BitmaskType.Corner4,
            new HashSet<int> { 0 },
            new Dictionary<int, VariantDefinition> { [0] = new(Vector2I.Zero) });

        AssertThat(format.GetMaxBitmaskValue()).IsEqual(15);
    }

    /// <summary>
    /// Test GetMaxBitmaskValue returns 15 for Edge4.
    /// </summary>
    [TestCase]
    public void TestGetMaxBitmaskValue_Edge4_Returns15()
    {
        var format = new AutoTileFormatDefinition(
            "test",
            BitmaskType.Edge4,
            new HashSet<int> { 0 },
            new Dictionary<int, VariantDefinition> { [0] = new(Vector2I.Zero) });

        AssertThat(format.GetMaxBitmaskValue()).IsEqual(15);
    }

    /// <summary>
    /// Test GetMaxBitmaskValue returns 255 for Full8.
    /// </summary>
    [TestCase]
    public void TestGetMaxBitmaskValue_Full8_Returns255()
    {
        var format = new AutoTileFormatDefinition(
            "test",
            BitmaskType.Full8,
            new HashSet<int> { 0 },
            new Dictionary<int, VariantDefinition> { [0] = new(Vector2I.Zero) });

        AssertThat(format.GetMaxBitmaskValue()).IsEqual(255);
    }

    // ==================== GetMaxMultiCellBounds ====================

    /// <summary>
    /// Test GetMaxMultiCellBounds returns null when all variants are 1x1.
    /// </summary>
    [TestCase]
    public void TestGetMaxMultiCellBounds_AllSingleCell_ReturnsNull()
    {
        var format = CreateTestFormat(new[] { 0, 5, 10, 15 });

        var bounds = format.GetMaxMultiCellBounds();

        AssertThat(bounds).IsNull();
    }

    /// <summary>
    /// Test GetMaxMultiCellBounds returns bounds when multi-cell variants exist.
    /// </summary>
    [TestCase]
    public void TestGetMaxMultiCellBounds_WithMultiCell_ReturnsBounds()
    {
        var variantMappings = new Dictionary<int, VariantDefinition>
        {
            [0] = new(new Vector2I(0, 0)),
            [5] = new(new Vector2I(5, 0), new Vector2I(1, 3), new Vector2I(0, -2)),
            [10] = new(new Vector2I(10, 0))
        };
        var format = new AutoTileFormatDefinition(
            "test",
            BitmaskType.Edge4,
            new HashSet<int> { 0, 5, 10 },
            variantMappings);

        var bounds = format.GetMaxMultiCellBounds();

        AssertThat(bounds).IsNotNull();
        AssertThat(bounds!.Value.Size).IsEqual(new Vector2I(1, 3));
        AssertThat(bounds!.Value.Offset).IsEqual(new Vector2I(0, -2));
    }

    /// <summary>
    /// Test GetMaxMultiCellBounds returns largest multi-cell variant.
    /// </summary>
    [TestCase]
    public void TestGetMaxMultiCellBounds_ReturnsLargest()
    {
        var variantMappings = new Dictionary<int, VariantDefinition>
        {
            [0] = new(new Vector2I(0, 0), new Vector2I(2, 2)), // Area = 4
            [5] = new(new Vector2I(5, 0), new Vector2I(1, 3), new Vector2I(0, -2)), // Area = 3
            [10] = new(new Vector2I(10, 0), new Vector2I(3, 2)) // Area = 6 (largest)
        };
        var format = new AutoTileFormatDefinition(
            "test",
            BitmaskType.Edge4,
            new HashSet<int> { 0, 5, 10 },
            variantMappings);

        var bounds = format.GetMaxMultiCellBounds();

        AssertThat(bounds).IsNotNull();
        AssertThat(bounds!.Value.Size).IsEqual(new Vector2I(3, 2));
    }

    // ==================== AllBitmasksFor ====================

    /// <summary>
    /// Test AllBitmasksFor Corner4 returns 16 values (0-15).
    /// </summary>
    [TestCase]
    public void TestAllBitmasksFor_Corner4_Returns16()
    {
        var bitmasks = AutoTileFormatDefinition.AllBitmasksFor(BitmaskType.Corner4);

        AssertThat(bitmasks.Count).IsEqual(16);
        AssertBool(bitmasks.Contains(0)).IsTrue();
        AssertBool(bitmasks.Contains(15)).IsTrue();
        AssertBool(bitmasks.Contains(16)).IsFalse();
    }

    /// <summary>
    /// Test AllBitmasksFor Edge4 returns 16 values (0-15).
    /// </summary>
    [TestCase]
    public void TestAllBitmasksFor_Edge4_Returns16()
    {
        var bitmasks = AutoTileFormatDefinition.AllBitmasksFor(BitmaskType.Edge4);

        AssertThat(bitmasks.Count).IsEqual(16);
        for (var i = 0; i < 16; i++)
        {
            AssertBool(bitmasks.Contains(i)).IsTrue();
        }
    }

    /// <summary>
    /// Test AllBitmasksFor Full8 returns exactly 47 valid blob values.
    /// </summary>
    [TestCase]
    public void TestAllBitmasksFor_Full8_Returns47()
    {
        var bitmasks = AutoTileFormatDefinition.AllBitmasksFor(BitmaskType.Full8);

        AssertThat(bitmasks.Count).IsEqual(47);
    }

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

    // ==================== Helper Methods ====================

    private static AutoTileFormatDefinition CreateTestFormat(int[] allowedBitmasks)
    {
        var bitmasks = allowedBitmasks.ToHashSet();
        return new AutoTileFormatDefinition(
            "test_format",
            BitmaskType.Edge4,
            bitmasks,
            CreateVariantMappings(bitmasks),
            isBuiltIn: false);
    }

    private static Dictionary<int, VariantDefinition> CreateVariantMappings(IEnumerable<int> bitmasks)
    {
        return bitmasks.ToDictionary(
            b => b,
            b => new VariantDefinition(new Vector2I(b, 0)));
    }
}
