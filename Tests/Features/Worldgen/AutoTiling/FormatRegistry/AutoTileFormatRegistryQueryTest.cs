using System.Collections.Generic;
using CardCleaner.Features.Worldgen.AutoTiling;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.FormatRegistry;

/// <summary>
///     AutoTileFormatRegistryQueryTest scenarios split out of AutoTileFormatRegistryTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AutoTileFormatRegistryQueryTest : AutoTileFormatRegistryTestBase
{
    // ==================== TryGet ====================

    /// <summary>
    /// Test TryGet returns false and null for missing format.
    /// </summary>
    [TestCase]
    public void TestTryGet_ReturnsFalseForMissing()
    {
        var found = AutoTileFormatRegistry.TryGet("nonexistent_format", out var format);

        AssertBool(found).IsFalse();
        AssertThat(format).IsNull();
    }

    /// <summary>
    /// Test TryGet returns true and format for existing.
    /// </summary>
    [TestCase]
    public void TestTryGet_ReturnsTrueForExisting()
    {
        var found = AutoTileFormatRegistry.TryGet("edge16", out var format);

        AssertBool(found).IsTrue();
        AssertThat(format).IsNotNull();
        AssertString(format!.Name).IsEqual("edge16");
    }

    /// <summary>
    /// Test TryGet works for custom format.
    /// </summary>
    [TestCase]
    public void TestTryGet_WorksForCustomFormat()
    {
        AutoTileFormatRegistry.Register(CreateCustomFormat("tryget_custom"));

        var found = AutoTileFormatRegistry.TryGet("tryget_custom", out var format);

        AssertBool(found).IsTrue();
        AssertString(format!.Name).IsEqual("tryget_custom");
    }

    // ==================== Get ====================

    /// <summary>
    /// Test Get returns format for existing name.
    /// </summary>
    [TestCase]
    public void TestGet_ReturnsFormatForExisting()
    {
        var format = AutoTileFormatRegistry.Get("corner16");

        AssertString(format.Name).IsEqual("corner16");
    }

    /// <summary>
    /// Test Get throws KeyNotFoundException for missing name.
    /// </summary>
    [TestCase]
    public void TestGet_ThrowsForMissing()
    {
        AssertThrown(() => AutoTileFormatRegistry.Get("definitely_not_a_format"))
            .IsInstanceOf<KeyNotFoundException>();
    }

    /// <summary>
    /// Test Get throws with descriptive message.
    /// </summary>
    [TestCase]
    public void TestGet_ThrowsWithDescriptiveMessage()
    {
        try
        {
            AutoTileFormatRegistry.Get("missing_format_xyz");
            AssertBool(false).IsTrue(); // Should not reach here
        }
        catch (KeyNotFoundException ex)
        {
            AssertString(ex.Message).Contains("missing_format_xyz");
        }
    }

    // ==================== Contains ====================

    /// <summary>
    /// Test Contains returns true for built-in format.
    /// </summary>
    [TestCase]
    public void TestContains_TrueForBuiltIn()
    {
        AssertBool(AutoTileFormatRegistry.Contains("blob47")).IsTrue();
    }

    /// <summary>
    /// Test Contains returns false for non-existent format.
    /// </summary>
    [TestCase]
    public void TestContains_FalseForNonExistent()
    {
        AssertBool(AutoTileFormatRegistry.Contains("not_a_format")).IsFalse();
    }

    /// <summary>
    /// Test Contains returns true for registered custom format.
    /// </summary>
    [TestCase]
    public void TestContains_TrueForCustom()
    {
        AutoTileFormatRegistry.Register(CreateCustomFormat("contains_custom"));

        AssertBool(AutoTileFormatRegistry.Contains("contains_custom")).IsTrue();
    }

    /// <summary>
    /// Test Contains is case-insensitive.
    /// </summary>
    [TestCase]
    public void TestContains_CaseInsensitive()
    {
        AssertBool(AutoTileFormatRegistry.Contains("EDGE16")).IsTrue();
        AssertBool(AutoTileFormatRegistry.Contains("Edge16")).IsTrue();
        AssertBool(AutoTileFormatRegistry.Contains("edge16")).IsTrue();
    }
}
