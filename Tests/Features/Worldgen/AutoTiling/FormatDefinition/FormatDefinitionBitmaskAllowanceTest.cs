using System.Collections.Generic;
using System.Linq;
using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.FormatDefinition;

/// <summary>
///     FormatDefinitionBitmaskAllowanceTest scenarios split out of AutoTileFormatDefinitionTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FormatDefinitionBitmaskAllowanceTest : AutoTileFormatDefinitionTestBase
{
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
}
