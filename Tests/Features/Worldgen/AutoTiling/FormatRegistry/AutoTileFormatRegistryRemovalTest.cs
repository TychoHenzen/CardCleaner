using System;
using System.Linq;
using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.FormatRegistry;

/// <summary>
///     AutoTileFormatRegistryRemovalTest scenarios split out of AutoTileFormatRegistryTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AutoTileFormatRegistryRemovalTest : AutoTileFormatRegistryTestBase
{
    // ==================== Unregister ====================

    /// <summary>
    /// Test Unregister returns false for built-in formats.
    /// </summary>
    [TestCase]
    public void TestUnregister_ReturnsFalseForBuiltIn()
    {
        var result = AutoTileFormatRegistry.Unregister("corner16");

        AssertBool(result).IsFalse();
        AssertBool(AutoTileFormatRegistry.Contains("corner16")).IsTrue();
    }

    /// <summary>
    /// Test Unregister returns true and removes custom format.
    /// </summary>
    [TestCase]
    public void TestUnregister_RemovesCustomFormat()
    {
        AutoTileFormatRegistry.Register(CreateCustomFormat("unregister_test"));
        AssertBool(AutoTileFormatRegistry.Contains("unregister_test")).IsTrue();

        var result = AutoTileFormatRegistry.Unregister("unregister_test");

        AssertBool(result).IsTrue();
        AssertBool(AutoTileFormatRegistry.Contains("unregister_test")).IsFalse();
    }

    /// <summary>
    /// Test Unregister returns false for non-existent format.
    /// </summary>
    [TestCase]
    public void TestUnregister_ReturnsFalseForNonExistent()
    {
        var result = AutoTileFormatRegistry.Unregister("never_existed");

        AssertBool(result).IsFalse();
    }

    /// <summary>
    /// Test Unregister is case-insensitive.
    /// </summary>
    [TestCase]
    public void TestUnregister_CaseInsensitive()
    {
        AutoTileFormatRegistry.Register(CreateCustomFormat("case_test"));

        var result = AutoTileFormatRegistry.Unregister("CASE_TEST");

        AssertBool(result).IsTrue();
        AssertBool(AutoTileFormatRegistry.Contains("case_test")).IsFalse();
    }

    // ==================== ClearCustomFormats ====================

    /// <summary>
    /// Test ClearCustomFormats removes custom formats.
    /// </summary>
    [TestCase]
    public void TestClearCustomFormats_RemovesCustom()
    {
        AutoTileFormatRegistry.Register(CreateCustomFormat("clear_test_1"));
        AutoTileFormatRegistry.Register(CreateCustomFormat("clear_test_2"));

        AutoTileFormatRegistry.ClearCustomFormats();

        AssertBool(AutoTileFormatRegistry.Contains("clear_test_1")).IsFalse();
        AssertBool(AutoTileFormatRegistry.Contains("clear_test_2")).IsFalse();
    }

    /// <summary>
    /// Test ClearCustomFormats preserves built-in formats.
    /// </summary>
    [TestCase]
    public void TestClearCustomFormats_PreservesBuiltIns()
    {
        AutoTileFormatRegistry.Register(CreateCustomFormat("to_be_cleared"));

        AutoTileFormatRegistry.ClearCustomFormats();

        AssertBool(AutoTileFormatRegistry.Contains("corner16")).IsTrue();
        AssertBool(AutoTileFormatRegistry.Contains("edge16")).IsTrue();
        AssertBool(AutoTileFormatRegistry.Contains("blob47")).IsTrue();
    }

    /// <summary>
    /// Test ClearCustomFormats is idempotent.
    /// </summary>
    [TestCase]
    public void TestClearCustomFormats_Idempotent()
    {
        AutoTileFormatRegistry.ClearCustomFormats();
        AutoTileFormatRegistry.ClearCustomFormats();

        // Should not throw and built-ins should remain
        AssertThat(AutoTileFormatRegistry.GetAll().Count).IsGreaterEqual(3);
    }

    /// <summary>
    /// Test GetAll count decreases after ClearCustomFormats.
    /// </summary>
    [TestCase]
    public void TestClearCustomFormats_DecreasesCount()
    {
        AutoTileFormatRegistry.Register(CreateCustomFormat("count_test_1"));
        AutoTileFormatRegistry.Register(CreateCustomFormat("count_test_2"));
        var countBefore = AutoTileFormatRegistry.GetAll().Count;

        AutoTileFormatRegistry.ClearCustomFormats();

        var countAfter = AutoTileFormatRegistry.GetAll().Count;
        AssertThat(countAfter).IsLess(countBefore);
        AssertThat(countAfter).IsEqual(3); // Only built-ins remain
    }

    // ==================== GetAll ====================

    /// <summary>
    /// Test GetAll returns read-only collection.
    /// </summary>
    [TestCase]
    public void TestGetAll_ReturnsReadOnlyCollection()
    {
        var formats = AutoTileFormatRegistry.GetAll();

        // IReadOnlyCollection - verify we can iterate and count
        AssertThat(formats.Count).IsGreaterEqual(3);
        var count = 0;
        foreach (var _ in formats) count++;
        AssertThat(count).IsEqual(formats.Count);
    }

    /// <summary>
    /// Test GetAll includes both built-in and custom formats.
    /// </summary>
    [TestCase]
    public void TestGetAll_IncludesCustomFormats()
    {
        AutoTileFormatRegistry.Register(CreateCustomFormat("getall_custom"));

        var formats = AutoTileFormatRegistry.GetAll();
        var names = formats.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        AssertBool(names.Contains("corner16")).IsTrue();
        AssertBool(names.Contains("getall_custom")).IsTrue();
    }

    // ==================== Thread Safety (Basic) ====================

    /// <summary>
    /// Test EnsureBuiltInsRegistered is idempotent.
    /// </summary>
    [TestCase]
    public void TestEnsureBuiltInsRegistered_Idempotent()
    {
        AutoTileFormatRegistry.EnsureBuiltInsRegistered();
        AutoTileFormatRegistry.EnsureBuiltInsRegistered();
        AutoTileFormatRegistry.EnsureBuiltInsRegistered();

        // Should not create duplicate entries
        var formats = AutoTileFormatRegistry.GetAll();
        var corner16Count = formats.Count(f => f.Name.Equals("corner16", StringComparison.OrdinalIgnoreCase));
        AssertThat(corner16Count).IsEqual(1);
    }
}
