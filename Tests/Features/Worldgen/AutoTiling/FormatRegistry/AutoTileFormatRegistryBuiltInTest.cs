using System.Linq;
using CardCleaner.Features.Worldgen.AutoTiling;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.FormatRegistry;

/// <summary>
///     AutoTileFormatRegistryBuiltInTest scenarios split out of AutoTileFormatRegistryTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AutoTileFormatRegistryBuiltInTest : AutoTileFormatRegistryTestBase
{
    // ==================== Built-In Registration ====================

    /// <summary>
    /// Test that corner16 built-in format is auto-registered.
    /// </summary>
    [TestCase]
    public void TestBuiltIn_Corner16_AutoRegistered()
    {
        var found = AutoTileFormatRegistry.TryGet("corner16", out var format);

        AssertBool(found).IsTrue();
        AssertThat(format).IsNotNull();
        AssertString(format!.Name).IsEqual("corner16");
        AssertBool(format.IsBuiltIn).IsTrue();
    }

    /// <summary>
    /// Test that edge16 built-in format is auto-registered.
    /// </summary>
    [TestCase]
    public void TestBuiltIn_Edge16_AutoRegistered()
    {
        var found = AutoTileFormatRegistry.TryGet("edge16", out var format);

        AssertBool(found).IsTrue();
        AssertThat(format).IsNotNull();
        AssertString(format!.Name).IsEqual("edge16");
        AssertBool(format.IsBuiltIn).IsTrue();
    }

    /// <summary>
    /// Test that blob47 built-in format is auto-registered.
    /// </summary>
    [TestCase]
    public void TestBuiltIn_Blob47_AutoRegistered()
    {
        var found = AutoTileFormatRegistry.TryGet("blob47", out var format);

        AssertBool(found).IsTrue();
        AssertThat(format).IsNotNull();
        AssertString(format!.Name).IsEqual("blob47");
        AssertBool(format.IsBuiltIn).IsTrue();
    }

    /// <summary>
    /// Test GetAll returns at least 3 built-in formats.
    /// </summary>
    [TestCase]
    public void TestGetAll_ReturnsAtLeast3BuiltIns()
    {
        var formats = AutoTileFormatRegistry.GetAll();

        AssertThat(formats.Count).IsGreaterEqual(3);

        var names = formats.Select(f => f.Name.ToLowerInvariant()).ToHashSet();
        AssertBool(names.Contains("corner16")).IsTrue();
        AssertBool(names.Contains("edge16")).IsTrue();
        AssertBool(names.Contains("blob47")).IsTrue();
    }

    // ==================== Case-Insensitive Lookup ====================

    /// <summary>
    /// Test case-insensitive lookup: lowercase.
    /// </summary>
    [TestCase]
    public void TestCaseInsensitive_Lowercase()
    {
        var found = AutoTileFormatRegistry.TryGet("corner16", out var format);

        AssertBool(found).IsTrue();
        AssertThat(format).IsNotNull();
    }

    /// <summary>
    /// Test case-insensitive lookup: uppercase.
    /// </summary>
    [TestCase]
    public void TestCaseInsensitive_Uppercase()
    {
        var found = AutoTileFormatRegistry.TryGet("CORNER16", out var format);

        AssertBool(found).IsTrue();
        AssertThat(format).IsNotNull();
    }

    /// <summary>
    /// Test case-insensitive lookup: mixed case.
    /// </summary>
    [TestCase]
    public void TestCaseInsensitive_MixedCase()
    {
        var found = AutoTileFormatRegistry.TryGet("Corner16", out var format);

        AssertBool(found).IsTrue();
        AssertThat(format).IsNotNull();
    }

    /// <summary>
    /// Test case-insensitive lookup: BLOB47.
    /// </summary>
    [TestCase]
    public void TestCaseInsensitive_Blob47()
    {
        var found = AutoTileFormatRegistry.TryGet("BLOB47", out var format);

        AssertBool(found).IsTrue();
        AssertString(format!.Name).IsEqual("blob47");
    }
}
