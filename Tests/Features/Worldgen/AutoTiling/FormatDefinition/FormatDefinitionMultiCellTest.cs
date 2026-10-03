using System.Collections.Generic;
using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.FormatDefinition;

/// <summary>
///     FormatDefinitionMultiCellTest scenarios split out of AutoTileFormatDefinitionTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FormatDefinitionMultiCellTest : AutoTileFormatDefinitionTestBase
{
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
}
